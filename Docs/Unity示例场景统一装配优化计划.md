# Unity 示例场景装配与包归属优化实施记录

## 1. 目标

本次优化将 Unity 示例入口收敛为一个公共 Starter，同时把玩法专用资产归还给对应 View 包。

最终要求：

1. `Assets/Scenes/StarterScene.unity` 是唯一公共入口。
2. Starter 保留登录和多人玩法选择。
3. Starter 右上角提供不依赖登录的 Local Mode，可选择 MOBA 或 Shooter。
4. MOBA 和 Shooter 使用各自的包内 Gameplay Scene。
5. Scene、Bootstrap Prefab、Root Prefab、Catalog 和 Profile 均属于对应 View 包。
6. `Assets` 不再承载 `DemoComposition` 或公共 Gameplay Bootstrap Scene。
7. Local 与 Multiplayer 复用统一 Launch Request，但保持各自的 Intent 约束。
8. MOBA 正式多人仍为 Lockstep，Shooter 正式多人仍为 Authoritative Interpolation。
9. 使用真实 Unity `-batchmode -nographics` 从 Starter 验证本地与多人流程。

## 2. 最终架构决策

早期方案曾计划保留一个公共 Gameplay Bootstrap Scene，并把四种 Profile 集中放入 `Assets/DemoComposition`。该方案虽然减少了 Scene 数量，但仍让主工程拥有所有玩法资产，也使公共 Catalog 同时依赖 MOBA 和 Shooter。

最终采用“单一公共 Starter + 玩法包内 Scene”的拓扑：

```text
Assets/
  Scenes/
    StarterScene.unity

Packages/com.abilitykit.demo.moba.view.runtime/
  Composition/
    Prefabs/
      MobaDemoRoot.prefab
      MobaGameplayBootstrap.prefab
    Profiles/
      MobaGameplayCatalog.asset
      MobaLocalProfile.asset
      MobaMultiplayerProfile.asset
  Scenes/
    MobaDemoGameplayScene.unity

Packages/com.abilitykit.demo.shooter.view.runtime/
  Composition/
    Prefabs/
      ShooterGameplayBootstrap.prefab
      ShooterLocalDemoRoot.prefab
      ShooterMultiplayerDemoRoot.prefab
    Profiles/
      ShooterGameplayCatalog.asset
      ShooterLocalProfile.asset
      ShooterMultiplayerProfile.asset
  Scenes/
    ShooterDemoGameplayScene.unity
```

该拓扑保留 Starter 与 Gameplay 生命周期隔离，同时使每个玩法包独立拥有和校验自己的 Scene、Catalog、Profile 与 Prefab。

## 3. 启动契约

### 3.1 公共请求

装配层使用：

```csharp
public enum DemoGameplayId
{
    Moba,
    Shooter
}

public enum DemoLaunchMode
{
    Local,
    Multiplayer
}

public readonly struct DemoLaunchRequest
{
    public DemoGameplayId Gameplay { get; }
    public DemoLaunchMode Mode { get; }
    public string ProfileId { get; }
}
```

约束：

- `DemoLaunchRequest` 只描述 Gameplay、Mode 和可选 Profile ID。
- Gateway、账号、Token、Room 等网络参数继续由 `DemoMultiplayerLaunchIntent` 管理。
- Request 一次性消费，不允许缺失时静默选择默认玩法。
- Gameplay Scene 只从本玩法 Catalog 查找 Profile。

### 3.2 Local 流程

1. 打开公共 Starter。
2. 点击右上角 Local Mode。
3. 选择 MOBA 或 Shooter。
4. Starter 清除可能残留的 `DemoMultiplayerLaunchIntent`。
5. Starter 写入 `DemoLaunchRequest(gameplay, Local, string.Empty)`。
6. Starter 加载所选玩法的包内 Scene。
7. 包内 Bootstrap 从本玩法 Catalog 选择 Local Profile 并实例化 Root Prefab。

Local 流程不要求登录，且启动后不得存在 Multiplayer Intent。

### 3.3 Multiplayer 流程

1. Starter 登录 Gateway。
2. 用户选择 MOBA 或 Shooter。
3. Starter 写入 `DemoMultiplayerLaunchIntent` 和 `DemoLaunchRequest`。
4. Starter 加载对应玩法的包内 Scene。
5. 包内 Bootstrap 验证 Gameplay、Mode 和 Multiplayer Intent 一致。
6. Multiplayer Profile 实例化正式玩法 Root。

同步策略不由 Bootstrap 推断或覆盖：

- MOBA：`frame-sync-authority` / Lockstep。
- Shooter：`state-sync-authority` / Authoritative Interpolation，模型值为 `3`。

