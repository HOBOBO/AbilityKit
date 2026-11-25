# 预测视图实例

`BattleSessionFeature` 现在为远端驱动的预测世界提供可选的侧视图：

```csharp
var shown = battleSession.ShowPredictionView("comparison-left", new Vector3(30f, 0f, 0f));
// 稍后，在会话仍活动时：
battleSession.HidePredictionView("comparison-left");
```

其他世界可提供自己的 `IBattleProjectionViewSource`（稳定的 `WorldId` 和
`TryGetProjection(out producer, out frame)`），并用
`ShowProjectionView(instanceId, source, worldOffset)` 注册视图。
每个实例读取自己的来源；`TryGetProjectionView(instanceId, out info)` 返回其角色、来源世界、
偏移及有效能力。`GetProjectionViews(buffer)` 列出全部活动实例，
`RebindProjectionViews()` 刷新绑定器，`HideProjectionView` 可移除任一角色的实例。
来源暂时不可用时，清除其投影角色，但保留实例注册。

投影视图默认仅绑定角色。向 `ShowProjectionView` 或 `ShowPredictionView` 的重载传入
`BattleProjectionViewCapabilities`，可增加特效、区域效果、飘字、事件或相机控制。
选择 `Events` 还会安装事件接收器所需的特效、区域和飘字处理器。
投影视图不订阅主战斗的快照或触发器流。

来源可实现 `IBattleProjectionViewEpochSource`，在世界重建时报告新的纪元，包括复用同一生产者对象的情况。
实例随后清除旧角色和临时效果，再重新填充。
若要传递事件，实现 `IBattleProjectionViewEventSource.SubscribeEvents`。
订阅接收实例 ID、世界偏移、纪元和当前帧；来源必须把位置事件映射到该实例的偏移。
来源不可用、纪元变化或实例关闭时，释放返回的订阅。
注册时执行首次同步，使订阅在下一次表现步进前生效。
请求 `Events` 但未提供事件来源时返回 `false`。

在远端驱动世界启动后调用 `ShowPredictionView`。
客户端预测或其角色投影生产者不可用，或 ID 已被使用时，返回 `false`。
不同 ID 的视图可同时显示。偏移应用于侧视图投影角色的位置，主视图保持正常坐标。
`PredictionViewCount` 统计预测角色实例，`ProjectionViewCount` 统计全部投影实例。

每个侧视图独立拥有表现 `EntityWorld`、角色查找表、功能运行时、资源提供者、层级根节点和表现对象池。
功能调度器可推进多个同类型投影视图，实例注册表负责查找所有实例。
已确认视图的功能有独立 CLR 类型，因此按类型进行调试查找时，不会将投影实例误认为已确认实例。
恢复过程中释放远端驱动世界时，会清除预测角色但保留视图注册；来源重新可用后，视图重新填充。
辅助视图保留自身角色。来源可用性在表现步进时检查。
停止战斗会话会释放全部投影视图实例。

投影在每次表现步进中使用 `IActorProjectionProducer.ExtractAll`，并对新观察到的角色使用 `ExtractSpawn`。
它创建或移除表现实体，更新变换、队伍、生命和模型身份，随后由既有表现绑定器直接渲染，不执行插值。
既有本地玩家预测桥接继续驱动主视图。

当前范围：`ActorProjectionData` 不携带动画或技能提示。
启用事件的来源必须通过自己的事件订阅提供这些信息；仅有角色投影时，只显示角色状态。
投影没有角色代际字段，因此同一世界纪元内，若同一角色 ID 在两次采样步进间被销毁并复用，
此视图自身无法区分。
