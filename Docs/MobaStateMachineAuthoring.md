# MOBA 状态机创作与发布

角色 HFSM 和投射物状态机的编辑源位于
`Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaStateMachineAuthoring/`。
使用 **Window > AbilityKit > MOBA 状态机编辑器** 查看状态、转换并修改当前生产配置。
编辑器保存前调用运行时解析器校验；**发布**会写入 Unity 与 Console 的
`moba/character_hfsm.json`、`moba/actor_state_machines.json`。

角色 HFSM 使用 `StateMachineDefinition`，投射物使用
`HierarchicalProfile<MobaActionSpec>`；两种结构不能互相代替。投射物表中的
`StateMachineProfileId` 引用投射物状态机配置 ID，导表时会校验该引用。
状态机图不再从 `J-角色表.xlsx` 导出。技能数值、投射物参数和效果数值仍由
`LubanConfig/Moba/Production/Datas/` 管理。

批处理入口：

```powershell
# 从 Unity 工程执行以下 -executeMethod
AbilityKit.Ability.Impl.BattleDemo.Moba.Editor.MobaStateMachinePublisher.Publish
AbilityKit.Ability.Impl.BattleDemo.Moba.Editor.MobaStateMachinePublisher.Check
```

完整发布后执行 `python LubanConfig/Moba/export_pipeline.py --check-published`。
不要直接编辑 `Resources/moba` 或 Console 副本。旧 `effect_plans` 已停止加载；
事件条件和动作在 Trigger Authoring 编辑，普通 `effects` 数值表仍由 Luban 发布。
