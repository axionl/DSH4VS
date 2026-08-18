using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.ProjectSystem.Query;

namespace DSH4VS.Core
{
    /// <summary>
    /// IDE 上下文（解决方案、项目、活动文件和选中文本），在命令执行时捕获并注入 DSH 任务。
    /// </summary>
    public sealed class DSHAskContext
    {
        #region 上下文属性

        /// <summary>当前解决方案文件路径。</summary>
        public string SolutionPath { get; set; }

        /// <summary>当前活动文件所属的项目文件路径。</summary>
        public string ProjectPath { get; set; }

        /// <summary>当前活动文件路径。</summary>
        public string FilePath { get; set; }

        /// <summary>当前编辑器中的选中文本。</summary>
        public string SelectionText { get; set; }

        /// <summary>DSH 使用的工作目录（其 cwd）。</summary>
        public string WorkspaceRoot
        {
            get
            {
                if (!string.IsNullOrEmpty(SolutionPath)) return Path.GetDirectoryName(SolutionPath);
                if (!string.IsNullOrEmpty(ProjectPath)) return Path.GetDirectoryName(ProjectPath);
                if (!string.IsNullOrEmpty(FilePath)) return Path.GetDirectoryName(FilePath);
                return Environment.CurrentDirectory;
            }
        }

        #endregion

        #region 上下文捕获

        /// <summary>
        /// 使用 VisualStudio.Extensibility 客户端上下文、编辑器视图和工作区查询捕获当前上下文。
        /// </summary>
        /// <param name="extensibility">扩展入口提供的 VisualStudioExtensibility 对象。</param>
        /// <param name="context">命令执行时传入的客户端上下文。</param>
        /// <param name="cancellationToken">取消标记。</param>
        public static async Task<DSHAskContext> FromClientContextAsync(
            VisualStudioExtensibility extensibility,
            IClientContext context,
            CancellationToken cancellationToken)
        {
            var ctx = new DSHAskContext();

            // 解决方案
            try
            {
                var solutions = await extensibility.Workspaces()
                    .QuerySolutionAsync(solution => solution.With(solution => solution.Path), cancellationToken);
                foreach (object item in solutions)
                {
                    ctx.SolutionPath = (item as ISolutionSnapshot)?.Path;
                    break;
                }
            }
            catch
            {
                // 未打开解决方案
            }

            // 活动文件
            try
            {
                ctx.FilePath = context[ClientContextKey.Shell.ActiveSelectionPath]
                    ?? context[ClientContextKey.Shell.ActiveEditorFileName];
                if (string.IsNullOrEmpty(ctx.FilePath))
                {
                    ctx.FilePath = (await extensibility.Editor().GetActiveTextViewAsync(context, cancellationToken))?.FilePath;
                }
            }
            catch
            {
                // 无活动文档
            }

            // 包含活动文件的项目（目录向上查找项目文件）
            ctx.ProjectPath = FindProjectPath(ctx.FilePath);

            // 选中文本
            try
            {
                var textView = await extensibility.Editor().GetActiveTextViewAsync(context, cancellationToken);
                var selection = textView?.Selection;
                if (selection != null && !selection.Value.IsEmpty)
                {
                    ctx.SelectionText = CopyToString(selection.Value.Extent);
                }
            }
            catch
            {
                // 仅光标或无选中文本
            }

            return ctx;
        }

        #endregion

        #region 路径辅助

        /// <summary>
        /// 将指定文本范围复制为字符串；复制失败时返回空值。
        /// </summary>
        /// <param name="range">需要复制的文本范围。</param>
        private static string CopyToString(TextRange range)
        {
            try
            {
                return TextExtensions.CopyToString(range);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从指定文件所在目录开始向上查找项目文件。
        /// </summary>
        /// <param name="filePath">用于定位项目的文件路径。</param>
        /// <returns>找到的项目文件路径；未找到时返回空值。</returns>
        private static string FindProjectPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            var directory = Path.GetDirectoryName(filePath);
            while (directory != null)
            {
                try
                {
                    var projectFile = Directory.GetFiles(directory, "*.csproj").FirstOrDefault()
                        ?? Directory.GetFiles(directory, "*.vbproj").FirstOrDefault()
                        ?? Directory.GetFiles(directory, "*.fsproj").FirstOrDefault()
                        ?? Directory.GetFiles(directory, "*.vcxproj").FirstOrDefault();
                    if (projectFile != null)
                    {
                        return projectFile;
                    }
                }
                catch
                {
                    // 目录不可读
                }
                directory = Path.GetDirectoryName(directory);
            }
            return null;
        }

        #endregion
    }
}