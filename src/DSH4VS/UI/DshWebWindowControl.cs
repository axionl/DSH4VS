using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.UI;

namespace DSH4VS.UI
{
    /// <summary>
    /// DSH Web 工具窗口的远程用户控件。对应的 <c>DshWebWindowControl.xaml</c>（DataTemplate）以
    /// 内嵌资源方式随程序集发布，由 Remote UI 在 VS 进程中实例化。
    /// </summary>
    internal sealed class DshWebWindowControl : RemoteUserControl
    {
        private readonly DshWebWindowViewModel viewModel;

        #region 构造函数

        /// <summary>创建 DSH Web 远程控件及其数据上下文。</summary>
        /// <param name="extensibility">扩展入口提供的 VisualStudioExtensibility 对象。</param>
        public DshWebWindowControl(VisualStudioExtensibility extensibility,
            IClientContext clientContext)
            : base(dataContext: new DshWebWindowViewModel(extensibility, clientContext))
        {
            viewModel = (DshWebWindowViewModel)DataContext;
        }

        #endregion

        #region 生命周期

        /// <summary>
        /// 控件在 VS 进程中加载完成时回调，此时探测 dsh web 状态并驱动 UI。
        /// </summary>
        public override async Task ControlLoadedAsync(CancellationToken cancellationToken)
        {
            await viewModel.InitializeAsync(cancellationToken);
            await base.ControlLoadedAsync(cancellationToken);
        }

        #endregion
    }
}
