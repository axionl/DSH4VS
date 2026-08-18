using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DSH4VS.Core;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DSH4VS.UI
{
    /// <summary>
    /// DSH 任务对话框的数据上下文，管理任务输入、IDE 上下文和环境检测状态。
    /// </summary>
    public sealed class PromptDialogViewModel : ObservableObject
    {
        #region 私有字段

        private const int MaxFileChars = 200000;
        private DSHAskContext context;
        private string promptValue;
        private string environmentStatus = "正在检查 Node.js、npx 和 DSH 环境…";
        private bool includeSelection;
        private bool includeFile;
        private bool hasSelection;
        private bool hasFile;
        private bool isBusy;
        private bool canInstallNode;
        private bool canInstallDsh;

        #endregion

        #region 可绑定属性

        /// <summary>
        /// 用户输入的任务说明。
        /// </summary>
        public string Prompt
        {
            get => promptValue;
            set => SetProperty(ref promptValue, value);
        }

        /// <summary>
        /// Node.js、npx 和 DSH 的当前环境检测结果。
        /// </summary>
        public string EnvironmentStatus
        {
            get => environmentStatus;
            private set => SetProperty(ref environmentStatus, value);
        }

        /// <summary>
        /// 是否将当前选中文本追加到任务上下文。
        /// </summary>
        public bool IncludeSelection
        {
            get => includeSelection;
            set => SetProperty(ref includeSelection, value);
        }

        /// <summary>
        /// 是否将当前文件内容追加到任务上下文。
        /// </summary>
        public bool IncludeFile
        {
            get => includeFile;
            set => SetProperty(ref includeFile, value);
        }

        /// <summary>
        /// 当前是否存在可用的选中文本。
        /// </summary>
        public bool HasSelection
        {
            get => hasSelection;
            private set => SetProperty(ref hasSelection, value);
        }

        /// <summary>
        /// 当前是否存在可读取的活动文件。
        /// </summary>
        public bool HasFile
        {
            get => hasFile;
            private set => SetProperty(ref hasFile, value);
        }

        /// <summary>
        /// 环境检测或安装操作是否正在执行。
        /// </summary>
        public bool IsBusy
        {
            get => isBusy;
            private set => SetProperty(ref isBusy, value);
        }

        /// <summary>
        /// 是否允许安装 Node.js。
        /// </summary>
        public bool CanInstallNode
        {
            get => canInstallNode;
            private set => SetProperty(ref canInstallNode, value);
        }

        /// <summary>
        /// 是否允许安装 DSH。
        /// </summary>
        public bool CanInstallDsh
        {
            get => canInstallDsh;
            private set => SetProperty(ref canInstallDsh, value);
        }

        /// <summary>
        /// 当前解决方案、文件和选中文本的摘要。
        /// </summary>
        public string ContextSummary { get; private set; }

        /// <summary>
        /// 检查 Node.js、npx 和 DSH 环境的异步命令。
        /// </summary>
        public IAsyncRelayCommand CheckEnvironmentCommand { get; }

        /// <summary>
        /// 下载安装 Node.js 的异步命令。
        /// </summary>
        public IAsyncRelayCommand InstallNodeCommand { get; }

        /// <summary>
        /// 通过 npx 安装 DSH 的异步命令。
        /// </summary>
        public IAsyncRelayCommand InstallDshCommand { get; }

        /// <summary>
        /// 提交当前任务并关闭对话框的命令。
        /// </summary>
        public IRelayCommand RunCommand { get; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化供 XAML 创建的任务对话框 ViewModel。
        /// </summary>
        public PromptDialogViewModel()
        {
            CheckEnvironmentCommand = new AsyncRelayCommand(RefreshEnvironmentAsync);
            InstallNodeCommand = new AsyncRelayCommand(InstallNodeAsync);
            InstallDshCommand = new AsyncRelayCommand(InstallDshAsync);
            RunCommand = new RelayCommand<object>(Run);
        }

        /// <summary>
        /// 初始化任务对话框 ViewModel，并根据当前 IDE 上下文设置可选项状态。
        /// </summary>
        /// <param name="context">Visual Studio 当前解决方案、文件和选中文本上下文。</param>
        /// <param name="forceSelection">是否强制勾选选中文本选项。</param>
        public PromptDialogViewModel(DSHAskContext context, bool forceSelection) : this()
        {
            InitializeContext(context, forceSelection);
        }

        /// <summary>
        /// 设置 Visual Studio 当前上下文并更新对话框状态。
        /// </summary>
        /// <param name="context">Visual Studio 当前解决方案、文件和选中文本上下文。</param>
        /// <param name="forceSelection">是否强制勾选选中文本选项。</param>
        public void InitializeContext(DSHAskContext context, bool forceSelection)
        {
            this.context = context;
            HasSelection = !string.IsNullOrEmpty(context?.SelectionText);
            HasFile = context?.FilePath != null && File.Exists(context.FilePath);
            IncludeSelection = HasSelection || forceSelection;
            IncludeFile = HasFile;
            ContextSummary = BuildSummary(context) + (HasSelection ? "" : "\n（当前没有选中文本）");
            OnPropertyChanged(nameof(ContextSummary));
        }

        #endregion

        #region 命令处理

        /// <summary>
        /// 下载并静默安装 Node.js，完成后刷新环境状态。
        /// </summary>
        private async Task InstallNodeAsync()
        {
            IsBusy = true;
            EnvironmentStatus = "正在从 Node.js 官方源下载安装包，请稍候…";
            try
            {
                var error = await DshLocator.InstallNodeJsAsync();
                await RefreshEnvironmentAsync();
                if (!string.IsNullOrEmpty(error))
                {
                    EnvironmentStatus += "\n" + error;
                }
            }
            finally { IsBusy = false; }
        }

        /// <summary>
        /// 通过 npx 下载并安装 DSH，完成后刷新环境状态。
        /// </summary>
        private async Task InstallDshAsync()
        {
            IsBusy = true;
            EnvironmentStatus = "正在通过 npx 安装 @deepseek-ai/dsh，请稍候…";
            try
            {
                var error = await DshLocator.InstallDshAsync();
                await RefreshEnvironmentAsync();
                if (!string.IsNullOrEmpty(error))
                {
                    EnvironmentStatus += "\n" + error;
                }
            }
            finally { IsBusy = false; }
        }

        /// <summary>
        /// 校验任务输入并设置对话框结果为成功。
        /// </summary>
        /// <param name="parameter">由视图传入的对话框窗口实例。</param>
        private void Run(object parameter)
        {
            if (string.IsNullOrWhiteSpace(Prompt))
            {
                return;
            }
            var window = parameter as Window;
            if (window != null)
            {
                window.DialogResult = true;
            }
        }

        #endregion

        #region 任务文本构造

        /// <summary>
        /// 构造包含用户任务和 IDE 上下文的完整 DSH 任务文本。
        /// </summary>
        /// <returns>可直接传递给 DSH CLI 的任务文本。</returns>
        public string BuildTaskText()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Prompt?.Trim());
            sb.AppendLine();
            sb.AppendLine("## 上下文（由 Visual Studio 扩展自动注入）");

            if (!string.IsNullOrEmpty(context?.WorkspaceRoot))
            {
                sb.AppendLine("- 工作目录/workspace: " + context.WorkspaceRoot);
            }

            if (!string.IsNullOrEmpty(context?.SolutionPath))
            {
                sb.AppendLine("- 解决方案: " + context.SolutionPath);
            }

            if (!string.IsNullOrEmpty(context?.ProjectPath))
            {
                sb.AppendLine("- 项目: " + context.ProjectPath);
            }

            if (!string.IsNullOrEmpty(context?.FilePath))
            {
                sb.AppendLine("- 活动文件: " + context.FilePath);
            }
            if (IncludeSelection && !string.IsNullOrEmpty(context?.SelectionText))
            {
                sb.AppendLine();
                sb.AppendLine("### 选中文本");
                sb.AppendLine(context.SelectionText);
            }
            if (IncludeFile && HasFile)
            {
                var content = File.ReadAllText(context.FilePath);
                if (content.Length > MaxFileChars)
                {
                    content = content.Substring(0, MaxFileChars) + "\n…(内容过长已截断)";
                }

                sb.AppendLine();
                sb.AppendLine("### 文件内容 " + context.FilePath);
                sb.AppendLine(content);
            }
            return sb.ToString();
        }

        #endregion

        #region 环境检测

        /// <summary>
        /// 检测 Node.js、npx 和 DSH，并更新按钮可用状态。
        /// </summary>
        private async Task RefreshEnvironmentAsync()
        {
            IsBusy = true;
            try
            {
                EnvironmentStatus = "正在检查 Node.js、npx 和 DSH 环境…";
                var status = await DshLocator.CheckEnvironmentAsync();
                EnvironmentStatus = (status.HasNode ? "Node.js " + status.NodeVersion : "未找到 Node.js") + "；"
                    + (status.HasNpx ? "npx " + status.NpxVersion : "未找到 npx") + "；"
                    + (status.HasDsh ? "已找到 DSH" : "未找到 DSH");
                CanInstallNode = !status.HasNode;
                CanInstallDsh = status.HasNode && status.HasNpx && !status.HasDsh;
            }
            finally { IsBusy = false; }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 根据 IDE 上下文生成用于界面展示的摘要。
        /// </summary>
        /// <param name="context">Visual Studio 当前上下文。</param>
        /// <returns>格式化后的上下文摘要。</returns>
        private static string BuildSummary(DSHAskContext context)
        {
            if (context == null)
            {
                return "";
            }
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(context.SolutionPath))
            {
                sb.AppendLine("解决方案: " + context.SolutionPath);
            }

            if (!string.IsNullOrEmpty(context.FilePath))
            {
                sb.AppendLine("文件: " + context.FilePath);
            }
            if (!string.IsNullOrEmpty(context.SelectionText))
            {
                var selection = context.SelectionText;
                if (selection.Length > 60)
                {
                    selection = selection.Substring(0, 60) + "…";
                }
                sb.AppendLine("选中: " + selection.Replace("\r", " ").Replace("\n", " "));
            }
            return sb.ToString().TrimEnd();
        }

        #endregion
    }
}