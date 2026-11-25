# MOBA 特效表现模板管理优化方向

## 文档状态

- 类型：MOBA 表现层优化方向与实施批次记录
- 状态：前四批已实现；第五批空间语义收敛与运行时尺寸来源诊断已实现，验证记录见文末
- 配置权威来源：`LubanConfig/Moba/Production/Datas/T-特效表.xlsx`
- 发布入口：`python LubanConfig/Moba/export_pipeline.py --apply`

## 目标与边界

建立一套可预测的特效模板契约，统一资源、额外缩放、来源缩放、挂载、跟随、偏移坐标空间、渲染排序和生命周期策略。模板负责可复用的表现默认值；技能 Cue 可以覆盖本次播放所需的位置、缩放和时长；运行时倍率通过独立来源贡献，不写回模板或 Prefab 基准。

特效表现不改变逻辑实体的尺寸、碰撞、射程或伤害。逻辑实体缩放投影、Avatar 缩放、VFX 模板缩放和播放速度是不同维度，禁止复用同一个字段或累乘已变换的结果。

## 初始问题基线（前四批前）

`T-特效表.xlsx` 有「特效资源」和「特效表现模板」两个子表。表现模板已有 `AttachMode`、`Follow`、`Socket`、`OffsetX/Y/Z`、`Radius`、`Scale`、`StackPolicy` 和 `StopPolicy` 等字段。`Scale` 已进入 `PresentationRequest`，Unity 后端按 Prefab 基准缩放、请求缩放和按来源聚合的运行时缩放重新计算局部缩放。

目前有两种位置更新路径，语义尚未统一：

1. `PresentationUnityBackend` 通过 attachment resolver 解析 Actor/Socket，resolver 用 `TransformPoint` 将偏移按目标局部坐标转换到世界位置，再将实例设为目标 Transform 的子节点。此前模板 `Follow` 字段没有进入该路径，导致表面上的“是否跟随”配置没有控制最终行为。
2. `BattleVfxFollowController` 每帧从插值目标位置更新对象，并直接将 `Offset` 加到世界坐标；它不旋转偏移，也不建立 Transform 父子关系。

`OffsetX/Y/Z` 没有坐标空间字段。当前 attachment resolver 将其解释为目标局部偏移；非挂载时则落为世界位置。挂载还会继承父节点缩放。模板也没有渲染排序字段，VFX 运行时未统一设置 Renderer 的 Sorting Layer/Order。

## 配置职责模型

### 资源与播放

资源 ID/路径、默认时长、播放速度、循环、次数、延迟与间隔属于播放契约。Prefab 内部 Renderer 的材质队列、粒子材质和复杂的多 Renderer 结构仍由资源制作维护；模板只配置跨资源统一的策略。

### 缩放

最终空间缩放从稳定基准计算：

```text
最终局部缩放 = Prefab 基准缩放 × 模板额外倍率 × 本次 Cue 倍率 × 各活跃来源倍率的合成值
```

各来源使用稳定 SourceId 独立设置、替换和移除；移除来源后从其余贡献重新求值。禁止把当前 Transform.Scale 当作下一次计算的基准。模板 `Scale` 是额外倍率，`Radius` 是语义尺寸参数，二者不互相替代。挂载父节点缩放是否继承应成为独立策略，默认继承；忽略父缩放时由表现后端显式补偿，不通过修改 Prefab 基准或偏移实现。

### 挂载、跟随与偏移

将原先容易混淆的行为拆成四项：

| 维度 | 建议值 | 行为 |
|---|---|---|
| AttachmentTarget | World / ActorRoot / Socket / Projectile / Area | 选取初始定位目标或父节点 |
| FollowMode | None / Position / PositionAndRotation / TransformParent | 决定目标更新如何驱动特效 |
| OffsetSpace | World / TargetLocal | 决定偏移在哪个坐标系中解释 |
| ParentScaleMode | Inherit / Ignore | 决定 TransformParent 是否继承目标缩放 |

`Follow` 是兼容期旧字段：与非 World 的 `AttachMode` 同时启用时表示持续跟随；关闭时表示仅在生成时解析一次目标位置，不保留父子关系。没有挂载目标时 Follow 无效，应在配置检查中提示。Socket 找不到时的 fallback 规则必须显式且可诊断，建议依次回退到 ActorRoot，再回退到请求的世界位置。

