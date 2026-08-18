# Upgrade Options

## Upgrade Strategy

| Option | Description |
|---|---|
| **All-At-Once (selected)** | 单个项目整体迁移，统一完成目标框架升级和 VisualStudio.Extensibility 重写。 |
| Bottom-Up | 按依赖层逐步迁移；当前解决方案只有一个项目，不适用。 |
| Top-Down | 从应用入口向依赖项迁移；当前解决方案只有一个项目，不适用。 |

## Project Approach

| Option | Description |
|---|---|
| **In-place rewrite (selected)** | 直接将现有扩展项目改造为新的进程外扩展，不保留旧 VSIX 项目。 |
| Side-by-side | 保留旧扩展并新增新项目；与用户明确的“不再使用现有 VSIX”要求冲突。 |

## Target Framework

| Option | Description |
|---|---|
| **net8.0-windows (selected)** | VisualStudio.Extensibility 当前官方扩展宿主支持的 .NET 8 Windows 目标框架。 |
| net10.0 | .NET 10 LTS，但当前扩展宿主包会产生兼容性警告。 |

## Test Coverage

| Option | Description |
|---|---|
| **Skip (selected)** | 当前解决方案未发现独立测试项目；执行现有构建和可执行验证。 |
| Generate | 生成测试基线；不作为本次迁移前置工作。 |
