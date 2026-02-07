---
name: state-handles-controllers
section: procedure
---

# Procedure (recommended workflow)

1. **识别字段归属（先做清单）**
   - 纯数据 → `State`
   - 可释放资源/引用 → `Handles`
   - 行为逻辑 → `Controllers`

2. **先收口访问边界**
   - 对外提供窄 wrapper（accessors）或 host port
   - 让调用点不再直接访问 feature 的内部字段

3. **小批次迁移**
   - 一次只迁移一个职责域（例如 replay debug、sim tick remote-driven、dispose view 等）
   - 迁移后立刻修编译（using/partial/命名空间）

4. **补齐中文注释（仅新增内容）**
   - 新增业务文件：文件头中文说明
   - 新增注释：中文

5. **验证**
   - 每批次 `dotnet build`
   - 若涉及运行时序：补一条最小可观测日志或断言（避免 silent fail）

6. **回归检查**
   - State 是否混入资源
   - Handles.Reset/Dispose 是否覆盖新增资源
   - SubFeature 是否越权访问 feature 内部
