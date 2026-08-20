using System;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.UI;
using DSH4VS.Core;

namespace DSH4VS.UI
{
    /// <summary>
    /// DSH Web 工具窗口的远程 UI 数据上下文：负责检查/启动 dsh web 并驱动浏览器导航。
    /// 实例存活于扩展进程，仅 [DataMember] 属性通过 Remote UI 同步到 VS 进程。
    /// </summary>
    [DataContract]
    public sealed class DshWebWindowViewModel : NotifyPropertyChangedObject
    {
        #region 常量

        /// <summary>上下文同步服务已连接（状态灯：青绿色）。</summary>
        private const string ContextStateConnected = "connected";

        /// <summary>上下文同步服务未连接（状态灯：橘黄色）。</summary>
        private const string ContextStateNotConnected = "notconnected";

        /// <summary>上下文同步功能未启用（状态灯：灰色）。</summary>
        private const string ContextStateDisabled = "disabled";

        #endregion

        #region 私有字段

        private string detail = "正在检查 DSH Web UI…";

        private Visibility browserVisibility = Visibility.Collapsed;

        private Visibility overlayVisibility = Visibility.Visible;

        private bool isStarting;

        private string browserSource = DshRunner.WebUrl;

        private string webAddress = DshRunner.WebUrl;

        private string dshVersion = "版本：检查中…";

        private string webCommand = "npx --yes @deepseek-ai/dsh web --port 13080";

        private string webPort = DshRunner.WebPort.ToString();

        private string webOutput = "等待启动 DSH Web UI…";

        private string webProcessId = "进程 ID：未知";

        private string dshPath = "检查中…";

        private string environmentStatus = "正在检查 Node.js、npx 和 DSH 环境…";

        private string activeContextStatus = "活动上下文：尚未同步";

        private string activeContextState = ContextStateDisabled;

        private readonly VisualStudioExtensibility extensibility;

        private static IClientContext latestClientContext;

        private static DshWebWindowViewModel latestInstance;

        private IClientContext clientContext;

        #endregion

        #region 构造函数

        /// <summary>初始化 DSH Web 窗口数据上下文和启动命令。</summary>
        /// <param name="extensibility">扩展入口提供的 VisualStudioExtensibility 对象。</param>
        /// <param name="clientContext">打开工具窗口时的 Visual Studio 客户端上下文。</param>
        public DshWebWindowViewModel(VisualStudioExtensibility extensibility,
            IClientContext clientContext)
        {
            this.extensibility = extensibility;
            this.clientContext = clientContext ?? latestClientContext;
            latestInstance = this;
            activeContextState = this.clientContext != null
                ? ContextStateNotConnected
                : ContextStateDisabled;
            StartWebCommand = new AsyncCommand(StartWebAsync);
            StopWebCommand = new AsyncCommand(StopWebAsync);
            CopyWebCommand = new AsyncCommand(CopyWebCommandAsync);
            CheckEnvironmentCommand = new AsyncCommand(CheckEnvironmentAsync);
            InstallNodeCommand = new AsyncCommand(InstallNodeAsync);
            InstallDshCommand = new AsyncCommand(InstallDshAsync);
        }

        /// <summary>显示 Node.js、npx 和 DSH CLI 的环境检测结果。</summary>
        [DataMember]
        public string EnvironmentStatus
        {
            get => environmentStatus;
            private set => SetProperty(ref environmentStatus, value);
        }

        /// <summary>显示当前 Visual Studio 活动文件、光标位置和选中文本状态。</summary>
        [DataMember]
        public string ActiveContextStatus
        {
            get => activeContextStatus;
            private set => SetProperty(ref activeContextStatus, value);
        }

        /// <summary>
        /// 活动上下文状态灯颜色标识：<c>connected</c>（青绿色）表示同步服务已连接，
        /// <c>notconnected</c>（橘黄色）表示未连接，<c>disabled</c>（灰色）表示未启用。
        /// </summary>
        [DataMember]
        public string ActiveContextState
        {
            get => activeContextState;
            private set => SetProperty(ref activeContextState, value);
        }

        /// <summary>更新工具窗口复用时使用的最新 Visual Studio 客户端上下文。</summary>
        /// <param name="context">当前 Visual Studio 客户端上下文。</param>
        public static void SetLatestClientContext(IClientContext context)
        {
            latestClientContext = context;
        }

