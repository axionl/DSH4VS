using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using DSH4VS.Core;

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

    /// <summary>
    /// 扩展初始化完成后，订阅 <see cref="ExtensionCore.ShutdownToken" />——这是框架提供的
    /// “扩展关闭”监视令牌，在 VS 退出/扩展卸载时被取消，比 AppDomain 进程退出事件更可靠。
    /// 令牌取消时停止由本扩展启动的 dsh web；外部已启动的 web 不会被触碰
    /// （<see cref="DshRunner.StopWeb" /> 仅在扩展自身启动过 web 时才杀进程，否则为无操作）。
    /// </summary>
    /// <param name="extensibility">扩展的 Extensibility 对象。</param>
    /// <param name="cancellationToken">用于监视取消的令牌。</param>
    protected override async Task OnInitializedAsync(
        VisualStudioExtensibility extensibility,
        CancellationToken cancellationToken)
    {
        await base.OnInitializedAsync(extensibility, cancellationToken);
        ShutdownToken.Register(static () => DshRunner.StopAll());
    }
}
