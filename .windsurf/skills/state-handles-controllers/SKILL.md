---
name: state-handles-controllers
description: Session/Flow 业务代码重构准则：State(纯数据)/Handles(资源)/Controllers(行为) 分离与落地步骤（含中文注释规范）。
---

# state-handles-controllers skill (index)

本 skill 用于把“会话/流程类代码”重构为：

- `State`：纯数据
- `Handles`：可释放资源/引用
- `Controllers`：行为逻辑
- `SubFeatures`：薄胶水

并补齐必要的中文注释（文件头说明 + 行内注释中文）。

## Sections

- [when_to_use.md](when_to_use.md)
- [required_context.md](required_context.md)
- [output_expectations.md](output_expectations.md)
- [invariants.md](invariants.md)
- [key_files.md](key_files.md)
- [procedure.md](procedure.md)
- [examples_and_troubleshooting.md](examples_and_troubleshooting.md)
