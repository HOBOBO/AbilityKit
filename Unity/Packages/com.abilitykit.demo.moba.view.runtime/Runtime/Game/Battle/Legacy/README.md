# 旧版战斗 API 边界

本目录保留兼容定义：[旧 transport](IBattleLogicTransport.cs) 和[旧请求](Requests/)。新代码不直接依赖 `AbilityKit.Game.Battle.Legacy` 命名空间；需要兼容时，在边界适配后交给当前[客户端装配路径](../../App/Flow/Composition/)，不把旧 transport 作为新会话入口。
