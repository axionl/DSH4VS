# 03.01-command-context: 迁移菜单命令与编辑器上下文

## Objective
使用 VisualStudio.Extensibility Command/CommandSet 和客户端上下文 API 替换 VSCT、OleMenuCommandService、AsyncPackage 与 EnvDTE 依赖。保留 Ask DSH、选区命令和 Cancel 命令。

## Affected files
- Commands/DSHCommands.cs
- Core/DSHAskContext.cs
- DshExtension.cs
- VSCommandTable.vsct（迁移完成后移除）

## Done when
新命令可以编译并由 VisualStudio.Extensibility contribution 模型发现；不再引用 AsyncPackage、OleMenuCommandService、VSCT 或 EnvDTE；当前文件和选中文本上下文仍可传递给 DSH。

## Research findings
- 当前实现文件为 `src/DSH4VS/Commands/ModernDshCommands.cs`，命令基类继承 `Command`；17.14 API 的抽象执行签名是 `ExecuteCommandAsync(IClientContext, CancellationToken)`，不是旧版字典上下文签名。
- `src/DSH4VS/DSH4VS.csproj` 已启用 SDK 默认 Compile 项，显式包含 `Commands/ModernDshCommands.cs` 会产生 `NETSDK1022`，因此必须移除重复 Include。
- 旧 `DSHCommands.cs`、`DSHAskContext.cs`、`VSCommandTable.vsct` 和 `VSPackage` 源文件已不在项目源文件清单中；当前命令实现保留了 VisualStudioContribution/CommandConfiguration 入口，后续运行器任务负责接入实际 DSH 业务。
