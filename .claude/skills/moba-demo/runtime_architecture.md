# demo.moba.runtime 架构分层

根：`Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/`，分 6 个顶层目录：

```
Runtime/
├── Application/   装配层（把领域能力接入 World/DI/Entitas/Bootstrap/宿主）
│   ├── Gameplay/       玩法规则（Core/Rules/Systems/Triggering）
│   ├── Gate/           （空目录）
│   ├── Rollback/       回滚支持（MobaActorTransformRollbackProvider / RollbackWorldRandom / PassiveSkillTriggerEventRollbackLog）
│   ├── Services/       ★ 40 个子目录（按业务，归 ability-kit skill）
│   ├── Session/        宿主适配（MobaSessionCoordinatorHost / MobaBattleDriverHost / 等 8 个，归 coordinator skill）
│   └── Systems/        Entitas Systems + Bootstrap Flow（18 子目录，含 Bootstrap/Flow/PlanActions）
├── Common/        包内共享层
│   ├── Enum/           DamageEnums 等
│   └── Shared/
│       ├── Battle/SearchTarget/Entitas/   目标选择 Entitas 适配
│       ├── ECS/Entitas/                   EntitasEcsWorld / EntitasUnitFacade
│       ├── Entitas/Generated/             ★ Entitas 生成代码（5 Context，详见 ecs_components.md）
│       └── Enum/
├── Domain/        玩法语义层（无 DI、无 System 安装）
│   ├── Ability/Impl/Moba/    EffectSourceCompat.cs（空命名空间占位）
│   ├── Ability/Pipeline/{Skill,Timeline}/
│   ├── ActionTimeline/       MobaTimelinePlayer / ClipHandlers
│   ├── Actions/ Attributes/ Behavior/ Predicates/ Triggering/
│   ├── Components/           ★ Domain 组件（ActorComponent/BuffComponent/SkillRuntime/...）
│   └── Events/{Buff,Combat,Skill,Summon,Unit}/   领域事件
├── Infrastructure/   基础设施适配层
│   ├── Config/{BattleDemo,Core}/
│   │   ├── BattleDemo/LubanGen/   ★ Luban 生成代码（Tables/Characters/Buffs/AttributeTemplates + vector2/3/4 + demo/Tbitem）
│   │   ├── BattleDemo/Loaders/    DefaultMobaConfigBytesLoader
│   │   └── Core/                  MobaConfigFormat + MobaConfigTableDeclarations + generated Manifest
│   ├── Entitas/                   MobaEntitasContextsExtensions
│   ├── Serialization/             DemoWireSerializerBootstrap（MemoryPack 适配，WorldInitStage 调用）
│   └── Util/{Converter,Generator}/ + FixedStepTickRunner
├── Worlds/Blueprints/    World 蓝图
├── Docs/                 ★ 11 篇团队约定 .md（见下）
└── Testing/              BattleTestScript / BattleTestScriptRunner / MobaRuntimeTestEnvironment / MobaTestConfigBuilder
```

注意：`Application/Services/` 与 `Application/Systems/` 的具体业务内容（Skill/Buffs/Triggering 等）归 ability-kit skill。

## 生成 Manifest 边界

`Infrastructure/Config/Core/MobaConfigTableDeclarations.cs` 是内建配置表的唯一声明入口。`com.abilitykit.demo.moba.codegen` 在编译期读取这些声明，生成配置表 Manifest、强类型 DTO/MO factory 和 changed-ID collector；runtime 默认直接消费生成结果。Generator 不推断 DTO→MO 字段映射，特殊转换继续由 `MO(DTO)` 构造器负责。

反射只保留给外部程序集、legacy 注册或生成结果缺失时的兼容 fallback。新增声明、诊断编号和 fallback 边界见 [codegen_analyzer.md](codegen_analyzer.md)。

## 11 篇团队约定 Docs（必读）

`Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Docs/`：

- `RuntimeArchitectureGuide.md` — 目录职责 + 依赖方向
- `StartupChainGuide.md` — 完整启动链路
- `BootstrapFlowGuide.md` — Stage 写法（**注意过时：声称 Dependencies 未使用，实际已实现拓扑排序**）
- `SystemOrderGuide.md` — 系统排序
- `ServiceRegistrationGuide.md` — 服务注册
- `SnapshotGuide.md` — 快照
- `EventGuide.md` — 事件
- `MobaCombatContextDesignGuide.md` — 战斗上下文设计
- `GoldenSkillFlowGuide.md` — 黄金技能流
- `MobaRuntimeProductionReadinessReview.md` — 生产就绪评估
- `LegacyCompatibilityInventory.md` — 遗留兼容清单
