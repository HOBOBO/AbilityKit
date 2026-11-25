# MOBA 共用瞄准释放快照基础能力

> 状态：已实现基础闭环，服务七名资源英雄的后续技能复刻。

## 目的

技能按钮的最后一次 `PointerUp` 可能和上一帧拖拽位置不同。若释放时只提交坐标而不刷新预览，玩家看到的指示器会停在旧位置，而权威技能却按新位置执行。点地技能（烬 E、维迦 W/E、古拉加斯 Q/R、拉克丝 E、安妮 R）和方向技能（烬 W、金克丝 W/R、卡尔萨斯 W、拉克丝 Q/W/R、安妮 W）都会暴露这个差异。

## 统一流程

`BattleHudSkillAimInputMapper` 将按钮输入投影到相机平面；`BattleHudInputEventDispatcher` 再依据技能表现契约处理输入：

1. `Start/Update` 写入当前瞄准状态，驱动 HUD 预览。
2. `End` 使用同一套 `ResolveWorldAim` 规则得到释放快照。点地模式按技能 `Range` 进行方向归一化和距离裁剪，方向模式保持方向输入。
3. 释放快照先调用 `SetHudSkillAim(..., aiming: true)`，让预览立即切换到最终落点/方向，再调用 `SubmitHudSkillAim` 进入权威技能链。
4. 取消、无效技能和提交失败仍由原有清理流程关闭预览；该基础能力不改变技能的 Area 半径、Projectile 碰撞宽度或目标查询规则。

## 验收要求

- 点地：`ActiveAim` 与 `SubmittedAim` 必须逐分量相等，且使用 `Range` 裁剪后的世界坐标。
- 方向：释放方向必须先写入预览，再提交；Release 与最后一帧 Drag 不同的场景必须覆盖。
- 失败/取消：不得产生技能提交，且瞄准状态必须清理。
- 权威几何：HUD 预览半径/宽度只消费技能表现配置；最终 Area、Projectile、TargetQuery 仍以运行时权威配置为准，不能由指示器反推玩法。

当前测试覆盖 `BattleHudInputEventBridgeTests` 的点地缩放、方向保持和释放快照刷新。后续每个英雄进入机制实现批次时，应将该测试作为共同输入前置验收，再补该英雄独有的目标过滤、碰撞、召唤物、护盾和表现断言。
