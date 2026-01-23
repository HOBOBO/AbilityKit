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

Rule 1：禁止丢失溯源（No Lost Origin）
任何会发布 TriggerEvent 的模块/系统/服务，必须保证事件携带溯源字段：

根事件必须显式写入 origin.*
非根事件必须继承（透传）上游的 origin.*
Rule 2：继承发布必须使用统一入口（Recommended）
当你从已有事件（存在 parentArgs）再发布新事件时，必须继承溯源字段：

至少继承：origin.source / origin.target / origin.kind / origin.configId / origin.contextId
推荐使用统一 helper：TriggerEventPublishExtensions.PublishInherited(...)
Rule 3：内部流水线事件必须带 Args（No args:null）
禁止发布 args: null 的 TriggerEvent（会导致溯源链路中断）。
内部流水线（例如伤害流水线）发布事件时，应至少填入：

source/target
origin.*（来自 payload/attack 上携带的 origin，或从上游传入）
Rule 4：origin.kind 类型统一为 enum
origin.kind 必须统一使用 EffectSourceKind（enum），禁止使用字符串，避免跨模块不一致与比较成本。

目标
任意 TriggerEvent 链式触发（skill -> effect -> damage -> buff -> projectile -> …），都能追溯到同一个 origin.contextId。
contextId 是对外统一语义；rootId 仅为溯源模块内部概念（“无父即根”），不要写入 origin.contextId。
术语与字段
source/target（当前事件参与者）
EffectTriggering.Args.Source
EffectTriggering.Args.Target
origin（溯源信息，链式传播）
EffectTriggering.Args.OriginSource
EffectTriggering.Args.OriginTarget
EffectTriggering.Args.OriginKind（EffectSourceKind）
EffectTriggering.Args.OriginConfigId（skillId / effectId / buffId …）
EffectTriggering.Args.OriginContextId（contextId）
sourceContextId（溯源上下文句柄）
EffectSourceKeys.SourceContextId（long）
这是溯源系统的 key，用于从 EffectSourceRegistry 反查 origin.*
根事件发布（Root Publish）必须满足
当你在系统里“首次产生一条链路事件”（比如玩家施法、被动技能触发、系统触发等）：

必须设置：
source/target
origin.source/origin.target
origin.kind/configId/contextId
effect.sourceContextId（即 EffectSourceKeys.SourceContextId）
origin.contextId 必须等于当前链路的 contextId（通常就是 sourceContextId）。
中继/转发事件（Relay Publish）必须满足
当你是在一个事件处理过程中再次发布新事件（例如 effect.execute -> effect.apply 这种转发）：

必须使用 PublishInherited(bus, eventId, payload, parentArgs, fillArgs) 来继承 origin.*
严禁在中继发布时无条件覆盖 origin.*
允许补默认值：仅在 key 不存在时设置
允许补充新字段：例如额外的 effect.id、buff.stage 等
中继发布的判断经验
出现下列模式之一，基本属于中继发布：

handler/subscriber 里读取 evt.Args/evt.Payload 后再次 bus.Publish(new TriggerEvent(...))
把 evt.Args copy 到新 args 再发
“按某个 id 派生新事件 id” 再发（例如 xxx.byId(effectId)）
规则：这种情况优先改为 PublishInherited。

Origin 填充的统一实现
统一使用 EffectOriginArgsHelper（集中封装）：
EffectOriginArgsHelper.FillFromRegistry(args, sourceContextId, registry)
EffectOriginArgsHelper.FillFromServices(args, sourceContextId, services)
它负责：
从 EffectSourceRegistry 填 origin.source/origin.target
从 snapshot 填 origin.kind/configId/contextId（contextId，不是 rootId）
语义约束（必须遵守）
origin.contextId 永远写 contextId（snapshot.ContextId / sourceContextId）
rootId 只在溯源系统内部维护/使用，不向外暴露为 origin.contextId