        /// <summary>将菜单命令的上下文同步结果更新到当前工具窗口。</summary>
        /// <param name="context">同步得到的 Visual Studio 上下文；失败时可以为空。</param>
        /// <param name="error">同步失败原因；成功时为空。</param>
        /// <param name="kind">同步类型，例如活动文档或光标位置。</param>
        public static void ReportContextSyncResult(
            DSHAskContext context, Exception error, string kind)
        {
            var instance = latestInstance;
            if (instance == null)
            {
                return;
            }

            if (error == null)
            {
                instance.ActiveContextStatus = FormatContextStatus(context)
                    + " | 已同步" + kind;
                instance.ActiveContextState = ContextStateConnected;
                return;
            }

            instance.ActiveContextState = ContextStateNotConnected;
            instance.ActiveContextStatus = "活动上下文：同步" + kind + "失败：" + error.Message;
        }

        /// <summary>DSH Web UI 监听端口，默认值为 3080。</summary>
        [DataMember]
        public string WebPort
        {
            get => webPort;
            set
            {
                if (SetProperty(ref webPort, value))
                {
                    UpdateWebConfiguration();
                }
            }
        }

        /// <summary>显示 DSH 命令可执行程序或脚本的所在位置，不包含字段标签。</summary>
        [DataMember]
        public string DshPath
        {
            get => dshPath;
            private set => SetProperty(ref dshPath, value);
        }

        /// <summary>显示 DSH Web UI 进程 ID。</summary>
        [DataMember]
        public string WebProcessId
        {
            get => webProcessId;
            private set => SetProperty(ref webProcessId, value);
        }

        /// <summary>显示 DSH Web UI 启动过程中的输出信息。</summary>
        [DataMember]
        public string WebOutput
        {
            get => webOutput;
            private set => SetProperty(ref webOutput, value);
        }

        /// <summary>显示用于启动 DSH Web UI 的命令。</summary>
        [DataMember]
        public string WebCommand
        {
            get => webCommand;
            set => SetProperty(ref webCommand, value);
        }

        /// <summary>显示 DSH Web UI 的访问地址。</summary>
        [DataMember]
        public string WebAddress
        {
            get => webAddress;
            set => SetProperty(ref webAddress, value);
        }

        /// <summary>显示当前 DSH CLI 版本。</summary>
        [DataMember]
        public string DshVersion
        {
            get => dshVersion;
            set => SetProperty(ref dshVersion, value);
        }

        #endregion

        #region 可绑定属性

        /// <summary>覆盖层提示信息。</summary>
        [DataMember]
        public string Detail
        {
            get => detail;
            set => SetProperty(ref detail, value);
        }

        /// <summary>WebView2 可见性。</summary>
        [DataMember]
        public Visibility BrowserVisibility
        {
            get => browserVisibility;
            set => SetProperty(ref browserVisibility, value);
        }

        /// <summary>启动覆盖层可见性。</summary>
        [DataMember]
        public Visibility OverlayVisibility
        {
            get => overlayVisibility;
            set => SetProperty(ref overlayVisibility, value);
        }

        /// <summary>是否正在启动 dsh web。</summary>
        [DataMember]
        public bool IsStarting
        {
            get => isStarting;
            set => SetProperty(ref isStarting, value);
        }

        /// <summary>WebView2 导航地址。</summary>
        [DataMember]
        public string BrowserSource
        {
            get => browserSource;
            set => SetProperty(ref browserSource, value);
        }

        /// <summary>启动 dsh web 的异步命令。</summary>
        [DataMember]
        public IAsyncCommand StartWebCommand { get; }

        /// <summary>停止由扩展启动的 dsh web 异步命令。</summary>
        [DataMember]
        public IAsyncCommand StopWebCommand { get; }

        /// <summary>复制 DSH Web UI 启动命令的异步命令。</summary>
        [DataMember]
        public IAsyncCommand CopyWebCommand { get; }

        /// <summary>重新检查 Node.js、npx 和 DSH CLI 环境。</summary>
        [DataMember]
        public IAsyncCommand CheckEnvironmentCommand { get; }

        /// <summary>下载安装 Node.js。</summary>
        [DataMember]
        public IAsyncCommand InstallNodeCommand { get; }

        /// <summary>通过 npx 安装 DSH CLI。</summary>
        [DataMember]
        public IAsyncCommand InstallDshCommand { get; }

        #endregion

        #region Web UI 初始化与启动

