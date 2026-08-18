# .NET 版本升级与 VisualStudio.Extensibility 重写计划

## Overview

**Target**: 将 `src/DSH4VS/DSH4VS.csproj` 从 `net472` 和传统 VSSDK/VSIX 实现迁移到 `net8.0-windows` 的 VisualStudio.Extensibility 进程外扩展。
**Scope**: 1 个 SDK-style WPF Visual Studio 扩展项目，约 13 个代码文件；保留现有菜单命令、编辑器上下文菜单、DSH Web UI 工具窗口和 PromptDialog 功能。

### Selected Strategy
**All-At-Once** — 单个项目整体升级和重写。
**Rationale**: 解决方案只有一个项目，且用户明确要求直接替换现有 VSIX 实现。

## Tasks

### 01-prerequisites: 验证 .NET 10 与 VisualStudio.Extensibility 工具链

确认本机安装的 .NET 10 SDK、Visual Studio 扩展开发组件和可用的 VisualStudio.Extensibility NuGet 包版本；记录当前项目结构、命令、工具窗口和 UI 依赖，为重写提供基线。

**Done when**: .NET 10 SDK 可用；VisualStudio.Extensibility 包可还原；现有入口、命令、工具窗口和 UI 文件清单记录在任务文档中。

---

### 02-extension-host: 重写 VisualStudio.Extensibility 扩展宿主与项目配置

将项目目标框架改为适用于 Windows/WPF 的 `net10.0-windows`，替换传统 `Microsoft.VisualStudio.SDK`、`Microsoft.VSSDK.BuildTools`、VSIX 打包和实验实例启动配置，加入 VisualStudio.Extensibility 进程外扩展所需的项目属性、包引用和扩展清单。新增现代扩展入口，迁移扩展生命周期和服务依赖，确保新宿主可以独立启动。

**Done when**: 项目不再依赖传统 VSIX 打包目标或 `AsyncPackage` 入口；VisualStudio.Extensibility 宿主可编译并被 Visual Studio 识别；项目生成新的扩展部署产物而非旧 `.vsix`。

---

### 03-feature-migration: 迁移命令、工具窗口和 WPF 交互功能

将 VSCT/菜单注册、`DSHCommands`、编辑器上下文菜单、`DSHWebWindow` 以及 PromptDialog 的交互逻辑映射到 VisualStudio.Extensibility 命令、文档/编辑器上下文和工具窗口 API。保留 DSH headless 调用、WebView2 内容和现有 WPF 资源；确保当前文件存在时 PromptDialog 自动勾选 IncludeFile。

**Done when**: 旧 VSCT 和 `[ProvideMenuResource]`/`[ProvideToolWindow]` 注册不再被使用；菜单命令、编辑器上下文命令和工具窗口均由新 API 提供；PromptDialog 的 IncludeFile 规则和现有 DSH 操作逻辑保留。

---

### 04-validation: 清理旧 VSIX 资产并完成全量验证

删除传统 VSIX manifest、VSCT、旧 Package 类和不再需要的 VSSDK 依赖，清理遗留目标与资源；执行 restore、完整 solution build、测试发现和扩展部署产物检查，修复所有错误和警告。

**Done when**: 解决方案构建成功且无警告；代码和项目中不存在传统 VSIX/VSSDK 入口引用；VisualStudio.Extensibility 部署产物存在；现有测试（如有）通过，并记录无法自动验证的 IDE 运行时检查项。
