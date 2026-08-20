using System;
using System.Collections.Generic;
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

        /// <summary>Visual Studio Shell 客户端上下文键和值的诊断摘要。</summary>
        public string ClientContextDetails { get; set; }

        /// <summary>当前编辑器中的选中文本。</summary>
        public string SelectionText { get; set; }

        /// <summary>当前主光标所在的 1 起始行号。</summary>
        public int CursorLine { get; set; }

        /// <summary>当前主光标所在的 1 起始列号。</summary>
        public int CursorColumn { get; set; }

        /// <summary>当前主光标所在行的文本。</summary>
        public string CurrentLineText { get; set; }

        /// <summary>当前 Visual Studio 中已经打开的文档集合。</summary>
        public IReadOnlyList<DSHOpenDocument> OpenedDocuments { get; set; }

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
                var selectionUri = ReadContextValue(context, ClientContextKey.Shell.ActiveSelectionUri);
                var selectionPath = ReadContextValue(context, ClientContextKey.Shell.ActiveSelectionPath);
                var selectionFileName = ReadContextValue(context, ClientContextKey.Shell.ActiveSelectionFileName);
                var editorContentType = ReadContextValue(context, ClientContextKey.Shell.ActiveEditorContentType);
                var editorFileName = ReadContextValue(context, ClientContextKey.Shell.ActiveEditorFileName);
                var normalizedSelectionPath = NormalizeFilePath(selectionPath);
                var normalizedSelectionUri = NormalizeFilePath(selectionUri);
                ctx.FilePath = !string.IsNullOrWhiteSpace(normalizedSelectionPath)
                    ? normalizedSelectionPath
                    : !string.IsNullOrWhiteSpace(normalizedSelectionUri)
                        ? normalizedSelectionUri
                    : !string.IsNullOrWhiteSpace(editorFileName)
                        ? editorFileName
                        : selectionFileName;
                ctx.ClientContextDetails = string.Join(" | ",
                    "活动选择 URI=" + FormatContextValue(selectionUri),
                    "活动选择路径=" + FormatContextValue(selectionPath),
                    "活动选择文件名=" + FormatContextValue(selectionFileName),
                    "编辑器内容类型=" + FormatContextValue(editorContentType),
                    "编辑器文件名=" + FormatContextValue(editorFileName));
            }
            catch
            {
                // 无活动文档
            }

            // 编辑器选区和光标
            try
            {
                var textView = await extensibility.Editor().GetActiveTextViewAsync(
                    context, cancellationToken);
                if (textView != null)
                {
                    var selection = textView.Selection;
                    if (!selection.IsEmpty)
                    {
                        var selectedRange = new TextRange(
                            textView.Document,
                            selection.Start.Offset,
                            selection.End.Offset - selection.Start.Offset);
                        var selectedChars = new char[selectedRange.Length];
                        selectedRange.CopyTo(selectedChars);
                        ctx.SelectionText = new string(selectedChars);
                    }

                    ctx.FilePath = string.IsNullOrWhiteSpace(ctx.FilePath)
                        ? textView.FilePath
                        : ctx.FilePath;
                    ctx.CursorLine = textView.Document.GetLineNumberFromPosition(
                        selection.ActivePosition.Offset) + 1;
                    var currentLine = textView.Document.GetLineFromPosition(
                        selection.ActivePosition.Offset);
                    ctx.CursorColumn = 0;
                    var currentLineChars = new char[currentLine.Text.Length];
                    currentLine.Text.CopyTo(currentLineChars);
                    ctx.CurrentLineText = new string(currentLineChars);
                }
            }
            catch
            {
                // 编辑器视图不可用时保留 Shell 上下文
            }

            // 包含活动文件的项目（目录向上查找项目文件）
            ctx.ProjectPath = FindProjectPath(ctx.FilePath);

            ctx.OpenedDocuments = DshOpenDocumentReader.Read();

            return ctx;
        }

        /// <summary>读取一个 Visual Studio 客户端上下文值；读取失败时返回空值。</summary>
        /// <param name="context">Visual Studio 客户端上下文。</param>
        /// <param name="key">需要读取的上下文键。</param>
        /// <returns>上下文值；不可用时为空字符串。</returns>
        private static string ReadContextValue(IClientContext context, ClientContextKey key)
        {
            try
            {
                return context[key];
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>为诊断文本标记空上下文值。</summary>
        /// <param name="value">上下文值。</param>
        /// <returns>实际值或空值标记。</returns>
        private static string FormatContextValue(string value)
        {
            return string.IsNullOrEmpty(value)
                ? "&lt;empty&gt;"
                : value.Replace("\\\\", "\\");
        }

        /// <summary>将客户端上下文中的文件 URI 或路径转换为本地文件路径。</summary>
        /// <param name="value">文件 URI 或路径。</param>
        /// <returns>本地文件路径；无法转换时返回空值。</returns>
        private static string NormalizeFilePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.IsFile)
            {
                return uri.LocalPath;
            }

            return value;
        }

        #endregion

        #region 路径辅助

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