# Shooter 验收外壳绑定说明（Unity 薄层）

本说明描述 Unity 端如何作为「验收外壳」绑定到纯 C# 验收抽象 [`ShooterAcceptanceLab`](../Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterAcceptanceLab.cs#L1)。核心同步逻辑与多模式/多网络环境的组合已由 xUnit 全量覆盖，Unity 端只负责「选择 + 驱动 + 可视化」，不持有任何同步规则。

## 1. 抽象边界

纯 C# 层（`AbilityKit.Demo.Shooter.View` 命名空间，asmdef↔csproj 双投影）暴露以下入口：

- [`ShooterAcceptanceCatalog`](../Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterAcceptanceLab.cs#L1)：可选同步模式 `SyncModes`、正式模板 `SyncTemplates`、验收矩阵 `SyncModeMatrix` 与网络环境 `NetworkEnvironments`，UI 直接绑定。
- [`ShooterAcceptanceCatalog.SyncModeMatrix`](../Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterAcceptanceLab.cs#L1)：每个同步模板对应一个 `NetworkSyncProfile`、承载类型、收敛方式和验收准则，避免 Unity 面板把「帧同步」「纯状态同步」「混合同步」混成同一条临时路径。
- [`ShooterAcceptanceLab.Create(...)`](../Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterAcceptanceLab.cs#L1)：一键装配 runtime + presentation + controller + carrier 并已 `StartGame`，返回可运行的 `ShooterAcceptanceSession`。支持 `enableAuthoritativeWorld` 勾选对比模式。
- [`ShooterAcceptanceSession`](../Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterAcceptanceLab.cs#L1)：暴露 `Runtime`/`Presentation` 供渲染，`Run(...)` 走框架 `DemoHarnessRunner` 返回四态结果与指标；`ApplyNetwork(...)` 运行时改网络；`CompareWorlds()` 计算预测/权威差异。
- [`ShooterSveltoGameplayBenchmarkProfiles`](../Unity/Packages/com.abilitykit.demo.shooter.runtime/Runtime/Domain/Gameplay/Scenario/ShooterSveltoGameplayBenchmark.cs)：高实体批量 ECS/Svelto 基准入口，用于把 Shooter 作为性能范式样例，而不是只作为同步样例。

Unity 端**只依赖以上三者**，不引用任何 `ShooterClient*SyncController` 具体类型。

## 2. 下拉菜单绑定

```csharp
// 同步模式下拉：未实现的模式置灰（Implemented == false）。
foreach (var mode in ShooterAcceptanceCatalog.SyncModes)
{
    AddDropdownOption(mode.DisplayName, enabled: mode.Implemented, payload: mode);
}

// 网络环境下拉：从理想基线到压力场景，已按序排列。
foreach (var env in ShooterAcceptanceCatalog.NetworkEnvironments)
{
    AddDropdownOption(env.DisplayName, enabled: true, payload: env);
}
```

## 3. 点击「开始验收」

```csharp
var sync = _selectedSyncOption;       // ShooterAcceptanceSyncOption
var network = _selectedNetworkOption; // ShooterAcceptanceNetworkOption

// enableAuthoritativeWorld: 启动时勾选「对比模式」，会额外启动一个独立权威 World。
_session = ShooterAcceptanceLab.Create(in sync, in network, enableAuthoritativeWorld: _compareToggle.isOn);
```

得到 `_session` 后，Unity 有两种驱动方式：

- 离线一键校验：调用 `_session.Run()`，把返回的 `DemoHarnessRunResult.Status`（Completed/Degraded/Unsupported/Failed）与 `Metrics` 直接打到验收面板。
- 逐帧可视化：每个 Unity 帧调用 `_session.Controller.Tick(dt)`，再从 `_session.Presentation.ViewModel` 读取实体位置驱动渲染；若开了对比模式，同步调用 `_session.TickAuthoritativeWorld(dt)` 保持权威 World 对齐。

## 4. 运行时调节网络环境（无需重建会话）

验收的核心诉求之一是「边跑边调网络」。会话启动后，UI 滑条/下拉可随时改网络：

```csharp
// 方式 A：切到目录里的另一个预设。
_session.ApplyNetwork(ShooterAcceptanceCatalog.NetworkEnvironments[3].Profile, "Cross Region");

// 方式 B：用滑条拼一个自定义网络（延迟/抖动/丢包/乱序/带宽）。
var tuned = new NetworkConditionProfile(
    baseLatencyMs: _latencySlider.Value,
    jitterMs: _jitterSlider.Value,
    packetLossRate: _lossSlider.Value,
    reorderRate: _reorderSlider.Value,
    bandwidthKbps: 0);
_session.ApplyNetwork(tuned, $"Tuned {_latencySlider.Value}ms");

// 下一次 Run() 或逐帧 Step 立即生效。
var result = _session.Run();
```

`NetworkProfile` 是会话上的可变属性，`ApplyNetwork` 只改值、不重建 controller，因此调节过程平滑无中断。

## 5. 多 World 对比模式（可选，启动勾选）

当 `enableAuthoritativeWorld: true` 时，会话额外持有：

- [`Session.AuthoritativeWorld`](../Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterAcceptanceLab.cs#L1)：一个独立 [`ShooterBattleRuntimePort`](../Unity/Packages/com.abilitykit.demo.shooter.runtime/Runtime/Application/Runtime/ShooterBattleRuntimePort.cs#L12)，纯前向模拟、无预测/回滚，作为「地面真相」。
- [`Session.AuthoritativePresentation`](../Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterAcceptanceLab.cs#L1)：独立的 `ShooterPresentationFacade`，权威 World 的快照已投影进去，Unity 可用**同一套** `ShooterViewBinder` 渲染第二个视口。

Unity 渲染两个并排视口：

```csharp
// 左视口：客户端预测 World（带预测/回滚，受网络环境影响）。
_viewBinderLeft.Bind(_session.Presentation);

// 右视口：权威 World（地面真相，不受网络影响）。
if (_session.HasAuthoritativeWorld)
{
    _viewBinderRight.Bind(_session.AuthoritativePresentation);
}
```

### 差异高亮

```csharp
var comparison = _session.CompareWorlds();
// comparison.Divergences: 每个玩家的 (ClientX,ClientY) vs (AuthorityX,AuthorityY) 与 Distance
// comparison.MaxDistance: 当前最大偏差，可驱动顶部偏差条
foreach (var d in comparison.Divergences)
{
    if (d.Distance > _tolerance)
    {
        _overlay.Highlight(d.PlayerId, (float)d.Distance);
    }
}
```

`CompareWorlds()` 返回 [`ShooterWorldComparison`](../Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterAcceptanceLab.cs#L1)，内含每实体 [`ShooterWorldDivergence`](../Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterAcceptanceLab.cs#L1)。预测误差越大、偏差越明显，直观展示回滚/预测在网络劣化下的表现。

> 对比模式默认关闭（`HasAuthoritativeWorld == false`），`CompareWorlds()` 返回空结果，不影响纯客户端演示。

## 6. 全矩阵冒烟

验收面板可提供「跑全部组合」按钮，直接调用：

```csharp
var results = ShooterAcceptanceLab.RunCatalogMatrix();
// results: 每个 (实现模式 × 网络环境) 一行四态结果，等价于手动点完整张验收矩阵。
```

该方法与 [`ShooterAcceptanceLabTests`](../src/AbilityKit.Demo.Shooter.Runtime.Tests/Client/ShooterAcceptanceLabTests.cs#L1) 中 `RunCatalogMatrixCoversEveryImplementedModeAndNetwork` 用例同源，保证 Unity 看到的结论与 CI 一致。

正式矩阵入口用于 UI 展示每个模式的验收边界：

```csharp
foreach (var row in ShooterAcceptanceCatalog.SyncModeMatrix.Rows)
{
    AddMatrixRow(row.TemplateId, row.Profile.CompatibilityModel, row.AcceptanceCriteria);
}
```

当前矩阵固定覆盖三类路径：

| 模板 | 同步语义 | 验收重点 |
|------|----------|----------|
| `predict-rollback-authority` | 本地预测 + 回滚对账 | 预测回滚、快照覆盖、确定性重放 |
| `authoritative-interpolation-presentation` | 服务器权威 + 远端插值 | 延迟播放缓冲、表现收敛、客户端开火不落权威状态 |
| `hybrid-hero-prediction` | 本地英雄预测 + 远端插值 | 本地追帧、远端插值、全量快照恢复 |

## 7. ECS/Svelto 基准入口

Shooter 的高性能 ECS 展示使用 runtime 层基准入口，Unity 面板可单独提供「跑 ECS 基准」按钮，不与网络同步验收按钮混用：

```csharp
var profile = ShooterSveltoGameplayBenchmarkProfiles.ProjectileStormBaseline;
var result = ShooterSveltoGameplayBenchmark.Run(runner, in profile);
// result.Deterministic / TotalInitialEntityFrames / LastResult.StateHash 可直接显示到诊断面板。
```

该基准复用 [`ShooterSveltoGameplayScenarioRunner`](../Unity/Packages/com.abilitykit.demo.shooter.runtime/Runtime/Domain/Gameplay/Scenario/ShooterSveltoGameplayScenarioRunner.cs) 的 `EntitiesDB.QueryEntities<...>()` 批量查询循环，目标是证明示例的 ECS/Svelto 范式入口已经独立于同步策略存在。

## 8. 诊断与一键脚本

- Unity 诊断面板优先绑定 [`ShooterHostDiagnosticsSnapshot`](../Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Hosting/ShooterHostDiagnostics.cs#L16)，它已经汇总帧号、实体数量、最近事件、预测/权威偏差、载体网络统计、快照应用结果、时间锚和 lag compensation telemetry。
- 本地状态同步服务器重启入口：[`restart_shooter_state_sync.ps1`](../Server/Orleans/tools/restart_shooter_state_sync.ps1#L1) 或 [`restart_shooter_state_sync.bat`](../Server/Orleans/tools/restart_shooter_state_sync.bat#L1)，默认监听 `127.0.0.1:41001`。
- TCP Gateway 冒烟入口：[`run_shooter_smoke.ps1`](../Server/Orleans/tools/run_shooter_smoke.ps1#L1)，用于验证 Orleans ShooterSmoke 项目构建、启动、建房、开始、输入、快照订阅与退出闭环。
- 服务端 World 生命周期现在由 [`ServerBattleWorldManager`](../Server/Orleans/src/AbilityKit.Orleans.Grains/Battle/ServerBattleWorldManager.cs#L1) 管理，MOBA 与 Shooter 都只是注册到同一个中立 battle world manager 中。

### Unity 自动化验收

交互入口为 `Tools/AbilityKit/Demos/Shooter/Shooter Demo` →「验收规格」→「运行完整验收矩阵」。在编辑模式且没有运行演示时可执行；窗口显示通过或失败摘要，逐项结果见 Console。此操作不连接窗口中配置的服务器，也不写场景或资产。

Unity 批处理或 CI 继续使用 [`ShooterAcceptanceAutomation.RunCatalogMatrixOrThrow()`](../Unity/Packages/com.abilitykit.demo.shooter.editor/Editor/Windows/ShooterAcceptanceAutomation.cs)，方法名不变。以下命令从仓库根目录运行：

```powershell
$logDir = Join-Path (Get-Location).Path "local/Logs/shooter-acceptance-$(Get-Date -Format 'yyyyMMdd-HHmmssfff')"
New-Item -ItemType Directory -Path $logDir | Out-Null
Unity.exe -batchmode -quit -projectPath Unity -executeMethod AbilityKit.Demo.Shooter.Editor.Windows.ShooterAcceptanceAutomation.RunCatalogMatrixOrThrow -logFile (Join-Path $logDir 'Editor.log')
```

窗口与批处理共用 [`ShooterAcceptanceLab.RunCatalogMatrix()`](../Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterAcceptanceLab.cs) 和原有失败判定：只要有场景未完成，或存在 `Failed` / `Unsupported`，执行器即抛出异常。窗口捕获并显示失败，批处理保留失败退出行为。需要自定义步数、步长或种子的 C# 调用仍可使用参数化重载。

## 9. 扩展新同步模式的接入点

新增模式时，纯 C# 层改三处即可，Unity 端零改动：

1. 在 [`ShooterClientSyncControllerFactory.Create`](../Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterClientSyncControllerFactory.cs#L37) 增加对应 `case`。
2. 在 [`ShooterAcceptanceCatalog.SyncModes`](../Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterAcceptanceLab.cs#L1) 增加一项并置 `implemented: true`。
3. 在 [`ShooterAcceptanceCatalog.SyncTemplates`](../Unity/Packages/com.abilitykit.demo.shooter.view.runtime/Runtime/Client/Synchronization/ShooterAcceptanceLab.cs#L1) 增加模板，并让 `SyncModeMatrix` 产出对应验收准则。

下拉菜单、Session 装配、矩阵冒烟、对比模式、验收准则说明会自动纳入该模式。
