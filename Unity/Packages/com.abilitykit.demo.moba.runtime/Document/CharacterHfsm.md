# MOBA 角色 HFSM 配置与恢复契约

默认图位于 `com.abilitykit.demo.moba.view.runtime/Resources/moba/character_hfsm.json`，通过
`DefinitionJson.Load` 校验和迁移，资源缺失时使用语义哈希一致的代码默认图。所有参与同一场
战斗的逻辑世界必须使用同一份 Definition；快照的 DefinitionHash 不匹配时必须拒绝恢复。

## 所有权

- 逻辑层 Entitas `CharacterHfsmComponent` 是角色长期状态的唯一裁决者。默认层级为
  `life(alive[action(idle/moving/casting/controlled)],dead)`，死亡优先于动作，控制优先于施法，
  施法优先于移动。技能运行实例、战斗规则和移动输入提供事实，HFSM 不再执行伤害、位移或技能
  Pipeline 的副作用。
- 每次动作进入记录角色 ID + 起始逻辑帧生成的动作实例 ID、起始帧、动作局部帧；施法还记录
  技能运行实例 ID 和技能 ID。状态图快照和这些字段必须一起恢复，禁止只靠状态名从零重放。
- 表现 ECS `BattleCharacterHfsmComponent` 仅恢复逻辑快照，不独立判定角色状态。
  `character_view_actions.json` 将完整状态路径映射到表现层的 Sequence/Wait/Play/Log；可按
  EntityCode 为某个角色替换指定 action 槽位，也可在运行中通过表现组件 ReplaceAction 替换。
  ReplaceAction 只更新表现绑定版本，不改变逻辑 DefinitionHash。PlayAction 不需要 Animator：
  无头 sink 可记录播放意图，Mono 有 Animator 时按局部帧与 TickRate 采样。frameCount 为 0 表示
  尚未提供 ActionEditor 导出的帧数；帧数仅用于表现采样，不决定技能 Pipeline 完成时间。

Sequence 顺序执行，Wait 秒数按战斗 TickRate 向上取整为逻辑帧；Log 只在配置的局部帧输出一次，
历史帧恢复不重放 Log。Play 意图可在快照恢复或运行中替换后从当前局部帧重新求值。默认 casting
行为为 Log + Wait(0.5 秒) 占位；EntityCode 1001 用 Play + Wait 的 Sequence 替换该 Wait 槽位。

## 扩展

新状态、子机及转移优先级应在 Definition 中配置；新事实通过采样系统与绑定条件接入。复杂
施法可以将 `casting` 替换为含蓄力/释放/后摇的子机，并在逻辑快照中保存相应阶段事实。移动施法
与上半身攻击同时存在时，不要用单一动作枚举覆盖两个通道：主动作机保留互斥的玩法裁决，
独立的表现层/Animator layer 投影叠加姿态；若两个通道都影响玩法，则分别引入确定性的逻辑
子状态机及回滚载荷，并定义明确的冲突优先级。

## 重连边界

本地回滚、冷重连及新版网关快照均使用同一个角色 HFSM 回滚 provider。MOBA 服务端仅在角色
HFSM 与推送帧号一致时，使用已有 Wire Push 的 Payload 通道附带完整角色状态。压缩至少节省
16 字节时使用 schema 3 / PayloadOpCode=11017 的 GZip 编码，否则仍使用 schema 2 /
PayloadOpCode=10017 的原始 JSON。客户端在重建逻辑 Actor 后、预测 rebase 前验证并导入，同帧
表现实体按局部帧 seek；坏载荷会请求重新同步，压缩解码限制为 4 MiB。旧 schema 1 仍可接收，
但没有精确角色动作恢复保证。每帧载荷均可独立解码，尚未加入跨帧差量编码；技能运行实例
的完整冷恢复属于另一份权威载荷，未随此角色状态载荷导入，
本地预测施法事实可能在两次权威推送之间与服务器不一致。
