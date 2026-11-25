# World ECS 使用契约与限制

> 文档类型：使用指南
> 事实基线：2026-11-25，当前包源码。
> 核校状态：已核校以下身份、组件、查询和生命周期边界；Unity 执行与全部层级组合未验证。

安装条件及最小示例见[包入口](../README.md)。

- `IEntityId` 由槽位索引和版本组成；应在所属 `IECWorld` 使用，必要时调用 `IsAlive` 判断。不要把它当作跨世界或跨会话的稳定业务 ID。
- `Destroy` 不递归销毁子实体；明确需要带子树清理时用 `DestroyRecursive`。销毁不会自动 `Dispose` 组件持有的资源，资源与订阅释放由上层负责。
- 组件可经 `SetComponent` 写入值类型，或经 `SetComponentRef` 绑定引用类型；在同一个预期线程上操作世界，不把本包当作并发安全的完整 ECS 调度框架。

## 参数、读取与失败

Builder 默认初始容量为 64、最大容量为 `int.MaxValue`；容量参数必须为正，达到最大容量后继续创建会抛异常。默认组件注册表为共享 `ComponentRegistry.Shared`，也可显式注入。

`IsAlive` 只检查槽位和版本，没有世界身份字段。因此“在另一个世界也返回真”不能证明 ID 属于该世界；调用方必须保存所属世界。`Wrap` 遇无效 ID 会抛参数异常；无效 ID 的销毁和多数组件写入则无动作，不能把未抛错当作写入成功。

`GetComponent<T>` 缺组件时抛异常；可选读取使用 `TryGetComponent` / `TryGetComponentRef`。`SetComponentRef<T>(id, null)` 移除该引用组件。替换、移除或销毁只解除容器引用，不负责释放组件内部资源。

## 查询和层级

`Query<T...>().ForEach` 按内部索引的快照访问当前仍存活且匹配的实体；访问到的值组件是值副本，修改后须显式写回。内部索引使用 HashSet，不承诺确定性的遍历顺序；需要同步重演时由业务另定稳定排序，不将容器遍历顺序当协议。

父子关系不是资源所有权；`Destroy` 解除子实体的父关系并保留子实体，`DestroyRecursive` 才递归清理子树。不要自行建立环，也不要跨世界混用 ID。事件和访问回调在调用线上执行；避免在回调中重入复杂结构修改，不假设失败会恢复先前状态。

## 源码与验证范围

已对照 [EntityWorld](../Runtime/AbilityKit.World.ECS/Impl/EntityWorld.cs)、[Builder](../Runtime/AbilityKit.World.ECS/Builder/EntityWorldBuilder.cs)与[查询 API](../Runtime/AbilityKit.World.ECS/Core/EntityQuery.cs)。本轮只做源码核对及文档检查，未执行此包 Unity 示例或把 .NET 其他 ECS 适配器测试作为本包证据。
