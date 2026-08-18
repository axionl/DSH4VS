# Copilot Instructions

## 项目指南
- C# 代码风格：私有字段不使用下划线前缀；按功能使用中文 #region 组织代码，并为方法和属性添加中文 XML 注释。
- 当当前文件存在时，PromptDialog 应自动勾选 IncludeFile（包含当前文件）选项。
- 生成 .vsix 文件作为 VisualStudio.Extensibility 进程外扩展的官方安装载体时，必须完全使用新模型，不得保留传统 VSIX/VSSDK 逻辑。
- 将扩展目标框架改为 net8.0-windows，以匹配当前 VisualStudio.Extensibility 官方包支持并消除 VSEXT0009。