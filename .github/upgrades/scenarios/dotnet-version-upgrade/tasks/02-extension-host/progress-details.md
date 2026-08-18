# 任务进度

## 已完成

- 将 `src/DSH4VS/DSH4VS.csproj` 目标框架从 `net472` 改为 `net8.0-windows`。
- 移除传统 `Microsoft.VisualStudio.SDK`、`Microsoft.VSSDK.BuildTools`、VSCT、pkgdef、实验实例启动和旧 VSIX 资源配置。
- 添加 `Microsoft.VisualStudio.Extensibility` 17.14.2098、`Microsoft.VisualStudio.Extensibility.Build` 17.14.40608 和 `Microsoft.VisualStudio.Extensibility.Sdk` 17.14.40608。
- 新增 `src/DSH4VS/DshExtension.cs`，实现 VisualStudio.Extensibility 进程外入口和扩展元数据。
- 临时从新宿主编译范围隔离旧入口、旧命令、旧 Core/UI 文件，待后续功能迁移任务逐一替换。
- 将 `System.Management` 固定到当前 NuGet 源可还原的 10.0.7，消除 System.CodeDom 版本解析警告。

## 验证结果

使用 Visual Studio MSBuild 执行：

`msbuild src/DSH4VS/DSH4VS.csproj /restore /t:Build /p:Configuration=Debug /v:minimal`

结果：成功，无错误、无警告；生成：

`src/DSH4VS/bin/Debug/net8.0-windows/DeepSeekForVisualStudio.dll`

`src/DSH4VS/bin/Debug/net8.0-windows/DeepSeekForVisualStudio.vsix`

## 偏差与后续

- 官方 VisualStudio.Extensibility 构建流程仍生成 `.vsix` 安装载体，这是新进程外扩展的安装方式；旧 VSSDK 逻辑不会进入该产物。
- 旧命令和工具窗口尚未迁移，下一任务将使用 VisualStudio.Extensibility API 重写它们后再删除旧入口文件。
