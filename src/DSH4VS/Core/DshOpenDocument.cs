using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Dte = EnvDTE.DTE;
using Microsoft.VisualStudio.Shell;

namespace DSH4VS.Core
{
    /// <summary>已打开文档的轻量上下文。</summary>
    public sealed class DSHOpenDocument
    {
        /// <summary>文档路径；未保存文档可能为空。</summary>
        public string FilePath { get; set; }

        /// <summary>文档名称。</summary>
        public string Name { get; set; }

        /// <summary>是否为 DTE 当前活动文档。</summary>
        public bool IsActive { get; set; }
    }

    /// <summary>通过 DTE.Documents 读取 Visual Studio 已打开文档。</summary>
    internal static class DshOpenDocumentReader
    {
        #region 文档读取

        /// <summary>读取当前 Visual Studio 实例中所有已打开的文档。</summary>
        /// <returns>已打开文档的轻量快照。</returns>
        public static IReadOnlyList<DSHOpenDocument> Read()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var documents = new List<DSHOpenDocument>();
            foreach (var progId in new[] { "VisualStudio.DTE.18.0", "VisualStudio.DTE.17.0" })
            {
                var dte = GetRunningDte(progId);
                if (dte?.Documents == null)
                {
                    continue;
                }

                for (var index = 1; index <= dte.Documents.Count; index++)
                {
                    try
                    {
                        var document = dte.Documents.Item(index);
                        var path = document?.FullName;
                        var name = document?.Name;
                        if (!string.IsNullOrWhiteSpace(path) || !string.IsNullOrWhiteSpace(name))
                        {
                            documents.Add(new DSHOpenDocument
                            {
                                FilePath = path,
                                Name = name,
                                IsActive = string.Equals(path, dte.ActiveDocument?.FullName,
                                    StringComparison.OrdinalIgnoreCase)
                            });
                        }

                        if (document != null && Marshal.IsComObject(document))
                        {
                            Marshal.FinalReleaseComObject(document);
                        }
                    }
                    catch
                    {
                        // 单个文档不可访问时继续读取其余文档。
                    }
                }

                return documents;
            }

            return documents;
        }

        /// <summary>从 COM Running Object Table 获取已运行的 Visual Studio DTE。</summary>
        /// <param name="progId">Visual Studio DTE ProgID。</param>
        /// <returns>已运行的 DTE；不存在时返回空值。</returns>
        private static Dte GetRunningDte(string progId)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (GetRunningObjectTable(0, out var table) != 0)
            {
                return null;
            }

            table.EnumRunning(out var enumerator);
            var monikers = new IMoniker[1];
            var fetched = IntPtr.Zero;
            while (enumerator.Next(1, monikers, fetched) == 0)
            {
                try
                {
                    monikers[0].GetDisplayName(null, null, out var displayName);
                    if (displayName.IndexOf(progId, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    table.GetObject(monikers[0], out var runningObject);
                    return runningObject as Dte;
                }
                catch
                {
                    // 忽略无法读取的 COM 项。
                }
                finally
                {
                    if (monikers[0] != null && Marshal.IsComObject(monikers[0]))
                    {
                        Marshal.FinalReleaseComObject(monikers[0]);
                        monikers[0] = null;
                    }
                }
            }

            return null;
        }

        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(uint reserved,
            out IRunningObjectTable runningObjectTable);

        #endregion
    }
}
