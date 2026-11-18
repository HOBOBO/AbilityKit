# Marker 兼容源码入口

命名空间为 `AbilityKit.Core.Markers`。登记、查询、反射扫描、全局状态和迁移限制统一见随包 [Marker 注册与扫描兼容契约](../../Documentation~/Markers.md)，准入规则见 [Core 边界](../../Documentation~/CoreBoundary.md)。

| 文件 | 职责 |
|---|---|
| `MarkerAttribute.cs`、`IMarkerRegistry.cs` | 特性扫描回调与最小登记接口。 |
| `MarkerRegistry.cs` | 按首次登记顺序保留不同类型。 |
| `KeyedMarkerRegistry.cs` | 当前键映射及历史类型目录。 |
| `MarkerScanner.cs` | 按显式输入程序集调用直接声明的标记特性。 |
| `MarkerScannerExtensions.cs` | 既有程序集名称、命名空间与引用扫描辅助入口。 |
| `MarkerSystem.cs`、`MarkerBootstrapper.cs` | 已弃用并冻结的全局登记和引导兼容入口。 |

本目录不提供代码生成或编辑器发现，也不要求所有框架 Attribute 继承 MarkerAttribute。新发现和装配策略由所属所有者显式控制；旧全局入口不新增消费者，保留下一主版本前的兼容窗口。