## 4. Starter 与路由

公共路由仅定义三个稳定名称：

```csharp
public const string Starter = "StarterScene";
public const string Moba = "MobaDemoGameplayScene";
public const string Shooter = "ShooterDemoGameplayScene";
```

Starter 配置分别保存 MOBA 和 Shooter Scene Name，不再保存公共 Gameplay Scene Name。

Starter Controller 的职责：

- 绘制登录和 Multiplayer 玩法入口。
- 在右上角绘制 Local Mode 入口及玩法选择窗口。
- Local 启动前清理 Multiplayer Intent。
- Multiplayer 启动时保留正式登录、房间和网络 Intent。
- 根据 Gameplay 加载不同包内 Scene。
- 提供无头自动化可调用的公开 Local 启动 API；自动化仍走与 UI 相同的运行时路径。

## 5. 资产生成与迁移

`DemoGameplayCompositionBuilder.GenerateAll` 已调整为包所有权生成器：

- 创建 MOBA 和 Shooter View 包下的 `Composition` 与 `Scenes` 目录。
- 从旧路径迁移可复用 Root Prefab 和 Profile，尽量保留 GUID。
- 为每个玩法创建独立 Catalog、Local Profile、Multiplayer Profile 和 Bootstrap Prefab。
- 创建两个玩法专用 Gameplay Scene。
- 更新 Starter 配置和 Build Settings。
- 删除旧 `Assets/DemoComposition` 与公共 `Assets/Scenes/DemoGameplayBootstrapScene.unity`。

生成器保留旧路径常量仅用于幂等迁移和清理。运行时、构建和测试代码不再依赖旧路径。

## 6. Build Settings 与构建输入

Build Settings 精确启用：

```text
Assets/Scenes/StarterScene.unity
Packages/com.abilitykit.demo.moba.view.runtime/Scenes/MobaDemoGameplayScene.unity
Packages/com.abilitykit.demo.shooter.view.runtime/Scenes/ShooterDemoGameplayScene.unity
```

构建输入：

- MOBA Local：仅 MOBA Gameplay Scene。
- Shooter Local：仅 Shooter Gameplay Scene。
- Starter：Starter + MOBA Gameplay Scene + Shooter Gameplay Scene。

`MobaDemoBuild.ValidateMultiplayerSceneTopology` 校验：

- Build Settings 的 Scene 数量、顺序和路径。
- 每个玩法 Scene 的 Bootstrap 与 Catalog 引用。
- 每个 Catalog 恰好包含 Local 和 Multiplayer 两个 Profile。
- Profile Gameplay 与所属包一致。
- Root Prefab、Camera、AudioListener 和入口组件约束。

## 7. 无头自动化

### 7.1 Starter Local 协调器