        /// <summary>
        /// 控件加载完成时调用：探测 dsh web 是否已就绪并决定显示浏览器或启动覆盖层。
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            try
            {
                // 保留环境检查在窗口加载生命周期中执行，确保打开窗口即可看到检测结果。
                await CheckEnvironmentAsync(null, cancellationToken);
                var output = await OutputPane.GetAsync(extensibility, cancellationToken);
                if (TryGetWebPort(out var port) && await DshRunner.IsWebUpAsync(port))
                {
                    if (clientContext != null)
                    {
                        DshContextBridge.Start(output);
                        DshContextAutoSync.Start(extensibility, clientContext, output);
                        await SynchronizeContextAsync(output, cancellationToken);
                    }
                    else
                    {
                        ActiveContextState = ContextStateDisabled;
                    }

                    await ShowBrowserAsync();
                }
                else
                {
                    ActiveContextState = ContextStateDisabled;
                    Detail = TryGetWebPort(out _)
                        ? "如果 dsh web 尚未运行，请点击下方按钮启动。"
                        : "端口号无效，请输入 1 到 65535 之间的整数。";
                    WebOutput = "DSH Web UI 尚未运行。";
                    output.WriteLine("[DSH] DSH Web UI 未运行，等待用户启动。");
                }
            }
            catch (Exception ex)
            {
                Detail = "无法访问 DSH Web UI：" + ex.Message;
            }
        }

        /// <summary>检查 Node.js、npx 和 DSH CLI，并更新工具窗口中的状态和按钮。</summary>
        private async Task CheckEnvironmentAsync(object parameter, CancellationToken cancellationToken)
        {
            EnvironmentStatus = "正在检查 Node.js、npx 和 DSH 环境…";
            var status = await DshLocator.CheckEnvironmentAsync();
            EnvironmentStatus = (status.HasNode ? "Node.js " + status.NodeVersion : "未找到 Node.js") + "；"
                + (status.HasNpx ? "npx " + status.NpxVersion : "未找到 npx") + "；"
                + (status.HasDsh ? "已找到 DSH " + status.DshVersion : "未找到 DSH");
            DshPath = DshLocator.Locate()?.Display ?? "未找到";
            DshVersion = string.IsNullOrWhiteSpace(status.DshVersion)
                ? "版本：未知"
                : "版本：" + status.DshVersion;
        }

        /// <summary>下载安装 Node.js 并重新检查环境。</summary>
        private async Task InstallNodeAsync(object parameter, CancellationToken cancellationToken)
        {
            EnvironmentStatus = "正在从 Node.js 官方源下载安装包，请稍候…";
            var error = await DshLocator.InstallNodeJsAsync();
            if (!string.IsNullOrWhiteSpace(error))
            {
                EnvironmentStatus = error;
                return;
            }

            await CheckEnvironmentAsync(null, cancellationToken);
        }

        /// <summary>通过 npx 安装 DSH CLI 并重新检查环境。</summary>
        private async Task InstallDshAsync(object parameter, CancellationToken cancellationToken)
        {
            EnvironmentStatus = "正在通过 npx 安装 @deepseek-ai/dsh，请稍候…";
            var error = await DshLocator.InstallDshAsync();
            if (!string.IsNullOrWhiteSpace(error))
            {
                EnvironmentStatus = error;
                return;
            }

            await CheckEnvironmentAsync(null, cancellationToken);
        }

        /// <summary>启动 dsh web 服务，并在服务就绪后显示浏览器。</summary>
        /// <param name="parameter">命令参数。</param>
        /// <param name="cancellationToken">启动操作的取消标记。</param>
        private async Task StartWebAsync(object parameter, CancellationToken cancellationToken)
        {
            IsStarting = true;
            Detail = "正在启动 dsh web，请稍候…";
            WebOutput = string.Empty;
            try
            {
                if (!TryGetWebPort(out var port))
                {
                    Detail = "端口号无效，请输入 1 到 65535 之间的整数。";
                    WebOutput = Detail;
                    return;
                }

                var availablePort = DshRunner.FindAvailableWebPort(port);
                if (port == DshContextBridge.Port)
                {
                    availablePort = DshRunner.FindAvailableWebPort(port + 1);
                }
                if (availablePort == 0)
                {
                    Detail = "没有可用的 Web UI 端口。";
                    WebOutput = Detail;
                    return;
                }

                if (availablePort != port)
                {
                    AppendWebOutput($"[DSH] 端口 {port} 不可用，自动切换到 {availablePort}。");
                    WebPort = availablePort.ToString();
                    port = availablePort;
                }

                UpdateWebConfiguration();
                var output = await OutputPane.GetAsync(extensibility, cancellationToken);
                if (clientContext != null)
                {
                    DshContextBridge.Start(output);
                    DshContextAutoSync.Start(extensibility, clientContext, output);
                    await SynchronizeContextAsync(output, cancellationToken);
                }
                else
                {
                    ActiveContextState = ContextStateDisabled;
                }

                var actualPort = await DshRunner.StartWebAsync(
                    new CompositeOutput(output, AppendWebOutput), port);
                if (actualPort > 0)
                {
                    if (actualPort != port)
                    {
                        WebPort = actualPort.ToString();
                    }

                    await ShowBrowserAsync();
                }
                else
                {
                    DshContextAutoSync.Stop();
                    ActiveContextState = ContextStateDisabled;
                    Detail = "启动失败。请确认 Node.js 和 DSH 已安装后重试。";
                    AppendWebOutput("[DSH] 启动失败。请确认 Node.js 和 DSH 已安装后重试。");
                }
            }
            catch (Exception ex)
            {
                Detail = "启动失败：" + ex.Message;
                await LogAsync("启动 DSH Web UI 失败", ex);
            }
            finally
            {
                IsStarting = false;
            }
        }

