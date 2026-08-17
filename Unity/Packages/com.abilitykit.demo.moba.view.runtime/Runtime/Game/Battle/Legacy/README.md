# 旧版战斗 API 边界

[`Legacy`](Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Legacy) 目录仅保留旧版战斗请求与传输接口的兼容定义。

约束：

- 新代码不得继续依赖 [`AbilityKit.Game.Battle.Legacy`](Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Legacy)。
- 需要兼容旧调用时，应在边界适配层转换到当前 [`AbilityKit.Game.Battle`](Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle) 请求模型。
- 后续 asmdef 分层阶段可将本目录移动到单独 Legacy 程序集或删除。
