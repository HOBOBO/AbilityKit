# MOBA 行为树节点颗粒度与召唤物复用边界

## 结论

当前 Tibbers 使用的行为树已经按“感知事实、候选选择、条件判断、瞄准解析、意图请求、意图仲裁”拆分，没有发现 `Tibbers`、Annie 技能编号或召唤物编号写入运行时节点的情况。`summon_warden_bt` 是由通用节点组合出来的策略配置，不是一个包含完整追击、攻击和生命周期的大节点，因此可以作为其他召唤物的起点复用。

当前拆分足以支撑“选敌人、选择可用技能、接近、施法、保持”以及无敌人时跟随主人的基础召唤物行为。行为树仍未表达更完整的拴链和受击反击策略；这些是缺少通用能力或配置的表现，不应通过增加 Tibbers 特供节点解决。主人死亡处理继续由召唤生命周期服务负责。

## 代码与配置依据

运行时节点位于：

- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Behavior/BTree/MobaBTreeNodes.cs`
- `Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Application/Services/Behavior/BTree/MobaHeadingNodes.cs`

运行时适配和生命周期边界位于：

- `MobaBTreeDecision.cs`：每帧刷新事实、运行树、读取黑板意图。
- `MobaBTreeRuntimeContext.cs`：注入世界、注册表、搜索、技能选择策略和技能槽服务。
- `MobaBrainService.cs` / `MobaBrainSystem.cs`：为英雄和召唤物创建 Brain。
- `MobaSummonService.cs` / `MobaSummonLifecycleSystem.cs`：召唤、继承属性、超时、死亡和销毁。
- `MobaCharacterHfsmSystem.cs`：召唤物使用通用角色 HFSM。

导出源是 `tools/bt-export/authoring/moba/summon_warden_bt.json`。该树只由内置 `sequence`、`selector` 和 MOBA 叶节点组成；没有在 JSON 中写入 Tibbers ID、Annie ID 或英雄特化参数。

## 当前节点分层

| 层级 | 节点 | 单个节点的职责 | 复用判断 |
| --- | --- | --- | --- |
| 感知/事实 | `selectNearestEnemy`、`selectOwner` | 调用搜索服务或读取实体主人关系，提交目标事实 | 可复用；主人位置每次评估刷新 |
| 候选选择 | `selectReadySkill` | 从技能槽筛选冷却就绪技能，应用注入的选择策略 | 可复用；不识别英雄或召唤物身份 |
| 条件 | `hasEnemy`、`noEnemy`、`hasOwner`、`hasSelectedSkill`、`canCast`、`canMove` | 只读取黑板事实 | 可复用、可组合 |
| 条件 | `selectedSkillInRange`、`shouldApproachEnemy`、`shouldFollowOwner` | 比较目标距离和候选技能距离或跟随停止距离 | 可组合；距离策略由树属性配置 |
| 解析 | `resolveTargetAim` | 把目标事实转换成瞄准点和方向 | 可复用 |
| 解析 | `resolveHeadingAim` | 把生成方向或当前朝向转换成方向瞄准 | 可复用 |
| 意图请求 | `castSelectedSkill`、`moveToEnemy`、`moveToOwner`、`moveAlongLaunchHeading`、`holdPosition` | 只写对应的 `intent.*` 请求 | 可复用；没有直接执行移动或技能 |
| 仲裁 | `arbitrateCombatIntent` | 按优先级把请求复制到唯一 `out.*` 输出 | 可复用；保证多个分支不依赖执行顺序 |
| 生命周期 | 不在行为树节点内 | 由召唤服务、死亡订阅、HFSM 和 Brain 管理 | 边界正确，避免树节点承担销毁职责 |

`MobaBTreeDecision` 只负责把输出意图交给 Brain 执行器。节点不直接改属性、生成召唤物、触发 VFX 或销毁实体，这一点保证了同一套树可以用于英雄和召唤物。

## 对 Tibbers 的判断

Tibbers 当前树的执行顺序是：

1. 搜索最近敌人并写入目标事实。
2. 选择一个冷却就绪技能（包括配置的普攻槽）。
3. 目标有效时优先尝试“技能可用、在射程内、解析瞄准、请求施法”。
4. 施法分支失败时，尝试“有目标、可移动、需要接近、请求移动”。
5. 仅当没有敌人、主人有效且超出 5 个战斗单位时，提交向主人移动的意图；移动执行层在进入该停止距离后结束移动。
6. 攻击和跟随分支都不能执行时请求保持。
7. 最后统一仲裁施法、移动和保持意图。

其中任何一步都没有读取 Tibbers 配置编号。Tibbers 的模型、动作、属性继承、死亡和召唤时效由召唤物配置及服务处理；行为树只消费实体当前可用的技能和世界事实。

## 需要补齐的通用能力

以下通用主人关系能力已经接入；其余行为继续通过通用能力补齐，不应创建 `TibbersFollowNode` 之类的专用节点：

1. **目标抽象**：增加 `selectTarget`、`targetInRange`、`moveToTarget` 的通用语义，并保留现有 `*Enemy` 节点作为兼容别名。目标来源由搜索模板决定，可以是敌人、主人、保护点或技能指定对象。
2. **主人关系（已接入）**：`selectOwner` 从 `ownerLink` 刷新主人位置、有效性和水平距离；`hasOwner`、`shouldFollowOwner` 和 `moveToOwner` 通过树配置组合，当前无敌人跟随停止距离为 5 个水平战斗单位，与移动执行层的 XZ 判定一致。
3. **拴链策略**：如需更复杂的返回区域、主人换场景等策略，再增加通用条件或查询规则；销毁动作最终由召唤服务执行。
4. **普攻候选策略**：允许技能选择策略按技能标签、槽位或优先级选择普攻和主动技能；节点本身不要写 BasicAttack 或 Tibbers 技能编号。
5. **目标失效刷新**：搜索节点应继续每帧清理并刷新目标事实；持续节点若以后引入，必须重新发出本帧意图，不能依赖上帧的瞬时输出。

## 不应继续增加的做法

- 在节点中判断 `summonId == 30030502`、英雄编号或 Annie 技能编号。
- 一个节点同时完成搜索、跟随、攻击、施法、伤害、生命周期和表现事件。
- 节点直接调用 `MobaSummonService` 生成或销毁召唤物。
- 通过修改黑板的 `out.*` 字段绕过唯一仲裁节点。
- 为每种召唤物复制一份 `summon_xxx_bt` 运行时节点代码；差异应留在树 JSON、搜索模板、技能槽和配置策略中。

## 验收标准

新增召唤物接入时，至少应满足：

- 只新增或修改行为树 JSON、技能槽、搜索模板和 Brain 配置，不改运行时节点代码。
- 行为树导出和 `TreeValidator` 校验通过，所有 `moba.*` 节点都能由 `MobaBTreeCatalog` 注册表解析。
- 一个非 Tibbers 的召唤物可以复用同一棵树，并能替换搜索模板或技能选择策略。
- 召唤物死亡、超时、主人死亡和场景清理仍由召唤生命周期测试验证。
- Unity 行为树边界编译检查通过；当前检查结果为 0 个错误（仅存在样例字段未赋值警告）。

因此，当前不需要把 Tibbers 行为再压缩成一个“大节点”，也不需要为了形式拆分已有的原子节点。主人跟随已通过通用主人事实、范围条件和移动意图接入；后续再按具体英雄需求扩展目标抽象和拴链策略。