`tools/run_starter_local_headless.ps1` 顺序启动两个独立 Unity 进程：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run_starter_local_headless.ps1
```

Unity 命令会：

1. 打开真实 Starter Scene。
2. 进入 Play Mode。
3. 查找真实 Starter Controller。
4. 通过其公开 Local API 选择玩法。
5. 等待目标 Gameplay Scene 激活。
6. 验证 Active Profile、Active Root 和 Intent。
7. 验证 Scene、Profile、Root Prefab 均归属对应 View 包。
8. 写入 JSON artifact 并返回可靠进程退出码。

PowerShell 使用 `Start-Process -Wait -PassThru` 读取 `.ExitCode`，避免 Windows PowerShell 严格模式下 GUI 子系统进程不初始化 `$LASTEXITCODE` 的问题。

### 7.2 Multiplayer 协调器

- `tools/run_moba_unity_headless_multiplayer.ps1`：两个独立 Unity 项目，经 Starter 和正式房间验证 MOBA Lockstep、移动、技能和最终收敛。
- `tools/run_shooter_unity_headless_multiplayer.ps1`：两个独立 Unity 项目验证 Shooter full/delta Actor snapshot、共同权威帧哈希和 Authoritative Interpolation 收敛。

## 8. 测试矩阵

| 维度 | 用例 | 必须验证 |
|---|---|---|
| Starter | Local MOBA | 无需登录，进入 MOBA 包内 Scene，使用 `moba-local` |
| Starter | Local Shooter | 无需登录，进入 Shooter 包内 Scene，使用 `shooter-local` |
| Starter | Multiplayer MOBA | 登录后进入 MOBA 包内 Scene，保留 MOBA Multiplayer Intent |
| Starter | Multiplayer Shooter | 登录后进入 Shooter 包内 Scene，保留 Shooter Multiplayer Intent |
| Bootstrap | Local Intent | 不存在残留 Multiplayer Intent |
| Bootstrap | Gameplay mismatch | 明确失败且不遗留 Root |
| Catalog | MOBA | 只包含 MOBA Local/Multiplayer Profile |
| Catalog | Shooter | 只包含 Shooter Local/Multiplayer Profile |
| MOBA 网络 | 两客户端 | Lockstep、移动、技能、最终 Actor 收敛 |
| Shooter 网络 | 两客户端 | 模型 3、full/delta snapshot、共同权威帧哈希一致 |
| 构建 | Multiplayer | 三 Scene 路径和顺序准确 |
| 静态 | 旧路径 | 仅允许生成器迁移兼容常量引用旧路径 |

## 9. 2026-08-17 验收结果

### 9.1 Starter Local

完整协调器退出码为 `0`，MOBA 与 Shooter 均通过：

- Artifact：`artifacts/starter-local-headless/20260817-100838-746/summary.json`
- MOBA Scene：`Packages/com.abilitykit.demo.moba.view.runtime/Scenes/MobaDemoGameplayScene.unity`
- MOBA Profile：`moba-local`
- MOBA Root：`Packages/com.abilitykit.demo.moba.view.runtime/Composition/Prefabs/MobaDemoRoot.prefab`
- Shooter Scene：`Packages/com.abilitykit.demo.shooter.view.runtime/Scenes/ShooterDemoGameplayScene.unity`
- Shooter Profile：`shooter-local`
- Shooter Root：`Packages/com.abilitykit.demo.shooter.view.runtime/Composition/Prefabs/ShooterLocalDemoRoot.prefab`

两个结果均确认 `mode=Local`、`success=true`，Scene、Profile 和 Root Prefab 均位于对应 View 包。

### 9.2 Build Topology

Unity 执行：

```text
-executeMethod AbilityKit.Game.Editor.MobaDemoBuild.ValidateMultiplayerSceneTopology
```

退出码为 `0`，日志位于 `artifacts/starter-local-headless/topology-validation.log`。

### 9.3 Shooter Multiplayer

完整双客户端协调器退出码为 `0`：

- Artifact：`artifacts/shooter-unity-headless/20260817-021645-494`
- Template：`state-sync-authority`
- Model：`3`
- Common authoritative frame：`241`
- Stable hash：`0x7C4FD084`
- 双方应用 snapshot：`53/53`
- Full/Delta：双方均为 `10/43`
- Hash mismatch：`0`
- Resync：`0`
- 两个玩家跨客户端权威坐标差：`0.000/0.000`

这证明最终 Scene 路由没有改变 Shooter Authoritative Interpolation 契约。

### 9.4 MOBA Multiplayer

本次双客户端运行中，两个 Unity 客户端均正常完成并报告：

```text
Formal two-client MOBA flow, movement, skill synchronization, and final convergence passed.
```

Artifact 位于 `artifacts/moba-unity-headless/20260817-021254-734`。双方通过 Starter 进入 `MobaDemoGameplayScene`，均达到 BattleReady，完成移动、技能同步和最终收敛，且同步模式断言为 Lockstep。

协调器最终退出码为 `1`，原因是独立于本次场景架构的附加 room push 门禁发现 owner 的 `roomRefreshFallbackCount=12`、member 为 `0`。该结果不能记为“完整协调器通过”；需要在网络房间推送专项中继续消除 owner snapshot fallback。此前完整 MOBA Lockstep 基线仍保存在 `artifacts/moba-unity-headless/20260816-173858-391`。

## 10. 完成定义

场景与资产归属优化满足以下条件时完成：

1. 主 Assets 只保留公共 Starter Scene，不存在 `Assets/DemoComposition` 和公共 Gameplay Bootstrap Scene。
2. MOBA/Shooter 的 Scene、Catalog、Profile、Bootstrap 和 Root Prefab 属于各自 View 包。
3. Starter 支持登录后的 Multiplayer 入口和右上角无需登录的 Local Mode。
4. Local 启动清理 Multiplayer Intent，Multiplayer 启动保留正式网络 Intent。
5. Build Settings 精确包含 Starter、MOBA Scene、Shooter Scene。
6. Starter Local MOBA/Shooter 无头流程退出码为 `0`。
7. Shooter Authoritative Interpolation 双客户端协调器退出码为 `0`。
8. MOBA 两客户端玩法流程保持 Lockstep、移动、技能与收敛语义；room push fallback 作为独立网络门禁遗留跟踪。
9. 静态扫描除生成器迁移兼容常量外不存在旧公共 Gameplay Scene 或 `Assets/DemoComposition` 依赖。
10. `git diff --check` 通过。
