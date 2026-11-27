# Navigation 使用指南

> 文档类型：使用指南。适用范围：0.1.0 当前 Runtime，见 [package.json](../package.json)。
> 核校状态：2026-11-25 已核校本文的装配、查询限制与恢复边界；本地 .NET 示例与 suite 仅为局部证据。

## 装配与所有权

1. 宿主准备 XZ 网格：原点、正格距、正宽高和长度等于宽×高的 `blocked` 数组，索引按 `z * width + x`。`NavigationGrid` 不复制数组；发布后按只读数据使用。需要换图时构建新数据，再在安全点替换。
2. 使用 `NavigationWorldFactory.Create` 或 `new NavigationWorld`。通过 `NavigationService` 装配时，在第一次查询前调用 `Rebuild`；初始、`Rebuild(null)` 或 `Dispose` 后 `World` 为空。`NavigationWorld` 会持有传入的 options 引用，装配后也应视为只读。
3. 按状态选用路径，将有效 waypoint 交给移动宿主。查询没有 `Tick`；何时重规划、是否接受 `Partial`、如何取消旧路径，由宿主决定。搜索会复用同一世界的工作区，不并发或重入查询。返回的 waypoint 数组不是世界句柄，但结构体赋值仍共享数组引用。

## 完整示例与预期结果

以下 Console 示例在 3×2 网格绕过障碍，验证查到路径、保留精确请求点，且搜索预算耗尽时不会把旧路径当成新结果。

```csharp
using System;
using AbilityKit.Combat.Navigation;
using AbilityKit.Core.Mathematics;

var blocked = new bool[6];
blocked[1] = true;
var grid = new NavigationGrid(Vec3.Zero, 1f, 3, 2, blocked);
using var navigation = new NavigationService();
navigation.Rebuild(grid, new NavigationWorldOptions { AllowDiagonal = false, SimplifyPath = false });
var start = new Vec3(0.25f, 0f, 0.25f);
var target = new Vec3(2.75f, 0f, 0.25f);
if (navigation.World.FindPath(start, target, 0f, out var path) != PathStatus.Found || path.Length != 5 ||
    path.Waypoints[0].X != start.X || path.Waypoints[path.Length - 1].X != target.X)
    throw new Exception("绕行或终点不符合预期");

navigation.Rebuild(grid, new NavigationWorldOptions { AllowDiagonal = false, MaxIterations = 1 });
if (navigation.World.FindPath(start, target, 0f, out var limited) != PathStatus.Failed || limited.HasPath)
    throw new Exception("预算耗尽不应返回可用路径");
Console.WriteLine("Navigation: detour, exact endpoint and search budget OK");
```

预期输出末行。示例用于校验查询，不执行角色移动。

## 状态与限制

- `Found` 表示搜索到已映射的目标格；当前实现会夹取边界坐标，并把找到的路径末点替换为原请求坐标。越界目标也可能返回 `Found` 和越界末点；若业务要求严格边界，查询前应先用 `WorldToCell` 和 `IsInBounds` 判断。
- `Partial` 只用于阻挡目标格投影到可走格且搜索成功；不是任意不可达或超预算时的最近进度。目标不可达或搜索预算耗尽返回 `Failed`。`MaxIterations <= 0` 相当于不主动限额，不能用于拒绝查询。
- `FindPath`、`IsWalkable`、`TryProjectToWalkable` 当前均不做基于传入半径的膨胀；`IsWalkable` 还会把越界坐标夹到边界。间隙由宿主预处理或单独校验，不把 `IsWalkable` 当作越界检查或动态碰撞判定。

## 恢复边界与证据

默认服务没有时钟、历史或快照提供者。需要重演的宿主须确定采用的网格与选项、重规划输入与时序、已选 waypoint 及消费游标；要恢复跟随进度时，不应以当前地图重新寻路来替代导入原路径。帧同步的检查点重演和状态同步的权威状态导入均由各自宿主负责。

本地 .NET 10 Release 的 Navigation suite 与完整示例仅验证网格和查询；未跑 Unity、完整烘焙、真实战斗、独立安装或跨平台确定性。源码见 [NavigationGrid](../Runtime/Math/NavigationGrid.cs)、[NavigationService](../Runtime/Navigation/NavigationWorldFactory.cs)和 [GridPathfinder](../Runtime/Navigation/GridPathfinder.cs)。
