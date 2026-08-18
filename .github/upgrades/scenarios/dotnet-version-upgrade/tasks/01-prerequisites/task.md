# 01-prerequisites: 验证 .NET 10 与 VisualStudio.Extensibility 工具链

确认本机安装的 .NET 10 SDK、Visual Studio 扩展开发组件和可用的 VisualStudio.Extensibility NuGet 包版本；记录当前项目结构、命令、工具窗口和 UI 依赖，为重写提供基线。

## 研究结果

- 解决方案仅包含 `src/DSH4VS/DSH4VS.csproj`，项目已经是 SDK-style，但当前目标为 `net472`。
- 本机已安装 .NET SDK `10.0.400`，MSBuild `18.9.6`；可使用 `net10.0`/`net10.0-windows`。
- NuGet 稳定包 `Microsoft.VisualStudio.Extensibility` 当前可用最高版本为 `17.14.2098`；未发现 `18.x` 稳定版本，因此迁移先锁定该稳定包，并在还原/编译时验证其对当前 VS Insiders 的兼容性。
- 当前入口为 `DSH4VSPackage : AsyncPackage`（`src/DSH4VS/DSH4VSPackage.cs`），命令通过 `OleMenuCommandService` 注册（`src/DSH4VS/Commands/DSHCommands.cs`）。
- 当前工具窗口为 `ToolWindowPane`（`src/DSH4VS/UI/DSHWebWindow.cs`），内容为 WebView2/WPF 控件；PromptDialog 使用 WPF + CommunityToolkit.Mvvm。
- 当前项目仍包含 `source.extension.vsixmanifest`、`VSCommandTable.vsct`、`Microsoft.VisualStudio.SDK` 和 `Microsoft.VSSDK.BuildTools`，这些属于待移除的传统 VSIX/VSSDK 资产。
- WPF/XAML 项目后续验证应使用 Visual Studio `msbuild.exe`，而不是仅使用 `dotnet build`，因为 WPF 标记编译需要完整 VS targets。

## Scope Inventory

- **项目**：`src/DSH4VS/DSH4VS.csproj`
- **宿主重写**：`DSH4VSPackage.cs`、项目包引用、扩展元数据
- **命令迁移**：`Commands/DSHCommands.cs`、`VSCommandTable.vsct`
- **工具窗口迁移**：`UI/DSHWebWindow.cs`、`UI/DSHWebWindowControl.xaml(.cs)`
- **对话框保留**：`UI/PromptDialog*`，已确认 `InitializeContext` 在存在当前文件时设置 `IncludeFile = HasFile`
- **验证**：MSBuild restore/build；当前未发现独立测试项目

**Done when**: .NET 10 SDK 可用；VisualStudio.Extensibility 包可还原；现有入口、命令、工具窗口和 UI 文件清单记录在任务文档中。
