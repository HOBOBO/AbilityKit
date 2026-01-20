---
trigger: manual
---

1. 项目目标与整体结构
定位：MOBA 技能/效果/触发器框架（Unity + Entitas ECS）
运行载体：EntitasWorld.Tick() 驱动
顺序：_triggerActions.Tick(deltaTime) -> Systems.Execute() -> Systems.Cleanup()
资源释放：Systems.TearDown()（不要在每帧 Cleanup 里做一次性反注册）
2. 关键模块
Triggering（触发器）
IEventBus：事件派发/订阅
TriggerRunner：执行触发器（conditions + actions）
TriggerRegistry：从程序集扫描 TriggerActionTypeAttribute/TriggerConditionTypeAttribute 自动注册 factory
TriggerEvent：事件结构 { Id, Payload, Args }
PooledTriggerArgs：Dictionary<string, object> 的对象池（Release 时 Clear）
Skill（技能）
SkillExecutor / SkillPipelineRunner：技能管线执行
MobaSkillTriggering：派发 skill.* 事件（例如 skill.cast.complete）
SkillCastRequest：常见 payload，包含 CasterActorId/TargetActorId/...
Passive Skills（被动技能）
MobaPassiveSkillTriggerRegisterSystem：按 SkillLoadoutComponent.PassiveSkillRuntime.TriggerIds 注册/反注册事件监听
默认自过滤：对“可判定来源 actorId 的事件”，外部事件只执行 AllowExternal=true 的 trigger entry
Buff（BUFF）
System 处理：MobaBuffApplySystem / MobaBuffTickSystem / MobaBuffRemoveSystem
Service 处理：MobaBuffService
公共事件常量：MobaBuffTriggering（buff.apply / buff.remove 等）
3. 触发器 JSON / Editor / DTO
DTO：TriggerDTO（JSON）包含 AllowExternal
Editor：AbilityModuleSO.TriggerEditorConfig 使用 Core: TriggerHeaderDTO 存头部字段
AllowExternal 必须写在 TriggerHeaderDTO 才能在 Inspector 序列化/展示
导出：AbilityTriggerJsonExporter 负责把 EditorConfig 导出成 TriggerDTO
4. 重要约定（必须统一，否则过滤/上下文会失效）
4.1 事件来源 actorId 约定
触发器若要支持“自身/外部”判定，事件必须在 evt.Payload/evt.Args 提供来源 actorId
当前被动系统识别来源的 key：
MobaSkillTriggering.Args.CasterActorId（"caster.actorId"）
EffectSourceKeys.SourceActorId（"effect.sourceActorId"）
或 payload 为 SkillCastRequest（优先读取 CasterActorId）
4.2 AllowExternal 语义（Passive Trigger）
AllowExternal=false（默认）：外部来源事件不会执行该 trigger
AllowExternal=true：允许处理外部来源事件（内部再通过条件判断关系/阵营等）
4.3 Buff 事件常量
使用 MobaBuffTriggering（不要在系统内再定义私有常量）
Events.ApplyOrRefresh = "buff.apply"
Events.Remove = "buff.remove"
Args.BuffId/EffectId/StackCount/DurationSeconds/RemoveReason
5. 常见坑（踩过的坑）
EventBus 实例不一致：订阅和 publish 必须用同一个 IEventBus 实例（项目已改为 DI singleton）
不要在 Entitas Cleanup 做一次性反注册：Cleanup 每帧调用；一次性逻辑应迁移到 TearDown
args 合并性能：优先复用可变 args（IDictionary）临时注入并恢复；避免每次复制整个 args
6. 扩展方式（新增 Action/Condition）
新增 runtime factory：实现 IActionFactory/IConditionFactory + 标注 [TriggerActionType]/[TriggerConditionType]
新增 Editor 强类型配置：继承 ActionEditorConfigBase/ConditionEditorConfigBase + 标注对应 attribute
新增 RuntimeConfig（导出用）：继承 ActionRuntimeConfigBase/ConditionRuntimeConfigBase，实现 ToActionDef/ToConditionDef
7. 代码实现规范（强约束）
- 必须：性能优先。默认假设战斗逻辑在高频调用路径（每帧/多实体/多技能），避免不必要的分配与反射。
- 优先：能用 struct 就用 struct（尤其是短生命周期、频繁创建/传递的数据）。如需 class，必须说明原因（例如：需要多态/引用语义/Unity 序列化）。
- 禁止：在 Update/Tick/Execute 等高频路径中产生 GC（new List/Dictionary/LINQ/闭包/装箱 等），除非明确标注“低频/编辑器/初始化阶段”。
- 必须：默认使用对象池/集合池。
  - Args：优先使用 PooledTriggerArgs / PooledDefArgs
  - 临时集合：优先使用项目内 Pools 提供的 pool（或复用已有对象），避免 new
