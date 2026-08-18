using System.Windows;
using DSH4VS.Core;

namespace DSH4VS.UI
{
    /// <summary>
    /// DSH 任务输入对话框，负责承载任务文本和上下文选项。
    /// </summary>
    public partial class PromptDialog : Window
    {
        private readonly PromptDialogViewModel viewmodel;

        #region 构造函数

        /// <summary>初始化任务对话框并加载当前 IDE 上下文。</summary>
        /// <param name="context">Visual Studio 当前解决方案、文件和选中文本上下文。</param>
        /// <param name="forceSelection">是否强制勾选选中文本选项。</param>
        public PromptDialog(DSHAskContext context, bool forceSelection = false)
        {
            InitializeComponent();
            viewmodel = (PromptDialogViewModel)DataContext;
            viewmodel.InitializeContext(context, forceSelection);
        }

        #endregion

        #region 公共方法

        /// <summary>构造包含任务说明和上下文的 DSH 任务文本。</summary>
        /// <returns>可传递给 DSH CLI 的任务文本。</returns>
        public string BuildTaskText() => viewmodel.BuildTaskText();

        #endregion
    }
}