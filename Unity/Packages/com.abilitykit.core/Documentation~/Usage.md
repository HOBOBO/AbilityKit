# Core 首次使用与契约导航

> 文档类型：使用指南
> 适用范围：当前 0.1.0 源码；Unity 最低版本及依赖以包内 package.json 为准。
> 核校状态：已核对下方标识、缓冲区与释放句柄示例及其源码；其他 API 按对应专题的范围阅读。
> 事实基线：2026-11-24；不包含 Player 或跨运行时位一致性验收。

## 安装与前置条件

从项目约定的包来源安装 Core，并确保依赖的 `com.abilitykit.deterministic` 可被同一项目解析。版本和依赖以 [package.json](../package.json) 为准；不需要安装 Demo 包。

包内纯运行时程序集为 `AbilityKit.Core`；Unity 项目使用自定义 asmdef 时，应显式引用所用类型所属程序集。直接使用 `Fixed64` 等类型时还需引用 `AbilityKit.Deterministic`。本包不提供正式 NuGet 安装地址，仓库中的 .NET 镜像与本地验证制品不能作为已发布渠道。

## 最短使用示例

下面是完整类，可从宿主调用 `CoreQuickStart.Run()`，验证本地名称注册、租用缓冲区和单次释放句柄。

```csharp
using System;
using AbilityKit.Core.Buffers;
using AbilityKit.Core.Identifiers;
using AbilityKit.Core.Lifetime;

public static class CoreQuickStart
{
    public static string Run()
    {
        var ids = new StableStringIdRegistry();
        int first = ids.GetOrRegister("ability.cast");
        bool sameId = first == ids.GetOrRegister("ability.cast");

        using var buffer = PooledBufferOwner<int>.Rent(2);
        buffer.Span[0] = 7;
        buffer.Span[1] = 11;
        int sum = buffer.Span[0] + buffer.Span[1];

        int released = 0;
        using var registration = DisposableRegistration.Create(() => released++);
        registration.Dispose();
        registration.Dispose();
        return $"{sameId}:{sum}:{released}";
    }
}
```

预期结果为 `True:18:1`。方法退出时缓冲区归还，句柄的再次释放不会重复回调。示例只访问已写入的逻辑元素，不依赖池内数组初始值。

## 使用契约

| 能力 | 调用方需要遵守 | 详细说明 |
| --- | --- | --- |
| 名称注册 | 使用布尔返回值判断查询是否成功；哈希可能冲突，不能视为全局唯一 ID | [稳定标识与计时](IdentifiersAndTiming.md) |
| 租用数组 | 一个逻辑所有者负责释放；释放后已取得的 Memory、Span、Segment 均不可继续使用 | [集合与缓冲区](CollectionsAndBuffers.md) |
| 释放句柄 | 回调最多执行一次；回调异常向调用方传播，后续释放不重试 | [释放句柄源码](../Runtime/Lifetime/DisposableRegistration.cs) |
| 数学与桥接 | 浮点视图不用于无损回存 raw 值；中间运算可能溢出；不据此承诺跨目标位一致 | [数学原语](Mathematics.md)、[定点桥接](DeterministicMathBridge.md) |
| 对象池与事件 | 按实际入口确认回调顺序、异常及资源所有权；不以全局门面替代宿主作用域 | [对象池](Pooling.md)、[事件](Eventing.md) |

`DisposableRegistration` 支持并发释放，不表示回调中访问的业务对象也线程安全。`PooledBufferOwner` 的释放可以竞争，但访问视图不得与释放并发；名称注册表由调用方同步。

## 限制、兼容与排错

- 缺少 Deterministic 程序集：核对包依赖解析和消费方 asmdef 引用，不复制定点源码到 Core 或业务包。
- 缓冲区释放后访问抛错：将读取限制在所有者生命周期内；不要缓存跨越释放边界的视图。
- 注册名称发生冲突：保留原始名称并按所属协议设计处理；不改变既有哈希算法来隐藏冲突。
- 新项目先看[首发支持清单](FirstReleaseSupport.md)。Continuous、Config、Reflection、Markers、DebugDraw 等兼容入口不作为默认接入，迁移说明从[文档索引](README.md)进入。

本页示例在本轮使用真实 .NET 镜像程序集编译与执行；支持环境和独立安装限制见[消费范围说明](IndependentConsumption.md)。完整游戏装配、仓库测试和候选发布由仓库文档维护，不属于本页最短接入路径。
