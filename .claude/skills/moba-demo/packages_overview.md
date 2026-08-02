# 5 个 moba 包职责与依赖

## 包清单（package.json）

| 包 | version | 关键依赖 |
|----|---------|---------|
| `com.abilitykit.demo.moba.share` | 0.0.1 | core(0.1.0), host(0.1.0), world.framesync(0.1.0) |
| `com.abilitykit.demo.moba.view.abstractions` | **0.1.0** | world.networkfragments |
| `com.abilitykit.demo.moba.host` | **0.1.0** (🆕) | host.extension(0.1.0), protocol.moba, core/world.*(0.1.0) — Moba host adapter 提取自 host.extension |
| `com.abilitykit.demo.moba.runtime` | 0.0.1 | share + demo.moba.host + 19 个 abilitykit.* 0.1.0 模块（ability/triggering/pipeline/attributes/modifiers/combat.*/host.extension/coordinator） |
| `com.abilitykit.demo.moba.view.runtime` | 0.0.1 | runtime + share + view.abstractions + demo.moba.host + game.battle.* + protocol.moba/room |
| `com.abilitykit.demo.moba.editor` | 0.0.1 | runtime + view.runtime + hotreload |

## 依赖图

```
share ◄── runtime ◄── view.runtime ◄── editor
            ▲            ▲
            │            │
        view.abstractions ┘
```

注意：

- 框架包稳定化（2026-07-31）已将 25 个 abilitykit 核心包推到 0.1.0 Beta；moba demo 自身的多数包仍为 0.0.1
- `demo.moba.host`（🆕）是从 `host.extension` 提取出来的 Moba host adapter（38 文件 + 3 asmdef），含 MobaBattleLaunchSpec/MobaRoomOrchestrator/MobaHostRuntimeBuilder 等
- `view.editor` 不存在
- `runtime` 的 package.json 没有显式列 coordinator 依赖，但 asmdef references 含 `AbilityKit.Coordinator`

## 各包职责（一句话）

- **share**：平台无关的纯接口/DTO/枚举/Flow 抽象，给 runtime 与 view.runtime 共用
- **view.abstractions**：view 与 logic 层之间的共享抽象（Hud/View/PresentationCue/位置插值等纯数据契约）
- **runtime**：MOBA 逻辑运行时——装配（Bootstrap/Worlds）、领域（Domain）、基础设施（Infrastructure/Config/LubanGen）、系统（Systems）
- **view.runtime**：MOBA 表现/会话运行时——Game/App/Flow 状态机、BattleSessionFeature、View/Presentation、Net/Sync、Replay、6 英雄验收测试
- **editor**：Editor 工具链——BattleDebug 14 面板、ConfigSync（SO + Exporter + Validator）、SceneGizmos、HotReload、Preview、CollisionDebug
