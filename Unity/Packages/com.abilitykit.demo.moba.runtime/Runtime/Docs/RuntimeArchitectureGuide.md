# MOBA 运行时架构指南

本文档定义 `com.abilitykit.demo.moba.runtime` 的运行时结构约定。这个包不是最小 Demo，而是 AbilityKit 在复杂战斗场景下的最佳实践样例：它应该展示一套可运行、可拆分、可替换、可测试的战斗逻辑组织方式。

## 目标定位

`moba.runtime` 承担三类职责：

1. 展示 AbilityKit 各核心模块在真实玩法中的组合方式。
2. 提供一套可复制的逻辑层目录、启动链路、服务注册和系统排序范式。
3. 作为 Unity 表现层、控制台模拟、服务端托管等外部宿主共用的战斗逻辑包。

它不承担以下职责：

1. 不直接绑定具体 UI、相机、特效、音频和输入表现。
2. 不把编辑器工具、配置导出工具或平台适配细节放入运行时主链路。
3. 不把临时兼容逻辑扩散为新的默认扩展点。

## 顶层目录职责

### 应用层

`Application` 是运行时装配层，负责把领域能力接入 World、DI、Entitas Systems、Bootstrap Flow 和对外宿主。

推荐放置：

1. World 启动入口，例如 `MobaWorldBootstrapModule`。
2. Bootstrap Flow 和 Bootstrap Stage。
3. Entitas System 注册、排序和安装逻辑。
4. 应用级 Service 接口与服务编排。
5. 对外用例入口，例如创建战局、推进帧、提交输入、查询快照。

不推荐放置：

1. 具体伤害、技能、Buff、投射物规则。
2. Unity 表现层对象和视图生命周期。
3. 配置表读取格式的底层适配细节。

### 领域层

`Domain` 是玩法语义层，表达 MOBA 规则本身。

推荐放置：

1. 英雄、单位、技能、Buff、效果、目标选择等领域模型。
2. 与框架无关或弱绑定的规则对象。
3. 领域事件、领域命令和领域枚举。
4. 技能链路中的配置解释、规则判定和语义模型。

不推荐放置：

1. DI 注册代码。
2. Entitas System 安装代码。
3. Unity 表现层类型。

### 基础设施层

`Infrastructure` 是基础设施适配层，负责把外部资源、配置、协议或工具接入运行时。

推荐放置：

1. Luban 配置加载与 DTO 映射。
2. 随机数、时间、日志、资源读取等可替换适配。
3. 网络协议、输入协议、快照序列化的适配代码。
4. 用于运行时的工具实现。

不推荐放置：

1. 领域规则本体。
2. 直接驱动表现层的逻辑。
3. 需要编辑器上下文才能工作的工具。

### 世界层

`Worlds` 是战斗世界组织层，负责描述不同 World 的上下文、模块组合和运行边界。

推荐放置：

1. 战斗 World 的创建、配置和上下文封装。
2. 不同运行模式下的 World 组合方案。
3. 与 FrameSync、Snapshot、StateSync 相关的 World 级策略。
4. 多 World 或宿主接入时的边界对象。

不推荐放置：

1. 单个技能或 Buff 的业务规则。
2. 通用框架扩展。
3. Unity View 逻辑。

### 公共部分

`Common` 是包内共享层，只放跨多个目录复用且稳定的基础类型。

推荐放置：

1. 包内通用常量、轻量工具和共享值对象。
2. 生成代码需要共享的上下文类型。
3. 没有明确领域归属但确实被多处稳定复用的类型。

不推荐放置：

1. 为逃避分层而放入的杂项逻辑。
2. 只被一个功能使用的私有工具。
3. 临时兼容代码。

### 文档

`Docs` 是样例包的团队约定入口。所有会影响扩展方式、启动链路、系统顺序、事件流转和快照语义的规则，都应优先在这里留下说明。

现有文档职责：

1. `BootstrapFlowGuide.md`：Bootstrap Flow 与 Stage 扩展方式。
2. `ServiceRegistrationGuide.md`：World DI 服务注册方式。
3. `SystemOrderGuide.md`：Entitas System 阶段和排序约定。
4. `SnapshotGuide.md`：快照和状态恢复约定。
5. `EventGuide.md`：事件流转约定。

## 依赖方向

推荐依赖方向如下：

```text
Application
    -> Domain
    -> Infrastructure
    -> Worlds
    -> Common

Worlds
    -> Domain
    -> Infrastructure
    -> Common

Infrastructure
    -> Domain
    -> Common

Domain
    -> Common
```

约束规则：

1. `Domain` 不反向依赖 `Application`、`Worlds` 或 `Infrastructure` 的装配细节。
2. `Infrastructure` 可以把外部数据转换为领域对象，但不拥有玩法规则解释权。
3. `Application` 可以依赖所有内部层，因为它负责最终装配。
4. Unity 表现层应依赖 `moba.runtime`，而不是由 `moba.runtime` 反向调用表现层。
5. 新增代码如果无法判断归属，优先写入最窄的业务目录，不要直接放入 `Common`。

## 启动链路入口

当前推荐入口是：

```text
MobaWorldBootstrapModule
    -> MobaBootstrapFlow
    -> MobaBootstrapStageRegistry
    -> Bootstrap Stages
    -> World DI Modules / Entitas Systems / Runtime Initializers
```

关键职责：

