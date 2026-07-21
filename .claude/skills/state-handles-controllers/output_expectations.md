# Output expectations

完成一次小批次重构后，期望看到：

## 结构

- 新增/调整 `State / Handles / Controllers / SubFeatures` 的文件划分清晰
- 每个文件职责单一，命名能反映职责域
- 大类按真实业务域 partial 拆分（生命周期阶段 / Sim 变体 / dispose helpers / accessors / host 契约 / 领域子目录）

## 行为

- 行为不变（同样的 hook 时机、同样的资源释放顺序、同样的异常处理策略）
- Controller 无状态，状态读写只走 `state.*` / `handles.*`
- Controller 通过 host 接口回调 Feature，不直接持有 Feature 引用

## 边界

- SubFeature 不直接访问 Feature 内部字段，走 `FeatureModuleContext<T>` + Runtime 契约
- State 中不出现 IDisposable / UnityObject / dispatcher / CTS / Task 等资源
- Handles 自身按领域拆 partial，`Reset()` 覆盖全部新增资源

## Entitas ECS 层（如有涉及）

- 反注册放在 `OnTearDown()`，**不放 `OnCleanup()`**
- `OnCleanup` 基本不用（响应式基类注释明说"空实现"）

## 质量

- 新增业务代码文件带中文文件头注释
- `dotnet build` 通过
- Unity CI 通过