        /// <summary>停止由扩展启动的 dsh web 服务并返回启动界面。</summary>
        /// <param name="parameter">命令参数。</param>
        /// <param name="cancellationToken">停止操作的取消标记。</param>
        private async Task StopWebAsync(object parameter, CancellationToken cancellationToken)
        {
            IsStarting = true;
            Detail = "正在停止 dsh web，请稍候…";
            try
            {
                var output = await OutputPane.GetAsync(extensibility, cancellationToken);
                var port = int.TryParse(WebPort, out var configuredPort)
                    ? configuredPort
                    : DshRunner.WebPort;
                if (DshRunner.StopWeb(port))
                {
                    DshContextAutoSync.Stop();
                    output.WriteLine("[DSH] DSH Web UI 已停止。");
                    BrowserVisibility = Visibility.Collapsed;
                    OverlayVisibility = Visibility.Visible;
                    WebProcessId = "进程 ID：未知";
                    ActiveContextStatus = "活动上下文：尚未同步";
                    ActiveContextState = ContextStateDisabled;
                    Detail = "DSH Web UI 已停止，可再次点击按钮启动。";
                }
                else
                {
                    DshContextAutoSync.Stop();
                    ActiveContextState = ContextStateDisabled;
                    Detail = "当前 Web UI 不是由本扩展启动，无法自动停止。";
                }
            }
            catch (Exception ex)
            {
                Detail = "停止失败：" + ex.Message;
                await LogAsync("停止 DSH Web UI 失败", ex);
            }
            finally
            {
                IsStarting = false;
            }
        }