世界偏移始终在世界轴解释；TargetLocal 偏移由目标旋转和目标缩放转换。通过父节点挂载时，局部偏移保持局部坐标，父节点运动自然驱动其世界位置。`FollowMode=Position` 需要由运行时跟随控制器更新位置而不覆盖特效朝向；`PositionAndRotation` 同时更新旋转；`TransformParent` 使用真实父子层级。第一阶段不增加 Position 模式专用代码，优先统一现有 Parent 与 None 两种路径。

### 渲染排序

排序模板只表达语义，不暴露 Unity 的 Sorting Layer、Order 或 Transform Z 等后端细节。当前字段是 `OrderingMode` 与 `OrderingPriority`：`ResourceDefault` 保留资源制作时的排序；`RelativeToAttachment` 表示相对挂载对象的顺序；`ExplicitPriority` 表示使用显式抽象优先级。优先级是稳定的数据契约，具体渲染后端可将它解释为队列、层级、深度策略或其他排序机制。

通用请求携带 `PresentationOrdering`，由 `IPresentationOrderingAdapter<TVisual>` 消费。Unity 当前适配器优先使用 SortingGroup，否则使用 Renderer，并对资源基准做 delta 调整以保留 Prefab 内多个 Renderer 的相对差异；它不改 Sorting Layer。该 Unity 映射只是当前后端策略，可以替换适配器而不改工作簿、请求或技能配置。无 SortingGroup/Renderer 的资源保持原样，3D 深度仍由场景渲染规则决定。挂到父节点后仍沿用同一语义，由适配器基于最终 attachment target 解释，不把 Transform 父子深度等同于排序。

### 生命周期与冲突

堆叠、停止、目标丢失/销毁、Prefab 播放完成与模板时长之间需有可解释优先级。目标销毁策略建议枚举为 DestroyWithTarget / DetachAtLastWorldPose / KeepResolvingByActorId；当前实现中项目/Actor 跟随有延迟重绑和按目标清理能力，模板默认策略不能假设所有资产都具备相同生命周期。

## 实施批次

### 第一批：收敛模板请求路径中已存在字段的运行时语义

- 将模板 `Follow` 传入运行时请求，并让 `Follow=false` 的挂载特效只解析初始 Transform，不保留目标父节点。
- 倍率来源刷新只重新计算缩放，不重新解析位置，避免视觉倍率变化造成特效跳位。
- Cue 重建请求时保留模板跟随策略。
- 保持现有 Excel 结构和发布产物兼容，不更改既有字段值。
- 本批不更改旧 `BattleVfxFollowController` 的世界偏移语义；该路径在第二批复用统一坐标转换服务时处理。

验收：Presentation 模板请求中的挂载跟随特效随来源移动；一次性定位特效不保留来源父节点；运行时缩放来源更新不会重新解析位置。Unity EditMode 回归需运行后方可关闭本批。

### 第二批：显式偏移空间与父缩放策略

- 在模板 Excel 增加 `OffsetSpace` 与 `ParentScaleMode`。当前稳定值为 `OffsetSpace: TargetLocal=0 / World=1`、`ParentScaleMode: Inherit=0 / Ignore=1`；旧配置缺省仍按目标局部偏移、继承父缩放解释。
- Presentation attachment 已支持世界偏移与目标局部偏移；持续跟随的世界偏移会在运行时按目标当前位置校正，避免父子层级让偏移随目标旋转；Unity 后端已实现忽略父级缩放的补偿，并按来源缩放刷新时重新计算补偿。
- 增加配置导出校验，拒绝未定义 enum 值；Luban DTO/二进制/双端发布仍需经本批发布命令生成并验证。
- 逻辑 VFX 的 `BattleVfxFollowController` 当前仍是独立的位置同步实现。本批先保证 Presentation 模板路径语义闭环；后续应把两个路径改为调用共享坐标转换器，避免维护两套定义。

验收：世界偏移不受目标旋转/缩放影响；局部偏移保留旧 `TransformPoint` 行为；忽略父缩放时视觉世界尺寸稳定。非均匀缩放叠加旋转可能产生 Unity 层级剪切，需在 EditMode 验收确认并明确支持边界；缺失 Socket 与目标销毁场景按既有 fallback/lifecycle 规则验收。

实现边界：`ParentScaleMode=Ignore` 通过目标父级 `lossyScale` 对子对象局部缩放做分量补偿，适用于常见的均匀父缩放。父级链含非均匀缩放与旋转时，Unity Transform 可能形成剪切，单纯分量补偿不能保证完全抵消；若 EditMode 验收要求覆盖该组合，应改为不挂父节点并通过跟随控制器更新世界变换，或约束目标层级避免剪切。

