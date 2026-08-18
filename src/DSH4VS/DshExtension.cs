using System;
using Microsoft.VisualStudio.Extensibility;

namespace DSH4VS;

/// <summary>
/// DSH 的 VisualStudio.Extensibility 进程外扩展入口。
/// </summary>
[VisualStudioContribution]
internal sealed class DshExtension : Extension
{
    /// <summary>
    /// 提供进程外扩展的安装元数据。
    /// </summary>
    public override ExtensionConfiguration ExtensionConfiguration => new()
    {
        Metadata = new ExtensionMetadata(
            "DSH4VS",
            new Version(1, 0, 0),
            "Ariel AxionL (i@axionl.me)",
            "DSH for Visual Studio",
            "在 Visual Studio 中运行 DSH headless 任务并打开 DSH Web UI。")
        {
            DotnetTargetVersions = [DotnetTarget.Net8]
        }
    };
}
