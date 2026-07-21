# Procedure (recommended workflow)

1. **识别字段归属（先做清单）**
   - 纯数据 → `State`（必要时拆嵌套 POCO 子状态）
   - 可释放资源/引用 → `Handles`（必要时按领域拆 partial）
   - 行为逻辑 → `Controllers`（无状态，签名 `(state, handles, host)`）

2. **先收口访问边界**
   - 对外提供窄 wrapper（accessors）或 host port
   - 让调用点不再直接访问 Feature 的内部字段
   - 参考 `BattleSessionFeature.Accessors.cs` / `.PhaseAccessors.cs` / `.StateAccessors.cs` / `.SnapshotAccessors.cs` / `.NetworkAccessors.cs`

3. **设计 Host 接口与 Runtime 契约**
   - 定义 `IXxxHost` 接口（Controller 回调 Feature 用）
   - 定义 `IXxxRuntime` 接口（SubFeature 反向访问 Feature 用）
   - Feature 用显式接口实现暴露（参考 `HostBridges.cs` / `Runtime.cs`）

4. **小批次迁移**
   - 一次只迁移一个职责域（例如 replay debug、sim tick remote-driven、dispose view 等）
   - 迁移后立刻修编译（using / partial / 命名空间）
   - 必要时新建 partial 文件（`YourClass.YourDomain.cs`）

5. **补齐中文注释（仅新增内容）**
   - 新增业务文件：文件头中文说明
   - 新增注释：中文
   - 非公共/显而易见代码可不写

6. **验证**
   - 每批次 `dotnet build`（Console Demo + 相关项目）
   - Unity CI 通过
   - 若涉及运行时序：补一条最小可观测日志或断言（避免 silent fail）

7. **回归检查**
   - State 是否混入资源（IDisposable / UnityObject / CTS / Task）
   - Handles.Reset/Dispose 是否覆盖新增资源
   - Controller 是否真的无状态（无跨 tick 私有字段）
   - SubFeature 是否越权访问 Feature 内部（应走 Runtime 契约）
   - Entitas ECS 系统的反注册是否在 `OnTearDown` 而非 `OnCleanup`
