# Svelto 上下文适配使用指南

> 文档类型：使用指南
> 事实基线：2026-11-25，当前包源码。
> 核校状态：部分核校；覆盖 DI 注册、提交与释放；无独立适配器 suite，未跑 Unity。

安装与依赖见[包入口](../README.md)。本包提供 `SveltoWorldContext` 和 `SveltoWorldModule`；没有独立的 `IWorld` 实现，也不会自动调度游戏 engines 或推进 world Tick。

## 完整本地上下文示例

```csharp
using AbilityKit.World.Svelto;

using var context = new SveltoWorldContext();
context.SubmitEntities();
```

此例只验证创建、空结构提交和释放。业务另行安装 engines、定义 entities/groups，并由宿主决定结构变更的安全提交点和逻辑执行顺序。

## DI 装配与所有权

把 `SveltoWorldModule` 加入宿主的 world 模块组合后，它注册单例上下文及 `ISveltoWorldContext` 别名，并以非拥有方式暴露 `EnginesRoot`、`EntitiesSubmissionScheduler`、`EntitiesDB`、`IEntityFactory` 和 `IEntityFunctions`。解析到的底层服务借用同一上下文；不要分别 Dispose 这些别名，也不要让它们逃逸到上下文释放之后。

直接构造上下文时，调用方拥有上下文。传入 `EntitiesSubmissionScheduler` 的构造重载同样由上下文释放该 scheduler，并非借用；不要在外部再次管理其释放。DI 的 Singleton 生命周期以所属 container 为边界，宿主应为世界隔离 container，避免多个世界误用同一个上下文。

## 提交与失败

`SubmitEntities()` 仅调用 scheduler 的结构提交，不能代替引擎执行或帧推进。由宿主在明确的单一执行线上调用，避免枚举和结构变更冲突。`EntitiesDB` 当前通过 `IUnitTestingInterface.entitiesForTesting` 取得，这是此适配器的实现方式，不是所有 Svelto 版本的一般 API 保证。

`Dispose()` 先标记 disposed，再依次释放 EnginesRoot 和 scheduler；Root 释放异常会跳过 scheduler，后续重复 Dispose 不会重试。宿主需要记录和处理该失败，不把清理视为逐项隔离的事务。释放后 `SubmitEntities` 抛 `ObjectDisposedException`，属性访问不统一做终态检查，借用方必须遵守生命周期。

## 核校范围与源码

本轮 .NET Svelto 镜像构建通过；上例另行本地编译执行。没有独立适配器 suite、Unity Player、业务实体、预测回滚或状态导入测试。框架提供的上下文注册不能证明业务系统的确定性，也不提供帧同步和状态同步之间的数据转换。

- [SveltoWorldModule](../Runtime/Svelto/SveltoWorldModule.cs)：DI 注册及非拥有别名。
- [SveltoWorldContext](../Runtime/Svelto/SveltoWorldContext.cs)：创建、提交与释放。
- [ISveltoWorldContext](../Runtime/Svelto/ISveltoWorldContext.cs)：公开能力面。
