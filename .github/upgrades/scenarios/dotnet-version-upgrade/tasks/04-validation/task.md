# 04-validation: 清理旧 VSIX 资产并完成全量验证

删除传统 VSIX manifest、VSCT、旧 Package 类和不再需要的 VSSDK 依赖，清理遗留目标与资源；执行 restore、完整 solution build、测试发现和扩展部署产物检查，修复所有错误和警告。

**Done when**: 解决方案构建成功且无警告；代码和项目中不存在传统 VSIX/VSSDK 入口引用；VisualStudio.Extensibility 部署产物存在；现有测试（如有）通过，并记录无法自动验证的 IDE 运行时检查项。
