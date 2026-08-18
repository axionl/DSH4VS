# 03-feature-migration: 迁移命令、工具窗口和 WPF 交互功能

将 VSCT/菜单注册、`DSHCommands`、编辑器上下文菜单、`DSHWebWindow` 以及 PromptDialog 的交互逻辑映射到 VisualStudio.Extensibility 命令、文档/编辑器上下文和工具窗口 API。保留 DSH headless 调用、WebView2 内容和现有 WPF 资源；确保当前文件存在时 PromptDialog 自动勾选 IncludeFile。

## 研究结果

- 旧命令入口位于 `Commands/DSHCommands.cs`，依赖 `AsyncPackage`、`IMenuCommandService`、`OleMenuCommand`、`DTE` 上下文和 `ToolWindowPane`。
- 旧命令包括 Ask DSH、Ask DSH about selection、Open DSH Web UI 和 Cancel DSH Task；菜单结构和编辑器上下文菜单目前完全由 `VSCommandTable.vsct` 提供。
- `Core/DSHAskContext.cs` 通过 EnvDTE 捕获解决方案、项目、活动文件和选中文本；必须替换为 VisualStudio.Extensibility client context/document API。
- `Core/DshRunner.cs` 混合了 DSH 进程管理、HTTP Web UI 启动、OutputPane 写入和 VS 状态栏更新；进程管理可保留，VS 输出/状态栏适配必须重写。
- `Core/OutputPane.cs` 依赖 `IVsOutputWindowPane` 和 `Package.GetGlobalService`，不能进入最终实现。
- `UI/DSHWebWindow.cs` 继承 `ToolWindowPane`；`DSHWebWindowControl.xaml` 使用 WebView2 和 `DshRunner.WebUrl`，需要迁移到新工具窗口内容模型。
- `PromptDialogViewModel.InitializeContext` 已满足项目约束：当 `context.FilePath` 存在且文件可读时设置 `IncludeFile = HasFile`；迁移时必须保留该行为。

## Scope Inventory

- **命令与菜单**：VisualStudio.Extensibility Command/CommandSet，替换 VSCT 和 VSSDK 注册。
- **编辑器上下文**：使用新模型的 `IClientContext`/文档快照替换 EnvDTE。
- **工具窗口**：迁移 DSH Web UI 工具窗口和 WebView2 生命周期。
- **业务代码**：保留 DshLocator、DshRunner 的 CLI 进程管理逻辑，拆出新的输出/状态服务。
- **WPF**：保留 PromptDialog 和 DSHWebWindowControl，修正私有字段命名以符合项目规则。

该任务包含多个独立技术边界和验证点，按命令、上下文/业务、工具窗口三个子任务执行。

**Done when**: 旧 VSCT 和 `[ProvideMenuResource]`/`[ProvideToolWindow]` 注册不再被使用；菜单命令、编辑器上下文命令和工具窗口均由新 API 提供；PromptDialog 的 IncludeFile 规则和现有 DSH 操作逻辑保留。
