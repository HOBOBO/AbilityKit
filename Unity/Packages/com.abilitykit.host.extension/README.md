# AbilityKit Host Extensions

`com.abilitykit.host.extension` 为 `HostRuntime` 提供可选的通用模块，负责把世界、时间、同步驱动与回滚装配到 Host。世界的创建、登记与释放由 `world.di` 管理；玩法规则、同步模式选择和项目装配由上层负责。本包不依赖具体游戏示例或玩法协议。

MOBA 的房间、开局和宿主适配归 `com.abilitykit.demo.moba.host`，协议归 `com.abilitykit.protocol.moba`，客户端帧包双写归 `com.abilitykit.demo.moba.view.runtime`。判断一个适配器是否通用，看它是否写死了项目的世界拓扑、输入路由或恢复策略，而不是看类型名有没有 `Moba`。

## 源码与依赖

| 目录 | 职责 |
| --- | --- |
| `Runtime/Time`、`Runtime/WorldStart` | 固定步进、世界时钟、调度器和自动启动 |
| `Runtime/FrameSync`、`Runtime/Rollback` | Host 帧同步驱动与回滚桥接 |
| `Runtime/FrameSync/Input`、`Runtime/FrameSync/CatchUp` | 输入缓冲端口与按预算追帧原语；旧补帧协议不属于首发承诺 |
| `Runtime/Client`、`Runtime/Server` | 分程序集的客户端与服务端适配 |

直接依赖：`host`、`core`、`world.di`、`world.framesync`、`world.networkfragments`、`world.snapshot`、`network.runtime`、`timer`；版本以 [package.json](package.json) 为准。Unity 目标为 2022.3，三个运行时程序集均为纯 C#。仓库的 .NET 10 工程直接引用 Unity 包源码，但尚未覆盖所有客户端、服务端及补帧子目录，不保证与 Unity 的公开面完全一致。

## 接入与限制

安装条件以包声明为准。首次接入从[使用指南](Documentation~/Usage.md)的固定步长完整示例开始；[文档索引](Documentation~/README.md)统一导航当前契约。

模块使用同一组 `HostRuntime` 和 `HostRuntimeOptions` 安装与卸载；宿主负责装配顺序和关闭，暂不支持模块并发驱动或在活动世界中热替换时钟。具体顺序、时间来源和失败边界见[时间与世界启动契约](Documentation~/TimeAndWorldStart.md)。

其余旧设计资料保留在 `Document/`，示例保留在 `Example/`。MOBA 专属房间与开局设计已随对应包归档。先以包内契约和实现确定当前行为；版本记录见 [CHANGELOG](CHANGELOG.md)。
