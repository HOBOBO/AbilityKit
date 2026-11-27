# Collision Abstractions 使用指南

> 文档类型：使用指南
> 适用范围：0.1.0 当前 Runtime；环境与依赖见 [package.json](../package.json)。
> 核校状态：已核校本文的碰撞体生命周期、后端选择、查询输出与限制；旧说明保留背景。
> 事实基线：2026-11-25；E0 与局部 .NET E3，不外推 Unity、性能或跨平台验收。

## 目标与前置条件

逻辑世界接收局部 ColliderShape 与世界 Transform3，返回 ColliderId / RaycastHit。调用方管理业务实体映射、更新和删除。方向使用单位向量，距离与形状采用一致世界尺度。安装、依赖及命名空间见[包入口](../README.md)。

## 完整示例与预期结果

以下完整 Console 示例对 Naive 和 Grid 执行同样的注册、射线、重叠与删除操作。对象中心在 X=5、半径为 1，最近命中距离约为 4。

```csharp
using System;
using System.Collections.Generic;
using AbilityKit.Combat.Collision;
using AbilityKit.Core.Mathematics;

public static class Program
{
    public static void Main()
    {
        foreach (var kind in new[] { BroadphaseType.Naive, BroadphaseType.Grid })
        {
            using var service = new CollisionService(new CollisionWorldOptions
            { BroadphaseType = kind, GridCellSize = 4f, InitialCapacity = 64 });
            var world = service.World;
            var transform = new Transform3(new Vec3(5, 0, 0), Quat.Identity, Vec3.One);
            var shape = ColliderShape.CreateSphere(Vec3.Zero, 1f);
            var id = world.Add(transform, shape, layerId: 1);
            var filter = new LayerFilter(1 << 1);
            var ray = new Ray3(Vec3.Zero, Vec3.Right);
            if (!world.Raycast(ray, 10f, filter, out var hit) ||
                hit.Collider != id || Math.Abs(hit.Distance - 4f) > 0.001f)
                throw new Exception("射线结果不符合预期");
            var results = new List<ColliderId>();
            var sphere = new Sphere(new Vec3(5, 0, 0), 2f);
            var added = world.OverlapSphere(sphere, filter, results);
            if (added != 1 || results.Count != 1 || results[0] != id)
                throw new Exception("重叠查询失败");
            results.Clear(); // 查询追加结果；复用列表前自行清空。
            if (world.OverlapSphere(sphere, LayerFilter.None, results) != 0)
                throw new Exception("空过滤器失效");
            if (!world.Remove(id) || world.UpdateTransform(id, transform))
                throw new Exception("删除后仍可更新");
            if (world.Raycast(ray, 10f, filter, out _)) throw new Exception("删除后仍命中");
            Console.WriteLine($"Collision {kind}: register, raycast, overlap, remove OK");
        }
    }
}
```

预期打印 Naive、Grid 各一行且不抛异常。例子显式 Remove 后释放服务，Dispose 不代替 Collider 生命周期清理。

## 生命周期与查询输出

| 入口 | 当前契约 |
| --- | --- |
| `Add(transform, localShape, layerId)` | 保存局部形状与世界变换；不要烘焙世界坐标后再次传非单位变换 |
| `UpdateTransform/Shape/Layer`、`Update` | 更新已有碰撞体；无效或已删除 ID 返回 false |
| `Remove` | 返回是否成功，不发业务事件；ID 从 1 递增、不复用、不跨世界通用 |
| `Raycast` | 返回最近命中，未命中时 false 且 hit=default；等距没有跨后端统一次级排序 |
| `OverlapSphere` | 追加至调用方 List，返回本次添加数量；null 列表抛 ArgumentNullException |
| `IColliderBoundsSource.CopyColliderBounds` | 追加保守中心/半径描述，不是完整几何或业务身份快照 |
| `CollisionService.Dispose` | 当前无清理动作；世界也没有统一 Dispose/Clear 接口 |

ColliderShape 支持 Sphere、Aabb、Capsule、OBB。Naive/Grid 均实现可选 ISphereSweepCollisionWorld、IOrientedBoxSweepCollisionWorld 和 IShapeOverlapCollisionWorld；第三方实现需按能力检查。形状重叠支持 XZ 圆、扇形、矩形、环形，最终判定使用碰撞体保守中心/半径，不能当作完整三维精确相交。

Sphere/Capsule 半径及 OBB 半尺寸使用缩放最大绝对分量保守处理；AABB 变换后重建保守包围盒。球扫掠的 Capsule 分支使用近似，不承诺所有形状精确 CCD。

## 过滤与后端选择

**LayerFilter 与层矩阵分开管理。** IncludeMask=0 表示不限制层；ExcludeMask 优先排除；LayerFilter.None 排除全部。IgnoredColliders 保存 int 数组引用并线性搜索，查询中不要修改数组。

掩码为 int，虽然层号校验接受 0–63，`1 << layer` 会令 32–63 与 0–31 冲突。当前应把查询层限定为 0–31。64×64 CollisionLayerMatrix 不能消除冲突；Raycast / Overlap / Sweep 不自动读取矩阵。源层关系和敌我资格由调用方明确判断。

| 选择 | 实际实现与约束 |
| --- | --- |
| 默认 / Naive | NaiveCollisionWorld，线性扫描历史 Entry；可作小规模对照 |
| Grid | GridCollisionWorld；CellSize > 0，InitialCapacity 至少为正且足够覆盖最坏候选数 |
| DynamicAabbTree | 工厂仍返回 Grid；树数据结构存在不等于 tree-backed world 已交付 |

**Grid 的 InitialCapacity 也是查询候选上限。** Entry 可扩容，但查询数组不跟随扩容；候选超限没有提示，可能静默漏检。应按最坏局部密度规划并对照 Naive，不能仅理解为预分配。负 Z 的 Cell Key 符号扩展也可能导致大量键冲突，坐标范围、密度及性能预算须独立验证。

## 常见问题与运行边界

| 现象 | 定位与处理 |
| --- | --- |
| 改矩阵后射线仍命中 | 查询只按 LayerFilter；明确目标层掩码，或上层判断源/目标关系 |
| 结果越来越多 | List 由查询方持有，复用前 Clear |
| Grid 结果少于 Naive | 检查候选上限、变换同步和空间配置，不能直接当作无碰撞 |
| 移动后仍命中旧位置 | 框架不读 Unity Transform，宿主必须 Update |
| 等距命中顺序不同 | 无统一 tie-break，不要以遍历顺序代替业务优先级 |

世界与 Grid 共享查询缓存没有线程安全或重入承诺，在单一逻辑线程同步/查询。几何层不负责积分、伤害或业务有效性；确定性数学内核不等于全后端、跨平台或全部业务顺序已验证。

## 验证范围与关联文档

2026-11-25 在 Windows / .NET 10 Release 通过 core-stability 的 Combat collision deterministic query tests 单步，非完整 gate。完整示例编译运行；本地探针另复现层位冲突、矩阵不参与查询和候选截断，复现限制不表示已修复。

源码：[世界/Naive](../Runtime/Math/CollisionWorld.cs)、[Grid](../Runtime/Collision/GridCollisionWorld.cs)、[过滤器](../Runtime/Collision/LayerFilter.cs)、[工厂](../Runtime/Collision/CollisionWorldFactory.cs)。未执行 Unity GUI/Player、干净 UPM 安装、完整业务、跨平台比对、压力预算或 CI，旧正文见[历史说明](../Document/CombatCollision碰撞抽象模块开发设计文档.md)。