1. `MobaWorldBootstrapModule` 是外部宿主接入 World 的默认模块入口。
2. `MobaBootstrapFlow` 负责按 Stage 顺序执行 Configure 与 Install。
3. `MobaBootstrapStageRegistry` 负责收集并排序 Bootstrap Stage。
4. Stage 负责具体启动阶段，不应把所有初始化逻辑重新塞回单个 Bootstrap 类。
5. Entitas System 的最终顺序应遵循 `SystemOrderGuide.md`。

新增启动逻辑时，优先选择：

1. 如果是服务注册，进入 World DI Module 或 `[WorldService]` 自动注册路径。
2. 如果是系统安装，使用 `[WorldSystem]` 和系统排序约定。
3. 如果是启动阶段，新增 Bootstrap Stage。
4. 如果只是配置数据预热，放入明确的 Config/Plan/Runtime Init 阶段。

## 服务注册入口

服务注册以 World DI 为默认路径。

推荐方式：

1. 稳定服务使用 `[WorldService]` 标记并由属性注册模块扫描。
2. 需要组合多个服务时，新增小型 `IWorldModule`。
3. 需要替换实现时，通过模块组合或宿主注入替换，不在业务代码里直接 new 默认实现。
4. 服务命名应表达能力，例如 `SkillPlanService`、`TargetingService`、`BattleSnapshotService`。

避免方式：

1. 在 Entitas System 内部手动构造复杂服务图。
2. 在领域对象内访问全局单例。
3. 通过静态字段传递战局状态。

## 系统与帧推进

Entitas System 是帧内执行单元，必须遵循稳定排序。

新增系统时需要确认：

1. 所属阶段：PreExecute、Execute、PostExecute 或更细分的业务 Phase。
2. 是否依赖其他系统输出。
3. 是否会写入可快照状态。
4. 是否需要在回滚或重放中保持确定性。
5. 是否可以通过服务或事件降低与其他系统的直接耦合。

系统不应承担：

1. 配置表底层加载。
2. 长生命周期宿主管理。
3. Unity 表现对象创建。
4. 与自身阶段无关的跨链路初始化。

## 技能、触发器与管线扩展

技能链路建议按以下层次组织：

```text
Config / DTO
    -> Plan / Runtime Model
    -> Trigger / Condition
    -> Targeting
    -> Pipeline Phase
    -> Effect / Damage / Projectile / Modifier
    -> Event / Snapshot
```

推荐扩展方式：

1. 新技能优先复用已有 Trigger、Targeting、Pipeline Phase 和 Effect Action。
2. 新效果先判断是领域规则、管线节点还是触发动作，不要直接写成系统特例。
3. 被动、Buff、条件响应优先接入 Triggering，而不是散落在多个系统中轮询判断。
4. 需要可配置的链路，应进入 ActionSchema 或 Plan，而不是硬编码在 System 中。
5. 影响战斗状态的结果必须能进入事件、快照或确定性状态同步链路。

## 外部宿主接入

`moba.runtime` 应允许不同宿主复用同一套逻辑：

1. Unity View Runtime：负责表现、输入采集和渲染反馈。
2. Console Simulation：负责无表现层的逻辑验证和回归测试。
3. Server/Host：负责托管战局、接收输入、广播状态。
4. Editor/Tools：负责配置校验、调试和生成辅助。

宿主接入时应只依赖公开的应用入口、World Module、输入协议和快照协议。不要绕过启动链路直接修改内部系统状态。

## 临时与兼容代码策略

为了从“先跑起来”过渡到“最佳实践样例”，允许短期保留兼容代码，但必须满足：

1. 文件名、类名或注释中明确标记 Legacy、Compat、Stub 或 Obsolete。
2. 标记替代路径或迁移目标。
3. 不把临时接口写进新的推荐文档。
4. 新功能不得继续依赖临时路径。
5. 后续清理时优先删除没有外部宿主使用的兼容层。

## 新增功能放置建议

| 需求 | 推荐位置 | 推荐入口 |
| --- | --- | --- |
| 新增战斗服务 | `Application` 或对应领域目录 | `[WorldService]` / `IWorldModule` |
| 新增启动阶段 | `Application/Systems/Bootstrap` | `MobaBootstrapStageBase` |
| 新增 Entitas System | `Application/Systems` 或业务子目录 | `[WorldSystem]` + System Order |
| 新增技能配置解释 | `Domain` + `Infrastructure` | Plan/DTO Mapper |
| 新增触发动作 | 领域触发目录 | Triggering / ActionSchema |
| 新增目标选择 | 目标选择领域目录 | Targeting Service / Pipeline Phase |
| 新增快照字段 | 状态所属目录 | Snapshot Guide 约定 |
| 新增宿主适配 | `Worlds` 或外部包 | World Module / Host Adapter |
| 新增表现逻辑 | `com.abilitykit.demo.moba.view.runtime` | View Event / Presentation Flow |

## 修改检查清单

提交 `moba.runtime` 变更前，至少检查：

1. 新代码是否放在最合适的层，而不是随手放入 `Common`。
2. 是否破坏了 Logic/View 分离。
3. 是否绕过 Bootstrap Flow、World DI 或 System Order。
4. 是否引入了新的静态全局状态。
5. 是否影响帧同步、回放、快照或确定性。
6. 是否需要补充或更新 `Docs` 下的约定文档。
7. 是否可以被 Unity View、Console Simulation 和 Host 以同一套逻辑复用。
