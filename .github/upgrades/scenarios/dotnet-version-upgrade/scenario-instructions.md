# .NET 版本升级与 VisualStudio.Extensibility 重写

## Strategy
All-At-Once：单个项目整体迁移，直接替换传统 VSIX/VSSDK 架构。

## Upgrade Options
- **Target Framework**: net8.0-windows
- **Project Approach**: In-place rewrite
- **Upgrade Strategy**: All-At-Once
- **Test Coverage**: Skip（当前无独立测试项目）

## Preferences
- **Flow Mode**: Automatic
- **Target Framework**: net10.0
- **Commit Strategy**: Manual
- **Legacy VSIX**: 不保留传统 VSIX 实现
- **VisualStudio.Extensibility Architecture**: 实现架构完全采用 VisualStudio.Extensibility 进程外模型；允许官方构建流程生成 VSIX 安装载体，但不保留传统 VSIX 逻辑
- **功能范围**: 保留现有菜单命令、编辑器上下文菜单、DSH Web UI 工具窗口和现有 WPF/业务功能

## Decisions
- 使用 VisualStudio.Extensibility 进程外扩展模型替代 AsyncPackage、VSIX manifest 和 VSSDK 命令注册。
- 当前工作区不是 Git 仓库，不执行分支创建或自动提交。
- 项目已经是 SDK-style，因此不单独执行 SDK-style 转换任务。
- 允许生成 `.vsix` 作为官方 VisualStudio.Extensibility 扩展的安装载体；`.vsix` 内不得包含传统 VSSDK 扩展逻辑。

## Execution Constraints
- 传统 VSIX manifest、VSCT 命令表、AsyncPackage 和 VSSDK 注册必须从最终扩展实现中移除。
- 官方 VisualStudio.Extensibility 构建包生成的 VSIX 仅作为安装载体，不得包含传统 VSSDK 扩展逻辑。
- VisualStudio.Extensibility 新扩展入口、命令和工具窗口必须先编译通过，再删除旧入口文件。
- WPF 界面迁移必须保持现有 PromptDialog 的 IncludeFile 自动勾选逻辑。

## Custom Instructions
- C# 私有字段不使用下划线前缀。
- 按功能使用中文 #region 组织代码，并添加中文注释。
- 当当前文件存在时，PromptDialog 应自动勾选 IncludeFile（包含当前文件）选项。
