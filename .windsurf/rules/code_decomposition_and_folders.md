---
trigger: manual
---

# 业务代码复杂度控制：拆分文件 + 按需分目录（避免单文件/单目录膨胀）

## 0. 目标

- 降低单文件与单目录的认知负担。
- 让“按职责定位代码”成为默认路径。
- 使重构可渐进进行（每次小批次拆分，持续 build 验证）。

## 1. 强约束：复杂代码必须拆分

只要满足以下任一条件，就必须考虑拆分（通常是强制）：

- 单文件职责明显不止一个（生命周期/资源释放/业务逻辑/调试逻辑混在一起）。
- 单文件过长或持续增长（例如出现大量区域化代码块、同一类方法重复模式明显）。
- 条件编译（`#if UNITY_EDITOR` 等）导致主逻辑可读性下降。
- 某个领域逻辑（net/replay/gateway/snapshot/sim/debug/editor）明显可形成独立单元。

拆分原则：

- **一文件一职责域**：每个文件都有清晰的单一原因变更。
- **优先 partial 拆分**：对既有大类（Feature/Controller）优先用 `partial` + 职责命名文件。
- **行为不变**：拆分应以“搬代码”为主，避免同时做语义重写。

## 2. 强约束：按需创建子目录，避免大杂烩目录

当某一目录下出现多个职责域交织时，必须按需创建子目录（而不是继续堆文件）：

- 示例：`Session/` 下按职责域拆为：
  - `Core/`（聚合根/host ports/accessors/编排/统一 dispose）
  - `Controllers/`（行为逻辑单元）
  - `SubFeatures/`（pipeline glue）
  - `Sim/`、`Gateway/`、`Snapshot/`、`Net/`、`Debug/`、`Editor/` 等（按需）

目录创建规则：

- **按职责域创建**：目录名反映职责，不用“Misc/Utils”。
- **保持浅层**：优先 1~2 层目录深度；只有在职责域进一步分裂时才加深层级。
- **稳定边界**：避免在多个目录之间循环依赖；如需共享，抽到更基础的域（通常是 Core 或 Common）。

## 3. 拆分命名约定（推荐）

- `Foo.Bar.cs`：在同一类型上按职责域拆分（partial）。
  - 例：`BattleSessionFeature.SimTick.RemoteDriven.cs`
- `Foo.Debug.cs` / `Foo.Editor.cs`：承载调试/编辑器条件编译逻辑。
- `Foo.Dispose*.cs`：释放逻辑按领域拆分（world/view/dispatchers 等）。

## 4. 与其他规则的关系

- 与 `state_handles_controllers.md` 配合：拆分时同时遵守 State/Handles/Controllers 边界。
- 与 `architecture.md` 配合：跨包/跨 asmdef 的拆分需要遵守依赖方向。

## 5. 验证要求

- 每个拆分小批次之后必须执行构建验证（例如 `dotnet build`），保证结构改动不引入破坏。
