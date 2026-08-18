# 03.03-toolwindow-wpf: 迁移 DSH Web UI 工具窗口与 WPF 交互

## Objective
将 ToolWindowPane 替换为 VisualStudio.Extensibility 工具窗口贡献，保留 WebView2 控件、PromptDialog 和现有 WPF 业务界面；确保当前文件存在时 IncludeFile 自动勾选。

## Affected files
- UI/DSHWebWindow.cs
- UI/DSHWebWindowControl.xaml(.cs)
- UI/PromptDialog.xaml(.cs)
- UI/PromptDialogViewModel.cs

## Done when
工具窗口由新模型提供且可编译；不再引用 ToolWindowPane 或 ProvideToolWindow；WebView2 可导航到 DSH Web URL；PromptDialog IncludeFile 行为保持。
