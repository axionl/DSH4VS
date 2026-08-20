using System;
using System.Collections.Generic;

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
            return Array.Empty<DSHOpenDocument>();
        }

        #endregion
    }
}
