---
name: ability-kit
section: output-expectations
---

# Output expectations

本 skill 的输出应包含：

- **涉及的关键文件与入口点**（给出相对路径）
- **数据流/调用链说明**（Publish -> Subscribe -> Handler -> TriggerRunner.RunOnce）
- **高频路径性能约束**（避免 GC、优先池化/struct）
- **对工程既有约束的遵守**（Cleanup/TearDown、EventBus 一致性等）