        /// <summary>将当前 DSH Web UI 启动命令复制到系统剪贴板。</summary>
        private async Task CopyWebCommandAsync(object parameter, CancellationToken cancellationToken)
        {
            try
            {
                await SetClipboardTextAsync(WebCommand);
                Detail = "启动命令已复制到剪贴板。";
            }
            catch (Exception ex)
            {
                Detail = "复制命令失败：" + ex.Message;
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>立即同步一次当前 Visual Studio 上下文，不阻止 Web UI 启动。</summary>
        /// <param name="output">输出通道。</param>
        /// <param name="cancellationToken">取消标记。</param>
        private async Task SynchronizeContextAsync(IDshOutput output,
            CancellationToken cancellationToken)
        {
            try
            {
                var context = await DshContextBridge.SyncAsync(
                    extensibility, clientContext, cancellationToken);
                ActiveContextStatus = FormatContextStatus(context);
                ActiveContextState = ContextStateConnected;
                output.WriteLine("[DSH] 已在启动 Web UI 前自动同步 Visual Studio 上下文。");
            }
            catch (Exception ex)
            {
                ActiveContextStatus = "活动上下文：同步失败";
                ActiveContextState = ContextStateNotConnected;
                output.WriteLine("[DSH] 自动同步 Visual Studio 上下文失败，继续启动 Web UI：" + ex.Message);
            }
        }

        /// <summary>将捕获的 Visual Studio 上下文转换为状态栏文本。</summary>
        /// <param name="context">当前活动上下文。</param>
        /// <returns>适合显示在工具窗口底部的上下文摘要。</returns>
        private static string FormatContextStatus(DSHAskContext context)
        {
            if (context == null || (string.IsNullOrWhiteSpace(context.FilePath)
                && (context.OpenedDocuments == null || context.OpenedDocuments.Count == 0)))
            {
                return "活动上下文：无活动文档";
            }

            var openedDocuments = context.OpenedDocuments == null || context.OpenedDocuments.Count == 0
                ? string.Empty
                : $" | 已打开 {context.OpenedDocuments.Count} 个文档";
            var position = context.CursorLine > 0 && context.CursorColumn > 0
                ? $" | 行 {context.CursorLine}，列 {context.CursorColumn}"
                : string.Empty;
            var selection = string.IsNullOrEmpty(context.SelectionText)
                ? string.Empty
                : " | 已选中 " + context.SelectionText.Length + " 个字符";
            var details = string.IsNullOrWhiteSpace(context.ClientContextDetails)
                ? string.Empty
                : " | " + context.ClientContextDetails;
            var displayPath = context.FilePath.Replace("\\\\", "\\");
            return $"活动上下文：{displayPath}{position}{selection}{details}";
        }

        /// <summary>将异常写入 DSH 输出窗格。</summary>
        /// <param name="message">异常上下文说明。</param>
        /// <param name="exception">需要记录的异常。</param>
        private async Task LogAsync(string message, Exception exception)
        {
            try
            {
                var output = await OutputPane.GetAsync(extensibility, CancellationToken.None);
                output.WriteLine($"[DSH] {message}: {exception}");
            }
            catch
            {
                // 输出窗格不可用时保留工具窗口中的错误信息
            }
        }

        /// <summary>切换到浏览器视图并显示 DSH Web UI 地址。</summary>
        private void ShowBrowser()
        {
            BrowserSource = DshRunner.GetWebUrl(GetWebPortOrDefault());
            BrowserVisibility = Visibility.Visible;
            OverlayVisibility = Visibility.Collapsed;
            Detail = "DSH Web UI 已就绪。";
        }

        /// <summary>显示浏览器并刷新状态栏中的 DSH 版本信息。</summary>
        private async Task ShowBrowserAsync()
        {
            ShowBrowser();
            WebAddress = DshRunner.GetWebUrl(GetWebPortOrDefault());
            WebProcessId = DshRunner.WebProcessId > 0
                ? "进程 ID：" + DshRunner.WebProcessId
                : "进程 ID：外部进程";
            await CheckEnvironmentAsync(null, CancellationToken.None);
        }

        /// <summary>尝试读取并验证当前 Web UI 端口。</summary>
        private bool TryGetWebPort(out int port)
        {
            return int.TryParse(WebPort, out port) && port >= 1 && port <= 65535;
        }

        /// <summary>获取当前端口；无效时返回默认端口。</summary>
        private int GetWebPortOrDefault() => TryGetWebPort(out var port) ? port : DshRunner.WebPort;

        /// <summary>根据当前端口刷新地址和启动命令。</summary>
        private void UpdateWebConfiguration()
        {
            var port = GetWebPortOrDefault();
            WebAddress = DshRunner.GetWebUrl(port);
            WebCommand = $"npx --yes @deepseek-ai/dsh web --port {port}";
        }

        /// <summary>在单线程单元线程上写入系统剪贴板。</summary>
        /// <param name="text">需要复制的文本。</param>
        private static Task SetClipboardTextAsync(string text)
        {
            var completion = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var thread = new Thread(() =>
            {
                try
                {
                    Clipboard.SetText(text ?? string.Empty);
                    completion.SetResult(null);
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            })
            {
                IsBackground = true,
                Name = "DSH Clipboard STA"
            };

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }

        /// <summary>追加前端显示的启动输出，并限制日志长度。</summary>
        private void AppendWebOutput(string text)
        {
            var value = string.IsNullOrEmpty(WebOutput)
                ? text
                : WebOutput + Environment.NewLine + text;
            WebOutput = value.Length > 8000 ? value[^8000..] : value;
        }

        /// <summary>同时写入 VS 输出窗格和 Web UI 前端的输出适配器。</summary>
        private sealed class CompositeOutput : IDshOutput
        {
            private readonly IDshOutput output;
            private readonly Action<string> append;

            /// <summary>初始化复合输出适配器。</summary>
            public CompositeOutput(IDshOutput output, Action<string> append)
            {
                this.output = output;
                this.append = append;
            }

            /// <inheritdoc />
            public void Write(string text)
            {
                output.Write(text);
                append(text);
            }

            /// <inheritdoc />
            public void WriteLine(string text)
            {
                output.WriteLine(text);
                append(text);
            }
        }

        #endregion
    }
}