### 第三批：抽象模板级渲染排序（已实现）

- 工作簿使用 `OrderingMode` 与 `OrderingPriority` 表达资源默认、相对挂载对象、显式优先级三种语义，不配置后端专属 layer/order/z 值。
- 通用 Presentation 请求携带排序语义；Unity 后端通过可替换的 `IPresentationOrderingAdapter<GameObject>` 应用策略，保留资源 Sorting Layer 与 Renderer 间的相对差异。
- ActionEditor 模板解析与 Trigger/Cue VFX 路径都按模板 AssetId 解析同一排序配置；Cue 快照协议无需扩展。
- `ResourceDefault` 恢复资源基准排序。配置缺省值为 0，旧模板保持资源默认表现。

验收：配置和通用请求不依赖 Unity 排序类型；两条主要播放路径读取相同模板排序语义；适配器可注入替换；未配置资源保持 Prefab 原默认行为。

### 第四批：Lifecycle 与作者化完善（已实现）

模板新增三列，旧行均填 0；既有 StackPolicy/StopPolicy 也已纳入导表枚举校验：

| 字段 | 稳定取值 | 作用 |
|---|---|---|
| StackPolicy | 0=Independent，1=ReplaceSameKey，2=RejectSameKey | 以创建时的 LogicalKey 与来源 ActorId 为键；更新请求不改变该身份 |
| StopPolicy | 0=Immediate，1=Drain | 显式停止立即回收，或停止产生新粒子后等待尾部结束 |
| TargetLossPolicy | 0=BackendDefault，1=Stop，2=Detach，3=Rebind | 兼容旧后端、立即结束、保持最后世界姿态并永久脱离、保持姿态等待同 ActorId 重新解析 |
| CompletionPolicy | 0=Duration，1=ResourceOrDuration，2=Manual | 使用播放时长、资源完成与时长任一先到、只接受显式停止 |
| StopGraceMs | 非负整数；0=默认 3000 ms | Drain 的非缩放时间上限，避免暂停或资源不报告结束时无限残留 |

`PresentationLifecyclePolicy` 和 `PresentationLifetimeState` 是无引擎依赖的契约。`IPresentationLifetimeAdapter<TVisual>` 负责资源完成与排空能力，可以替换；Unity 默认实现支持粒子完成、Timeline 完成和粒子排空。没有可检测的资源组件时，ResourceOrDuration 使用配置时长；不支持 Drain 的资源在显式停止时立即回收。配置时长为 0 且资源无法完成的实例必须显式停止，检查器会提示。

两条播放路径均读取模板生命周期：ActionEditor 模板请求携带 Lifecycle/TemplateId，Trigger/Cue 按 TemplateId 精确查找；仅有 AssetId 的旧请求只在映射唯一时使用模板，歧义时保留资源默认策略。独立多来源不通过反复改写共享实例实现，每个实例保留创建身份。RejectSameKey 忽略正在排空的旧实例；资源级 Unique/MaxActive 仍会计入尾部，防止绕过总体数量限制。ReplaceSameKey 先检查资源频率/数量门禁，再强制回收旧实例；门禁拒绝新请求时保留旧实例。

生命周期优先级：

1. 世界/场景清理、回滚后清除无效表现、替换同键实例等强制销毁立即回收，优先于所有尾部策略。
2. TargetLossPolicy=Stop 立即回收；Detach 保留最后世界姿态并停止重绑；Rebind 保留世界姿态，继续通过 ActorId 解析。显式丢失策略使用持久表现根进行软件跟随，避免目标 GameObject 销毁连带删除子特效。宿主必须让该根比目标存活更久。
3. 显式 Stop 按 Immediate/Drain 执行；重复 Stop 不重置排空计时。Drain 使用资源完成或非缩放超时；已进入排空的实例不再接受播放更新。
4. 自然完成按 CompletionPolicy 执行并回收，时长结束不自动追加 Drain 尾部；Manual 忽略自然完成。

池回收统一走 DestroyVfxEntity，清除请求身份及数量占用；GameObject 回池恢复 Drain 修改过的 Animator 启用状态并清除残留粒子。新实体创建全新的生命周期状态，不继承旧的停止/脱离标记。外部已销毁实体的策略占用在 Tick 清理。

