# 03.03-toolwindow-wpf 执行记录

## 已完成

- 新增 `UI/DshWebToolWindow.cs`：`[VisualStudioContribution]` 的 `ToolWindow` 派生类，标题 "DSH"，`Placement = ToolWindowPlacement.DocumentWell`，`GetContentAsync` 返回远程用户控件。
- 新增 `UI/DshWebWindowControl.cs`：`RemoteUserControl` 派生类，构造时传入 `VisualStudioExtensibility` 并创建 `DshWebWindowViewModel`；重写 `ControlLoadedAsync` 在 VS 进程内控件加载完成后探测 web 状态。
- 重写 `UI/DshWebWindowControl.xaml`：从 `UserControl` 改为远程 UI `DataTemplate`（EmbeddedResource + Page Remove），保留 WebView2（`Source` 绑定 + 可见性绑定）与启动覆盖层；移除仅在扩展进程可用的 HandyControl/MahApps 资源。
- 重写 `UI/DshWebWindowViewModel.cs`：改为 `NotifyPropertyChangedObject` + `[DataContract]`/`[DataMember]` 的 Remote UI 数据上下文；私有字段去下划线前缀；`StartWebCommand` 使用 `IAsyncCommand`；保留启动 web 按钮与浏览器导航逻辑。
- 删除旧 `UI/DSHWebWindow.cs`（ToolWindowPane）与 `UI/DSHWebWindowControl.xaml.cs`（代码后置）。
- `Commands/ModernDshCommands.cs` 新增 `OpenDshWebCommand`：`Shell().ShowToolWindowAsync<DshWebToolWindow>`，并放置到 View>Other Windows 与 Tools 菜单。
- 更新 `DSH4VS.csproj`：纳入全部 UI 文件，`DshWebWindowControl.xaml` 作为内嵌资源（`<EmbeddedResource>` + `<Page Remove>`），确保 `DSH4VS.UI.DshWebWindowControl.xaml` 被 `RemoteUserControl` 自动检索。
- 验证 `PromptDialogViewModel.InitializeContext`：`IncludeFile = HasFile`（当前文件存在且可读时自动勾选）行为保持。

## 验证

`dotnet build src/DSH4VS/DSH4VS.csproj -c Debug -p:DeployExtension=false -t:Rebuild`

结果：成功，0 错误 0 警告；`extension.json` 注册了 `DSH4VS.UI.DshWebToolWindow`（DocumentWell）与全部 4 个命令；程序集内嵌资源含 `DSH4VS.UI.DshWebWindowControl.xaml`。

## 无法自动验证的 IDE 运行时检查项

- WebView2 需在 VS 进程内解析 `Microsoft.Web.WebView2.Wpf` 程序集，且 `Source` 的字符串→Uri 绑定转换依赖 Remote UI 绑定引擎；需在 Visual Studio 实验实例中打开 "Open DSH Web UI" 确认浏览器可导航到 `http://127.0.0.1:3080`。
- 工具窗口 `ToolWindowPlacement`、对话框 `PromptDialog` 在扩展进程中的显示与 STA 线程上下文需在 IDE 中人工验证。
