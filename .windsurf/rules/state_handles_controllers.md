---
trigger: manual
---

# Session / Flow 业务代码规范：数据与处理行为分离（State / Handles / Controllers）+ 中文注释

## 0. 适用范围

- 适用于 `Unity/Packages/com.abilitykit.demo.*` 下的 **业务代码**（尤其是 Battle Flow / Session / View Runtime 相关）。
- 适用于新写代码与重构代码（拆分文件、迁移字段、抽 controller/sub-feature 等）。

## 1. 核心原则（强约束）

### 1.1 数据与行为分离

- **State（纯数据）**
  - 只存储状态（数值、标记、配置快照、统计等）。
  - 不直接持有 Unity 对象、线程/调度器、网络连接、CTS、Task、IDisposable 等资源。
  - 允许存储可序列化/可复制的数据结构。

- **Handles（资源与引用）**
  - 存储运行期资源/引用（session/runtime/world、dispatcher、net adapter、snapshot routing、cts/task 等）。
  - 需要提供异常安全的兜底释放（例如 `Reset()`/`Dispose()`，内部 try/catch 记录异常）。
  - 不承载业务状态机与规则判断（避免把逻辑塞进 handles）。

- **Controllers（行为）**
  - 承载可测试的行为逻辑单元（plan、tick loop、replay、net adapter、events/dispatchers 等）。
  - Controller 的依赖通过 **host ports（小接口）** 或显式参数注入。
  - Controller 不得直接访问 Feature 内部的“杂散字段”；若需要访问资源/状态，优先通过窄 wrapper（accessors）或 host port。

- **SubFeatures（薄胶水）**
  - 负责把 pipeline/hook 时机与 controller/feature wrapper 连接起来。
  - 避免在 sub-feature 内实现大段业务逻辑（除非它本身就是一个独立系统）。

### 1.2 依赖方向

- 行为逻辑（Controllers/SubFeatures）可以依赖 State/Handles（读写/调用），但 **State 不依赖行为**。
- 资源释放/异常兜底尽量收敛到 Handles 或明确的 DisposeHelpers，避免分散。

## 2. 代码组织建议（推荐但可权衡）

- 大类（如 `BattleSessionFeature`）应保持为 **组合根/编排器**：
  - 管生命周期边界（Attach/Detach/Start/Stop）
  - 装配 controllers/sub-features
  - 暴露窄 wrapper 与 host ports

- 单文件职责过重时，优先拆分为 partial（一个文件一个职责域）：
  - `*.SimTick.RemoteDriven.cs` / `*.SimTick.Confirmed.cs`
  - `*.DisposeHelpers.View.cs` / `*.DisposeHelpers.Worlds.cs` 等

## 3. 中文注释规范（强约束）

### 3.1 文件头注释（必需）

- 对业务代码文件（尤其是 Runtime 下），只要在一次改动中 **有实质性修改**（新增/移动/重构/逻辑调整），就必须在文件头添加或补齐中文注释，说明：
  - 该文件的职责
  - 关键边界（State/Handles/Controllers/SubFeatures/HostPorts 之一）
  - 主要调用时机（Attach/Detach/PreTick/MainTick/OnFrame 等）

> 文件头注释应简短（建议 3~8 行），避免写成设计文档。

### 3.2 行内/块注释语言

- 新增的注释统一使用 **中文**。
- 允许出现必要的英文专有名词（类型名/协议名/字段名），但说明文字保持中文。

### 3.3 例外（避免噪音）

以下情况可以不强制补文件头注释或不改注释语言：

- 第三方/自动生成代码
- 纯 DTO/纯配置结构（字段名已足够清晰且无复杂语义）
- 仅做机械迁移且该文件短期会被删除/替换（需在评审中明确，且不应长期保留）

## 4. 重构时的执行顺序（建议流程）

1. 先明确目标：要把什么字段迁移到 State/Handles，什么逻辑下沉到 Controller。
2. 先加窄 wrapper/host port（保证调用点不直访内部字段）。
3. 再做搬迁与拆分（每次小批量）。
4. 每批次后执行 `dotnet build`（或 Unity CI 对应构建）验证。