目标与架构原则
模块化 TriggerAction：复杂行为（召唤、伤害、表现）统一采用 Spec / Parser / Resolver / Action / Factory / (Builder) 的结构拆分，避免在单个 Action 里堆逻辑。
配置与运行时解耦：
Share/Editor 层不得依赖 Impl 层枚举/类型（避免跨层引用）。
Share/Editor 需要枚举下拉时，使用 Share 层枚举（数值与 Impl 保持一致）。
模板化配置优先：复杂参数用 templateId + overrides(args)，把“默认值/静态配置”放模板表，把“动态值/每次触发变化”放 args。
目标选择统一：伤害与表现统一采用“Option1：统一 TargetMode + 可选 queryTemplateId + 可选显式 target + 支持从 payload/vars 取目标”的思路，减少重复实现。
事件驱动表现：
持续表现：走 Buff/Effect 的 IGameplayEffectCue 生命周期。
瞬时表现：走 TriggerAction 发布 presentation.play / presentation.stop 事件，表现层订阅处理。
召唤（Spawn Summon）规则
SpawnSummon 运行时必须通过 Resolver 解析最终 Spec：
从 spawn_summon_action_templates 读模板 DTO/MO。
使用 Trigger args 对模板字段做覆盖（overrides）。
Action 只负责执行（不负责解析复杂参数）。
TriggerStrong 侧只存 Share 层枚举/数值：
运行时 ActionDef.args key 必须保持兼容（老配置不破）。
配表导出必须存在：
Assets/Resources/moba/spawn_summon_action_templates.json 必须可被 MobaConfigDatabase.LoadFromResources("moba") 加载。
ScriptableObject 表资产使用 MobaConfigTableAssetSO 子类接入统一导出器。
伤害（Damage）规则（结构要求）
Damage 行为同样遵循 Spec/Parser/Resolver：
GiveDamageAction / TakeDamageAction 不直接硬编码一堆目标/公式解析逻辑。
目标选择抽到 DamageTargetSelection（或等价模块）并支持从：
context Source/Target
显式 target
queryTemplateId 查询
payload/vars
伤害数值计算：Resolver 负责读取公式/参数并产出最终数值，Action 负责调用服务应用伤害。
表现模板（Presentation Template）规则
表现模板表：presentation_templates
只存“静态默认值”：资源 id、默认时长、附着方式、stack/stop policy、默认颜色/scale/offset 等。
Trigger 驱动表现（play_presentation）：
args 最少需要：templateId，可选：targetMode、queryTemplateId、requestKey、durationMs、posKey/pos 以及动态覆盖参数（scale/radius/color 等）。
如果 stop=true：发布 presentation.stop，否则发布 presentation.play。
事件契约：
EventId：presentation.play / presentation.stop
args 必须携带：templateId、targets（actorId list）或 positions（vec3 list）等能驱动表现的字段。
requestKey 用于 stop/replace 定位（表现层自行实现策略）。
MobaConfigDatabase / Registry 规则
新增表必须同步做三件事：
MobaConfigPaths 增加 File 常量
MobaRuntimeConfigTableRegistry 注册 DTO/MO
Resources 下补齐 {file}.json（或确保导出流程生成）
Editor 导出统一走：
MobaConfigJsonExporter（菜单 + Inspector 按钮）
MobaConfigTableAssetSO + IMobaConfigTableAsset 自动发现

Skill: 新增一个“模板化 TriggerAction”（以 SpawnSummon / PlayPresentation 为范式）
输入：
TriggerAction type string
Share 层 TriggerStrong RuntimeConfig + EditorConfig 字段
Impl 层 Action + Factory +（Parser/Resolver 如需）
MobaConfig 表（可选）
输出：
TriggerStrong 可编辑、可编译为 ActionDef
运行时 Action 通过 Factory 注册并可执行
如用模板表：Resources 可加载、Editor 可导出
步骤规范
定义 TriggerActionTypes 常量
在 TriggerActionTypes 增加 type 字符串常量（例如 play_presentation）。
TriggerStrong：RuntimeConfig
新增 *ActionConfig : ActionRuntimeConfigBase
Type => TriggerActionTypes.*
ToActionDef() 写入 args（key 命名稳定，兼容老配置）
TriggerStrong：EditorConfig
新增 *ActionEditorConfig : ActionEditorConfigBase
用 [TriggerActionType(...)] 注册 Odin 菜单
ToRuntimeStrong() 产出 runtime config
Impl：Factory
在 Create(type)/注册表中注册新 Action
Impl：Action
不做复杂解析：只做执行与事件/服务调用
复杂参数交给 Resolver/Selection
如果涉及模板表
DTO/MO + Registry + Resources json + Editor 导出 SO 全链路补齐（见下面 skills）。
Skill: 新增一个 Moba 配表（DTO/MO + Registry + Resources + 导出 SO）
目标：新增表能被 MobaConfigDatabase.LoadFromResources("moba") 加载，且能在 Unity 编辑器里一键导出 json。
步骤
DTO
在 MobaCoreDtos.cs 新增 XXXDTO，必须包含 int Id。
MO
新增 XXXMO，构造函数接收 XXXDTO，把字段映射为只读属性。
Paths
在 MobaConfigPaths.cs 增加 XXXFile 常量（不带 .json）。
Registry
在 MobaRuntimeConfigTableRegistry.cs 注册：
new Entry(MobaConfigPaths.XXXFile, typeof(XXXDTO), typeof(MO.XXXMO))
Resources
在 Assets/Resources/moba/ 增加 xxx.json（至少放一个占位 entry，避免缺表崩溃）。
Editor 导出 SO
新增 XXXSO : MobaConfigTableAssetSO
包含 public XXXDTO[] dataList;
FileWithoutExt => MobaConfigPaths.XXXFile
EntryType => typeof(XXXDTO)
GetEntries() => dataList
导出方式
选中 SO，在 Inspector 点 Export Config Json（或菜单 AbilityKit/Moba/Export Config Json）
Skill: Trigger 驱动表现（play_presentation）
输入 args 约定：
templateId (int, 必填)
targetMode (int enum, 默认 Target)
queryTemplateId (int, 可选)
target (object, 可选显式目标)
requestKey (string, 可选，用于 stop/replace)
durationMs (int, 可选覆盖)
posKey (string) / pos (vec3)（可选，用于位置表现）
stop (bool, 可选；true=stop)
动态覆盖：scale/radius/color...
行为：
解析目标集合（actorIds 或 positions）
发布事件：
presentation.play 或 presentation.stop
表现层订阅事件并实例化/停止实际表现资源
Skill: 召唤模板化（spawn_summon_action_templates）
配置结构：
TriggerStrong action 只提供：templateId + overrides（例如目标/位置模式等）
Resolver 负责：
从 MobaConfigDatabase 取模板 DTO/MO
用 args 覆盖字段
产出最终 SpawnSummonSpec
配表生产：
Unity SO：SpawnSummonActionTemplateSO
导出：spawn_summon_action_templates.json 到 Assets/Resources/moba/