- 优先：对只读合并参数使用“临时注入并恢复”或“overlay 视图”，避免复制整个 args 字典。
- 必须：新写/新增的业务逻辑代码提供中文注释（说明目的、关键约束、边界条件）。
  - 例外：纯样板代码/显而易见的 getter/setter 可不写
- 必须：新增代码如果引入性能风险点，需要在同文件内显式写出原因与替代方案（中文）。
7.1 提交前自检（建议）
- 是否引入 LINQ / foreach 分配 / closures？
- 是否在高频路径 new List/Dictionary/HashSet？
- 是否能复用 PooledTriggerArgs / PooledDefArgs？
- struct 是否避免了装箱（object/接口传递）？
## Buff 多阶段效果协议（OnAdd/OnRemove/OnInterval）
- 约定：Buff 配置不再使用单一 EffectId，改为：
  - OnAddEffects / OnRemoveEffects / OnIntervalEffects
  - IntervalMs（<=0 表示不触发 interval）
- 约定：Buff 相关事件：
  - buff.apply / buff.apply.<effectId>
  - buff.remove / buff.remove.<effectId>
  - buff.interval / buff.interval.<effectId>
- 必须：buff.* 事件 args 必须包含（用于被动 AllowExternal 外部过滤通用逻辑）：
  - effect.sourceActorId
  - effect.targetActorId
  - buff.id / buff.effectId / buff.stage / buff.stackCount
  - （remove）buff.removeReason
- 约定：buff.stage 取值：
  - add / remove / interval

  ## 表现层（Game/Battle）实体管理与 View 流程（Projectile/VFX）

### 1) Battle 表现层世界（EC.EntityWorld）结构
- BattleContext 持有表现层 ECS：
  - EntityWorld / EntityNode / EntityLookup / EntityFactory / EntityQuery
- BattleEntityFeature 在 OnAttach 时创建：
  - EntityNode = CreateChild("BattleEntity")
  - Factory：负责创建 Character/Projectile 的表现层 EC.Entity（不是 Unity GameObject）
  - Lookup：netId -> EntityId 映射
  - Query：查询封装（TryResolve/TryGetTransform 等）

### 2) Snapshot 驱动实体生成/更新
- 数据入口：FrameSnapshots + BattleSnapshotPipeline
- 常用快照：
  - ActorSpawnSnapshot：创建/刷新 Character/Projectile 表现层实体（BattleActorSpawnApplier）
  - ActorTransformSnapshot：更新 BattleTransformComponent，并把 entityId 写入 DirtyEntities
  - ProjectileEventSnapshot(4006)：Spawn/Hit/Exit 的事件流（用于特效/音效等表现）

### 3) BattleViewFeature：ViewBinder 绑定/同步 GameObject
- BattleViewFeature 负责把 EC.Entity“表现数据”同步到 Unity GameObject
- 核心：
  - RefreshDirtyViews(): 遍历 DirtyEntities -> binder.Sync(entity)
  - binder 内部维护 EntityId -> Handle（包含 GameObject、VFX 绑定等）
  - OnEntityDestroyed: 清理对应 Handle、Destroy GameObject、清理其绑定的 VFX entity

### 4) VFX 统一管理（VFX 也是 EC.Entity）
- VfxDatabase：Resources/vfx/vfx.json（VfxDTO：Id/Resource/DurationMs）
- BattleVfxManager：
  - TryCreateVfxEntity：创建 VFX EC.Entity（挂 BattleVfxComponent + BattleViewGameObjectComponent + Follow + Lifetime）
  - Tick(world)：
    - follow：同步跟随目标实体位置（读 BattleTransformComponent）
    - lifetime：到期自动销毁（Destroy GameObject + Destroy entity）
- 约定：VFX 配置为纯表现配置，不进入 MobaConfigDatabase 的 runtime 表注册（避免逻辑侧依赖 Unity 资源）

## 飞行物核心模块
模块位置
逻辑层：IProjectileService / ProjectileWorld / ProjectileService
Moba 适配：MobaProjectileService（launcher + schedule emit）
同步：MobaProjectileSyncSystem（创建 bullet ActorEntity + link）
配置表约定
ProjectileLauncherDTO: DurationMs/IntervalMs/CountPerShot/FanAngleDeg/EmitterType
ProjectileDTO: Speed/LifetimeMs/MaxDistance/HitPolicyKind/HitsRemaining/.../VfxId/OnSpawnVfxId/OnHitVfxId/OnExpireVfxId/OnHitEffectId
VfxDTO: Resource/DurationMs（表现层独立加载，不进 runtime config registry）
溯源链路
ProjectileSpawnParams/events 携带 TemplateId/LauncherActorId/RootActorId
网络事件
ProjectileEventSnapshot (4006)：Spawn/Hit/Exit 结构字段说明
性能/池化注意
schedule spawn 使用 list pool
禁止高频 new List/LINQ 等