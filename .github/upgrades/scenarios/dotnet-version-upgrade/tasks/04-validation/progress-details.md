# 04-validation 执行记录

## 已完成

- 删除传统 VSIX/VSSDK 资产：`DSH4VSPackage.cs`、`Commands/DSHCommands.cs`、`PackageGuids.cs`、`PkgCmdID.cs`、`VSCommandTable.vsct`、`source.extension.vsixmanifest`、`extension.vsixmanifest`、`VSPackage.resx`、`DSH4VS.csproj.Backup.tmp`、项目根残留 `DeepSeekForVisualStudio.vsix` 与 `xaml-build.log`。
- 清理 `DSH4VS.csproj`：移除针对已删除文件的 `Compile/None Remove`；修正损坏的注释编码；保留 `EmbeddedResource`（`UI\DshWebWindowControl.xaml`）与 `Page Remove`；将 `Microsoft.Web.WebView2` 从浮动版本 `1.0.*` 固定为缓存中的 `1.0.4129.50`，避免 restore 依赖网络浮动解析。
- 全量验证：
  - `dotnet restore`（本机 nuget.org 不可达，使用本地镜像源 `http://172.20.109.18:7070/v3/index.json`）成功。
  - 干净重建（删除 obj/bin 后）`dotnet build DSH4VS.sln -c Debug -p:DeployExtension=false --no-restore`：**0 错误 0 警告**。
  - 生成 `DeepSeekForVisualStudio.dll` 与 `DeepSeekForVisualStudio.vsix`（≈16.9 MB，含 WebView2 等依赖）。
  - `.vsextension/extension.json` 注册全部 4 个命令（Ask DSH、Ask DSH about selection、Open DSH Web UI、Cancel DSH Task）与工具窗口 `DSH4VS.UI.DshWebToolWindow`（DocumentWell）；`.vsextension/string-resources.json` 已随 VSIX 发布。
  - `deps.json` 中无 `Microsoft.VisualStudio.Shell`、VSSDK 或 VSCT 相关依赖；剩余源码与 csproj 中不存在 `ToolWindowPane`/`ProvideToolWindow`/`ProvideMenuResource`/`AsyncPackage`/`OleMenuCommandService`/`IVsOutputWindowPane`/`EnvDTE` 引用。

## 测试

- 解决方案中无测试项目，未发现可执行测试。

## 无法自动验证的 IDE 运行时检查项

- 在 Visual Studio 实验实例中确认：Tools 菜单与编辑器上下文菜单显示 4 个命令；"Open DSH Web UI" 打开 DSH 工具窗口且 WebView2 可导航到 `http://127.0.0.1:3080`（Remote UI DataTemplate 对 `Microsoft.Web.WebView2.Wpf` 的解析依赖 VS 进程程序集）。
- PromptDialog 在扩展进程中的显示（`Application.Current?.MainWindow` 可能为 null）与 `CenterOwner` 定位。
- DSH headless 任务输出写入 VS 输出窗口 "DSH" 通道、取消行为（Cancel DSH Task）与 web profile 启动/HTTP 探测。
