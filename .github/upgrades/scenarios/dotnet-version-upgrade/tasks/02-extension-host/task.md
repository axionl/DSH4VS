# 02-extension-host: 重写 VisualStudio.Extensibility 扩展宿主与项目配置

将项目目标框架改为适用于 Windows/WPF 的 `net10.0-windows`，替换传统 `Microsoft.VisualStudio.SDK`、`Microsoft.VSSDK.BuildTools`、VSIX 打包和实验实例启动配置，加入 VisualStudio.Extensibility 进程外扩展所需的项目属性、包引用和扩展清单。新增现代扩展入口，迁移扩展生命周期和服务依赖，确保新宿主可以独立启动。

## 研究结果

- 目标项目为单项目 SDK-style WPF 工程，不能使用 VSSDK SDK-style conversion 作为最终方案，因为用户要求移除 VSIX，而不是仅转换 VSIX 项目格式。
- `Microsoft.VisualStudio.Extensibility` 17.14.2098 提供 `net8.0-windows8.0`、`netstandard2.0` 和 `net472` 资产，可被 `net10.0-windows` 项目引用。
- `Microsoft.VisualStudio.Extensibility.Build` 和 `Microsoft.VisualStudio.Extensibility.Sdk` 可用版本为 17.14.40608；后续需要根据其构建 targets 验证扩展部署产物格式。
- 当前旧宿主和命令依赖 `Microsoft.VisualStudio.Shell`、`Microsoft.VisualStudio.Shell.Interop`、`AsyncPackage`、`OleMenuCommandService`，这些不能继续作为新进程外宿主的入口。
- 当前 WPF/XAML 资产需要保留 Windows Desktop 支持，项目目标应使用 `net10.0-windows`，而不是无 Windows 标识的 `net10.0`。
- 目标框架选择为 `net8.0-windows`，因为当前 VisualStudio.Extensibility 官方扩展宿主包基于 .NET 8；此选择可避免 net10 兼容性警告并保持官方支持。

## 受影响文件

- `src/DSH4VS/DSH4VS.csproj`
- 新增现代扩展入口文件（计划使用 VisualStudio.Extensibility contribution 模型）
- 后续由 03-feature-migration 处理：`DSH4VSPackage.cs`、`Commands/DSHCommands.cs`、`UI/DSHWebWindow.cs` 和 VSCT 注册

## 执行约束

先完成项目包和目标框架迁移，并以最小的新宿主入口验证包还原和构建；旧功能代码的 API 替换放到后续任务，避免在同一任务中混合宿主配置和全部功能迁移。官方 VisualStudio.Extensibility 构建流程可以生成 `.vsix` 安装载体，但其中只能包含新的进程外扩展逻辑。

**Done when**: 项目不再依赖传统 `AsyncPackage` 入口、VSCT 命令注册或 VSSDK 扩展逻辑；VisualStudio.Extensibility 宿主可编译并被 Visual Studio 识别；项目生成官方新的扩展部署产物（允许为 `.vsix`）。
