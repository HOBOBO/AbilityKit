# src/ 下 .NET 项目

全部 net10.0。源码通过 `<Compile Include="../../Unity/Packages/...">` 直接引用 Unity 包源码（共享源码，非编译产物）。

## 7 个项目

| 项目 | 职责 | 关键 ProjectReference |
|------|------|----------------------|
| `AbilityKit.Demo.Moba.Share` | 编译 `com.abilitykit.demo.moba.share` 源码 | Core, Host, World.FrameSync, Protocol.Moba, Triggering, Game.Battle.Runtime, World.Snapshot, Network.Runtime, World.ECS |
| `AbilityKit.Demo.Moba.Core` | 编译 `com.abilitykit.demo.moba.runtime` 源码（**逻辑核心**，排除 `Editor/`、`Impl/`、`Testing/`，`!UNITY`） | Share + 30 个 abilitykit.* (含 World.Entitas/FrameSync/StateSync, Combat.*, Coordinator, Flow, Host.Extension, Protocol.Moba)；NuGet: Entitas 1.14.2, Newtonsoft.Json |
| `AbilityKit.Demo.Moba.Infrastructure` | 配置基础设施扩展 | Core + Ability.Config；Newtonsoft.Json |
| `AbilityKit.Demo.Moba.Console` | **net10.0 Console 可执行入口** | Share, Core, Infrastructure, World.ECS, Game.Battle.Transport.Runtime, World.StateSync, World.Snapshot, Protocol.Moba, Host, Host.Extension, Triggering, HFSM.Core, Flow, Combat.Collision.Abstractions；Newtonsoft.Json |
| `AbilityKit.Demo.Moba.AI` | MOBA AI 训练/推理接入 | AI.Abstractions, Moba.Console, Host.Extension, Protocol.Moba |
| `AbilityKit.Demo.Moba.Tests` | xUnit 测试（25 个） | AI.*, Moba.AI, Core, Share, Infrastructure, Moba.Console, ET.Logic, Context, Trace |
| `AbilityKit.Demo.Moba.NetworkCondition.Tests` | 网络条件控制器测试（1 个） | Network.Runtime |

## 依赖图

```
Share ──► Core(逻辑) ──► Infrastructure
   │           │
   │           └──► Console(Exe) ──► AI ──► Tests
   │                   ▲
   └───────────────────┘
NetworkCondition.Tests ──► Network.Runtime (单文件引用 view.runtime)
```

## 源码共享机制

Unity 包源码通过 `<Compile Include="../../Unity/Packages/.../*.cs" />` 直接编入 .NET 项目。这意味着：

- 修改 Unity 包源码会同时影响 Unity 与 Console Demo
- Console Demo 的 csproj 文件维护着"哪些包源码被包含、哪些被排除"的清单
- `Editor/` 目录、`UNITY_*` 宏下的代码通常被排除

## Build 与 Run

```bash
# 编译整个 sln
dotnet build src/AbilityKit.Demo.Moba.sln

# 运行 Console Demo
cd src/AbilityKit.Demo.Moba.Console
dotnet run

# 跑测试
dotnet test src/AbilityKit.Demo.Moba.Tests
```

## csproj 排除规则（典型）

`AbilityKit.Demo.Moba.Core.csproj` 排除：

- `**/Editor/**`
- `**/Impl/**`
- `**/Testing/**`
- `<DefineConstants>` 加 `!UNITY` 相关常量
