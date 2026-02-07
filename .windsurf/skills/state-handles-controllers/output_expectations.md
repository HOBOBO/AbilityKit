---
name: state-handles-controllers
section: output_expectations
---

# Output expectations

完成一次小批次重构后，期望看到：

- 结构：
  - 新增/调整 `State`/`Handles`/`Controllers`/`SubFeatures` 的文件划分清晰
  - 每个文件职责单一，命名能反映职责域
- 行为：
  - 行为不变（同样的 hook 时机、同样的资源释放顺序、同样的异常处理策略）
- 边界：
  - SubFeature 不直接访问 feature 内部字段，走窄 wrapper/host port
  - State 中不出现 IDisposable/UnityObject/dispatcher/CTS/Task 等资源
- 质量：
  - 新增业务代码文件带中文文件头注释
  - `dotnet build` 通过
