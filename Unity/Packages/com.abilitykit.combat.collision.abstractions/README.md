# AbilityKit Combat Collision Abstractions

`com.abilitykit.combat.collision.abstractions` 提供逻辑碰撞形状、查询接口及 Naive/Grid 世界实现，与 Unity Physics 解耦。

## 定位与环境

本次核校版本 0.1.0、Unity 最低声明 2022.3；声明来源为 [package.json](package.json)，直接依赖 Core、World DI、Deterministic。几何类型及 ICollisionWorld 位于 `AbilityKit.Core.Mathematics`；服务、工厂和 LayerFilter 位于 `AbilityKit.Combat.Collision`。

本包维护 Collider 几何状态；实体映射、阵营资格、移动响应和表现由宿主负责。Abstractions 不表示只有接口，也不表示包含刚体物理模拟。

## 安装与首次使用

在 Unity Package Manager 中从项目配置的来源安装；本地开发可选择本目录 `package.json`，确保依赖可解析。本批没有重做干净 UPM 安装。

创建 CollisionService 或使用 CollisionWorldFactory，用局部形状加世界变换注册碰撞体，再调用 Raycast / Overlap。默认服务使用 Naive，Grid 需显式配置。可复制代码与预期结果见[完整示例](Documentation~/Usage.md#完整示例与预期结果)。

注册方保存 ColliderId、同步变换并负责 Remove。CollisionService.Dispose 当前为空，不替宿主清理实体、解除映射或发通知。

## 文档与限制

- [包内文档索引](Documentation~/README.md)与[使用指南](Documentation~/Usage.md)。
- [变更记录](CHANGELOG.md)。
- [历史开发说明](Document/CombatCollision碰撞抽象模块开发设计文档.md)。

查询掩码实际只有 32 位，层号应限定为 0–31；层矩阵不自动参与查询。Grid 构造容量同时限制查询候选缓冲，超限可能漏检；DynamicAabbTree 工厂分支当前仍返回 Grid。详细限制见指南。

> 核校状态：2026-11-25 对照当前 Runtime、.NET 镜像及限定测试核校；不代表跨平台确定性、性能或发布验收。
