# AbilityKit World Network Fragments

`com.abilitykit.world.networkfragments` 提供世界帧包、远端帧缓冲和同帧聚合数据结构，供宿主适配层使用。它不收发网络、不解码玩法快照，也不证明输入帧已经齐备。

## 安装与首次使用

安装条件、Unity 版本和直接依赖见 [package.json](package.json)。按接收作用域创建缓冲，不让不同世界或会话共用只按帧号索引的实例。

```csharp
using System;
using AbilityKit.Ability.Host;

using var frames = new RemoteFrameBuffer<string>();
frames.Add(3, "local frame");
if (!frames.TryGet(3, out var value) || value != "local frame")
    throw new InvalidOperationException("帧未保存");
frames.TrimBefore(4);
```

裁剪后第 3 帧不存在，但 `MaxReceivedFrame` 仍为 3；这是收件高水位，不是连续帧或可执行帧证明。

- [文档索引](Documentation~/README.md)
- [使用契约](Documentation~/Usage.md)：覆盖、裁剪、聚合与载荷所有权。
- 历史说明保留在 `Document/`，不作为新接入的默认契约。
