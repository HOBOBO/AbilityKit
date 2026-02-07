---
name: state-handles-controllers
section: examples_and_troubleshooting
---

# Examples & troubleshooting

## 常见拆分点示例

- 把 `#if UNITY_EDITOR` 的 debug/seek 从主逻辑 controller 拆到 `*.Debug.cs`。
- 把 `SimTick` 拆为 `RemoteDriven` / `Confirmed` 两个 partial 文件。
- 把 dispose helpers 按领域拆：worlds/view/dispatchers。

## 常见问题

- **编译错误：找不到方法/类**
  - 检查是否忘了把类声明改为 `partial`
  - 检查新文件 namespace 与外层嵌套类型是否一致

- **行为回归：Start/Stop 顺序改变**
  - 检查是否在迁移时调整了 finally 清理顺序
  - Handles.Reset 是否遗漏了新引入的资源

- **注释噪音过大**
  - 只对新增业务文件要求文件头注释；机械迁移不强制到处补注释