快照与回滚只恢复权威表现请求及其键，不序列化本地粒子、Drain 计时或 Unity 对象引用。Cue Unity 端口实现可选的 `IPresentationCueCleanupPort<THandle>`：注册表 Clear 与 ReconcileActiveSet 强制回收失效实例，并清除此前普通 Stop/瞬态播放留下、已退出活动键集合的 Drain 尾部；期望集合中的活动实例保留。清理失败保留引用以供重试。不实现该可选接口的旧端口仍按原 Destroy 契约处理。通用 Unity 后端 ClearAsync 同时推进加载代次，清理前开始、尚未完成加载的请求不会在清理后重新生成对象。本批没有增加粒子时间游标恢复，也不让表现完成影响战斗逻辑。

只读作者入口：Unity 菜单 `Tools/AbilityKit/Demos/Moba/Vfx/Inspect Published Templates`。窗口读取已发布模板，可选择来源对象，检查挂点 fallback、持续跟随、偏移空间、预计世界位置、Prefab × 模板 × 本次倍率 × 来源合成倍率、父缩放、抽象排序、生命周期与资源能力告警。来源倍率由作者输入合成值；窗口不冒充运行时来源账本，也不显示后端专属排序数值。修改仍在 Excel 权威源完成。

第四批时尚未接入的 ActionEditor 选中 Clip、运行时实例诊断与旧 BattleVfx 空间语义收敛，由下述第五批实现。

### 第五批：空间语义收敛与运行时尺寸来源诊断（已实现）

`UnityPresentationTransformUtility` 统一 Socket 路径查找、局部/世界偏移求值，以及世界尺寸到宿主局部缩放的换算。两条请求路径均从稳定 Prefab 基础缩放重新求值：

```text
内容尺寸 = Prefab 基础缩放 × 已解析请求 Scale × 正 Radius（未配置取 1）× 适用来源账本
世界尺寸 = 内容尺寸 ×（持续跟随且 Inherit 时的来源世界缩放，否则为 1）
对象 localScale = 世界尺寸 / 实际宿主世界缩放
```

`Scale` 在请求生成阶段已经合并模板与本次倍率；诊断不会把模板倍率再乘一次。Radius 继续保持旧 BattleVfx 正值作为尺寸因子的消费方式，通用 Unity 后端现与其对齐；这里没有把 Radius 改成播放速度或逻辑实体半径。

请求驱动的 BattleVfx 通过跟随控制器更新世界变换，保留在持久表现根下；通用后端保留现有默认挂载兼容方式。两者有相同的目标/偏移/尺寸语义：

- 优先使用 Binder 的渲染模型壳，其次使用实体的 GameObject；Socket 仅用 `Transform.Find(path)`，缺失回退到来源根。没有模型壳时，BattleVfx 使用插值位置或逻辑位置、逻辑 Forward 与 Scale；此时不能提供骨骼挂点，回退到逻辑投影根。
- TargetLocal 偏移包含目标旋转与缩放，World 偏移保持世界轴；持续跟随在目标尺寸变化后也会重新计算尺寸补偿。
- `Follow=false` 仅在创建时尝试定位，缺失目标使用请求世界位置，不在之后晚绑定，也不继承来源尺寸。来源销毁不结束这种一次性定位实例。
- Detach 保留最近目标世界尺寸；Rebind 等待相同 ActorId；缩放来源增删不使用上一次 Transform 作为基准。
- 无 `PresentationRequest` 的旧 `TryCreateVfxEntity` / `SyncFollow` 接口保留世界偏移约定，避免把旧调用点静默解释为局部偏移。
- 跟随过程中排序仍交给可替换的 `IPresentationOrderingAdapter<GameObject>`，相对排序以最终 Socket/Actor 目标求值；空间转换工具不包含排序实现。

池复用保留 Prefab 基准，回池清除实例尺寸来源、请求诊断及停止/脱离状态，避免上一次临时缩放污染下一次播放。

检查器仍使用 `Tools/AbilityKit/Demos/Moba/Vfx/Inspect Published Templates`：

