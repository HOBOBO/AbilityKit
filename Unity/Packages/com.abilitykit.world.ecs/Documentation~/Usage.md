# World ECS 使用契约与限制

安装条件及最小示例见[包入口](../README.md)。

- `IEntityId` 由槽位索引和版本组成；应在所属 `IECWorld` 使用，必要时调用 `IsAlive` 判断。不要把它当作跨世界或跨会话的稳定业务 ID。
- `Destroy` 不递归销毁子实体；明确需要带子树清理时用 `DestroyRecursive`。销毁不会自动 `Dispose` 组件持有的资源，资源与订阅释放由上层负责。
- 组件可经 `SetComponent` 写入值类型，或经 `SetComponentRef` 绑定引用类型；在同一个预期线程上操作世界，不把本包当作并发安全的完整 ECS 调度框架。
