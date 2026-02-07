---
name: state-handles-controllers
section: invariants
---

# Invariants (must hold)

- **State 必须保持纯数据**：不持有可释放资源/Unity 对象/线程调度器等。
- **Handles 必须可兜底释放**：异常路径下也能 `Reset()` 清干净，内部记录异常。
- **控制权单向**：Controller/SubFeature 可以读写 State/Handles，但 State 不依赖行为。
- **生命周期统一**：Start/Stop 的资源创建/销毁顺序必须稳定且可追踪。
- **注释语言**：新增注释使用中文；新增业务文件补文件头中文说明。