1. “模板估算”增加“读取 ActionEditor 选中片段”和“跟随 ActionEditor 选择”，通过现有选择 API 读取 `PlayParticle.presentationTemplateId`。未选中该类型、未配置或未发布的模板会明确提示；预览来源对象与额外倍率仍由作者输入，不把它们伪称为运行时值。
2. “运行时实例”接受场景对象或子节点。两条后端使用 `UnityPresentationInstanceState` 提供实际请求、基础尺寸、有效挂点、最近目标缩放、停止/脱离标记、排序语义与真实 Transform。账本按 SourceId 排序显示各来源的向量、通道、Multiply/Override 和优先级，同时显示当前通道合成值；不适用通道会标记。BattleVfx 的 cue.request / cue.radius 已在账本中，通用后端在账本外应用，两者均只计入一次。
3. 选择模型/Avatar 时可读取 `BattlePresentationScaleState` 的 Prefab 基础缩放、逻辑实体缩放、模型来源与实际 Transform。诊断使用复制的来源快照，不向外暴露可修改字典，也不改动配置。

本批支持以真实实例诊断运行时结果，未把 ActionEditor 粒子预览器改造成完整战斗后端，也未恢复粒子时间游标。非均匀缩放与旋转产生的层级剪切、零缩放父节点的不可逆转换仍是 Unity Transform 边界；除数接近零时相应局部分量取 0，不能承诺保持非零世界尺寸。

下一方向：统一 ActionEditor 粒子预览与运行时资源解析/挂点转换；运行时诊断增加来源所属系统和覆盖获胜原因；再按实际需求增加 Position / PositionAndRotation 跟随模式。排序继续通过后端适配器替换。

## 配置演进与发布约束

生产编辑只改 `LubanConfig/Moba/Production/Datas/T-特效表.xlsx`，不手改 `Resources/luban`、生成 DTO、`.bytes` 或 `Resources/vfx/vfx.json`。修改表结构时先更新 Luban bean/enum 和业务校验，再由 `python LubanConfig/Moba/export_pipeline.py --apply` 发布 Unity 与 Console 两端，最后运行 `--check-published` 和受影响表现验收。由于现有工作簿和多个发布目录可能包含其他在途修改，实施时只提交本特效体系涉及的文件，不回滚无关工作区差异。

## 第五批验证记录

- 无工作簿、Luban schema 或发布产物变更，本批不重新发布配置。
- `AbilityKit.Demo.Moba.Presentation.Core.Tests`：131 项通过，新增来源快照排序、通道过滤与不可回写验证。日志 `local/presentation-spatial-core.log`。
- Unity 2022.3.62f1 实际编译运行时与检查器；专项 19/19 通过（5 个空间/复用用例、10 个生命周期用例、4 个既有通用 Unity 后端用例）。空间一致性用例在内部覆盖 TargetLocal/World × Inherit/Ignore 四种组合；还覆盖来源移除、目标动态缩放、连续刷新不累乘、缺失 Socket、逻辑缩放回退、一次性定位、旧接口世界偏移及池复用。结果 `local/Logs/presentation-spatial-final.xml`。
- 专项验证使用无图形 EditMode；检查器编译通过，未做人工窗口交互或最终粒子画面验收。下述第四批扩展回归和七英雄冒烟失败仍是历史记录，本批未重跑或宣称修复。

## 第四批验证记录（历史）

- `python LubanConfig/Moba/export_pipeline.py --apply` 已发布配置；随后 `--check-published` 通过，Presentation 模板共 36 行。
- 已把抽象排序策略接入 Trigger/Cue 消费路径；Unity 排序实现是可注入适配器，模板语义未绑定具体排序 API。
- `AbilityKit.Demo.Moba.Presentation.Core.Tests`：130 项通过，涵盖生命周期优先级、多来源身份、替换门禁预检、强制回收活动实例与已注销尾部、清理失败重试。
- `AbilityKit.Demo.Moba.View.Runtime.Tests`：198 项通过；`MobaLubanConfigPipelineTests`：24 项通过，包含 JSON 与二进制生命周期字段投影。
- Python 导出管线单测：131 项通过，包含新增字段、枚举范围和停止超时整数校验。
- Python 导出管线单测在本仓库需要通过 `LubanConfig/Moba` 目录执行，并把该目录加入 `PYTHONPATH`；从其他目录直接按模块名启动会因测试导入方式而找不到 `export_pipeline`。
- Unity 2022.3.62f1 已实际编译运行时与编辑器检查器。生命周期专用 EditMode 用例 10/10 通过，覆盖并发加载下的同键替换/拒绝、Drain 完成与非缩放超时、目标丢失 Stop/Detach/Rebind、池状态恢复、管理器策略占用清理与复用，以及清理后旧异步加载不生成实例。最终专用测试结果见 `local/Logs/presentation-lifecycle-final.xml`。
- 扩展 Unity 回归共 132 项：120 通过、12 失败。其中当时的生命周期专用用例 9/9 通过；其余失败涉及缺少 dungeon_context 的 Feature/Profile、大厅自动进入预期、Cue 瞬态/持久命令预期、未注册实体与烬普攻动作槽位。未做干净基线对照，不能把这些失败全部认定为原有问题。详情见 `local/Logs/presentation-lifecycle-regression.xml`。
- 七英雄真实技能冒烟用例 `Luban_seven_heroes_spawn_and_cast_first_skill`：0/7 通过。所有用例均在启动校验阶段被 7 条 `moba.skill.contract.invalid_trigger_scope` 阻断，涉及技能 10010101/10010201/10010301/10070101 和区域 40070101/40080101：调用点要求 Global，但发布 Trigger 为 OwnerBound。该校验先于特效播放，本批未修改这些技能/区域机制来绕过校验；详情见 `local/presentation-seven-heroes-detail.log`。
- 该批验证不等于整个工作区回归全绿。旧 BattleVfx 请求路径的位置语义现由第五批收敛；非均匀父缩放剪切、动画资源完成检测与粒子时间游标恢复仍按上文边界处理。

