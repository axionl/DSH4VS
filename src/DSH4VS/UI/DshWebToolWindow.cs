using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.ToolWindows;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;

namespace DSH4VS.UI
{
    /// <summary>
    /// 承载 DSH Web UI 的 VisualStudio.Extensibility 工具窗口。
    /// </summary>
    [VisualStudioContribution]
    internal sealed class DshWebToolWindow : ToolWindow
    {
        #region 初始化

        /// <summary>初始化 DSH Web 工具窗口并设置窗口标题。</summary>
        public DshWebToolWindow()
        {
            Title = "DSH";
        }

        #endregion

        #region 工具窗口配置

        /// <summary>获取 DSH Web 工具窗口的停靠配置。</summary>
        public override ToolWindowConfiguration ToolWindowConfiguration => new()
        {
            Placement = ToolWindowPlacement.DocumentWell
        };

        #endregion

        #region 内容创建

        /// <summary>创建承载 DSH Web UI 的远程控件。</summary>
        /// <param name="cancellationToken">内容创建的取消标记。</param>
        public override Task<IRemoteUserControl> GetContentAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IRemoteUserControl>(new DshWebWindowControl(Extensibility));
        }

        #endregion
    }
}
