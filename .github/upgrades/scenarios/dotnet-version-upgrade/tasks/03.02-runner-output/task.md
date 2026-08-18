# 03.02-runner-output: 迁移 DSH 业务运行与输出服务

## Objective
保留 DSH CLI 进程启动、取消、Web profile 启动和 HTTP 探测逻辑，移除 OutputPane 对 IVsOutputWindowPane、Package.GetGlobalService 和状态栏 VSSDK API 的依赖，改用 VisualStudio.Extensibility 服务或可测试的抽象。

## Affected files
- Core/DshRunner.cs
- Core/OutputPane.cs
- Core/DshLocator.cs

## Done when
业务代码在 net8.0-windows 下编译；不再引用 Microsoft.VisualStudio.Shell 或 Shell.Interop；取消和输出行为保留。