### 第六批：ActionEditor 预览与运行时解析统一（已实现）

- ActionEditor 粒子预览改为可扩展基类：通用层只处理资源加载、实例生命周期、确定性粒子种子、多粒子根节点模拟和时间轴正向/反向采样，不依赖 MOBA 配置程序集。
- MOBA 编辑器通过专用预览子类调用 `MobaPresentationTemplateResolver` 与 `UnityPresentationTransformUtility`，资源优先级、模板 Scale/Radius、OffsetSpace、ParentScaleMode、Follow、Socket 精确路径及 Ordering 语义与运行时一致。模板资源覆盖、VFX ResourceKey、旧 Resource 的优先级保持同一解析规则。
- 挂载预览不把实例永久挂入模型层级，持续跟随按运行时请求每次刷新位置、旋转和父缩放补偿；一次性特效只在进入时定位，源模型之后移动或缩放不会反向改变实例。缺失挂点按共享工具回退到来源根，来源缺失时保留请求世界位置。AttachMode=World 时即使遗留 Socket 字段也不会误判为 Socket 挂载。
- ActionEditor 的 `AssetPlayer`、时间轴窗口关闭、资源切换、停止播放、程序集重载和 PlayMode 切换都会释放预览实例，避免隐藏对象和粒子状态泄漏到下一条时间轴。
- 新增编辑器回归覆盖资源优先级、四种偏移/父缩放组合、一次性/持续跟随、挂点缺失与来源缺失、Ordering 抽象适配器、多粒子根节点采样和反向擦除。

边界：预览仍是编辑器采样器，不替代运行时资源加载、池化和生命周期后端；ActionEditor 未配置模板时继续使用 `resourceKey` 或 `resPath` 的通用路径。

### 第七批：运行时来源账本与覆盖解释（已实现）

- `PresentationRateResolver` 新增 `Explain` 与稳定来源快照，报告目标通道的乘法合成值、最终 Override/Pause 控制来源、优先级、模式和来源数量。
- `PresentationScaleResolver` 新增 `Explain`，报告基础值、乘法合成、最终覆盖来源、优先级和来源数量；仍按来源 ID 替换，重复刷新不会累乘。
- `UnityPresentationInstanceState` 现在同时保存速率解析器、速率解释、缩放解释、最终 Ordering 语义和可替换 Ordering Adapter。诊断读取的是实际运行时账本，不把后端 SortingLayer、Renderer order 或 Z 值提升为模板契约。
- Unity 通用表现后端在 Set/Remove 速率、Set/Remove 缩放、请求更新时刷新诊断；BattleVfx 实例也绑定同一类速率/缩放解释数据。表现数值计算路径未改变，诊断不会反向修改请求。
- 新增核心契约测试，覆盖来源快照稳定排序、Override/Pause 控制选择、缩放 Override 与 Multiply 解释；本批次沿用既有空间、生命周期、ActionEditor 预览回归。

边界：当前解释面向实例级最终值，尚未把每个后端适配器内部的粒子/Animator 属性逐项展开；后续可在来源账本稳定后增加属性级消费报告。
