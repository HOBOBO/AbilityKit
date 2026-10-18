# 视图运行时目录布局

## 当前落地目录

以下目录是当前代码归属；后文的分批迁移记录保留为历史背景。

```text
Runtime/
  Logic/
    Flow/                  对局外流程决策与状态机内核，独立 asmdef
    Presentation/Core/     表现投影的纯 C# 内核，独立 asmdef
    Presentation/Adapter/  投影适配，独立 asmdef
    Presentation/Gameplay/  玩法表现 profile 与校验，独立 asmdef
    Presentation/Vfx/      VFX 映射数据库，独立 asmdef
    Presentation/Timeline/  技能表现时间轴调度与输出契约，独立 asmdef
    Contracts/Time/        表现时间契约和手动时间源，独立 asmdef
    Contracts/Logging/     战斗日志契约和非 Unity 实现，独立 asmdef
  Platform/Unity/
    Assets/                Resources 加载器和 UnityEngine.Object 资源接口
    Config/                Resources 配置源与 TextAsset 加载器
    Flow/                  Unity 游戏流程服务适配
    Time/                  UnityEngine.Time 适配
    Logging/               UnityEngine.Debug 适配
    Presentation/Entities/  Unity 表现 ECS 组件、实体及其 Feature
    Presentation/Gameplay/  Resources profile 读取和 JSON 适配
    Presentation/           会话数据源及 VFX Resources/JSON 适配
  Networking/
    Room/                  房间客户端与协议映射
    Transport/             本地 Host 传输与战斗逻辑客户端
    Replication/           战斗复制、插值与可靠事件路由
  UI/
    App/                   大厅与调试界面
    Battle/                战斗 HUD
    Framework/             通用 Unity UI 基础设施
  Game/
    App/                   应用装配、配置和流程宿主
    Battle/                Session、Bootstrap、输入与实体表现
    Test/                  现有 EditMode 测试程序集
Editor/                   Unity Editor 专用实体调试程序集
```

`Logic` 下的 asmdef 均禁用 Unity 引擎引用，.NET 工程直接编译同一份源码。技能时间轴只调用 `IMobaSkillPresentationTimelineSink`，Unity 视图负责实现动画和特效输出。`Platform/Unity/Assets` 承载 `Resources` 实现及带 `UnityEngine.Object` 约束的接口；纯 C# 的 `IBattleAssetLoadService`、`IBattleAssetDependencyProvider` 和清单解析仍在逻辑层。加载屏幕由 feature factory 注入服务，默认 Unity 实现在 `UnityMobaFeatureFactoryProvider` 选择。`Platform`、`Networking`、`UI` 和 `Game` 中没有独立 asmdef 的代码仍归主 View Runtime 程序集；目录迁移不改变命名空间。跨项目复用需要实际消费者验证后再分包，不能只根据目录名判断。

玩法表现 profile 的映射和校验现在由 `Logic/Presentation/Gameplay` 承担；`Platform/Unity/Presentation/Gameplay` 负责读取 `Resources` 并把 Unity JSON 文档转换为纯 C# profile。Unity 玩法模块注册表负责验证 HUD、投影和结算实现是否齐全。

## 表现层后续优先级

1. **P0：会话资源创建，已完成**。`BattlePresentationSessionFactory` 和 `BattleViewResourceCache` 通过 `IBattlePresentationDataSource` 加载配置与 VFX；Unity 会话装配传入实现。租约存在但配置缺失时仍直接失败，不回退到全局资源。
2. **P1：玩法模块职责，已完成**。注册表归 `Game/App/Flow/Composition`，TeamDeathmatch 快照投影归纯 C# `Logic/Flow`，HUD 控件归 `UI/Battle`。
3. **P1：视图资源与 VFX 数据，已完成**。`VfxDatabase` 只持有 VFX 映射并过滤无效条目，独立的无引擎程序集可由 .NET 编译；`UnityVfxDatabaseLoader` 负责 JSON 与 `Resources` 读取。
4. **P2：视图契约与组件布局，已完成**。投影视图的世界偏移契约改用 `AbilityKit.Core.Mathematics.Vec3`，Unity 会话创建视图时从 `Vector3` 转换；GameObject、Canvas 和 MonoBehaviour 实现继续由 Unity 侧持有。

本批验证：`AbilityKit.Game.Flow.Core` 与 `AbilityKit.Demo.Moba.Presentation.Vfx` 的 .NET 编译通过；Unity EditMode 相关测试 113/113 通过。

本文定义 `com.abilitykit.demo.moba.view.runtime` 的目标目录、依赖方向和分批迁移规则。当前目录仍处于过渡期；本规范同时用于判断新代码归属和约束后续迁移。

## 1. 当前问题

当前粗粒度分区已经建立，但仍存在以下结构性问题：

- `Battle/Client/Session` 同时容纳生命周期、网络、模拟世界、控制器和子 feature，`Client` 已不能表达具体职责。
- `Battle/Presentation/Hud` 与 `Battle/Presentation/View` 文件密度过高，控件、绑定、投影、资源和运行时编排混放。
- 大量生产代码共用 `AbilityKit.Game.Flow` 命名空间，物理目录无法通过命名空间反映依赖边界。
- EditMode 测试平铺在 `Test/UnitTest`，测试文件与被测模块缺乏稳定映射。
- `Shared` 包含上下文、模块宿主、订阅和 domain 对象；只有被两个以上稳定子域依赖的轻量契约才应继续留在这里。

## 2. 目标顶层结构

以当前顶层职责为基础，后续迁移按以下方向收敛。纯逻辑程序集可被 .NET 和无头宿主直接引用，Unity 装配层选择平台实现：

```text
Runtime/
  Logic/                          # 状态、决策、时间轴和纯 C# 契约
  Networking/                     # 房间通信、协议映射与复制
  Platform/Unity/                 # Resources、Time、Debug 和场景实体适配
  UI/                             # Unity UI 控件与页面
  Game/
    App/                          # 入口、配置与 feature 装配
    Battle/                       # 对局会话、启动、输入和跨域编排
    Test/                         # EditMode 测试
Editor/                           # 编辑器专用代码
```

Gateway 的资源加载服务、依赖解析器和标签版本由 `GameEntry` 装配为 `MultiplayerBattleAssetLoadingOptions`，入口模块只消费选定的实现。内存传输里无人使用的 `IAssetProvider` 世界服务注册已删除，传输组已归入 `Networking/Transport`。Bootstrap 中的 `ResourcesTextAssetLoader` 和 `ResourcesConfigSource` 已归入 `Platform/Unity/Config`，`DictionaryConfigSource` 等纯配置源仍留在 Bootstrap，后续可在消费者明确时单独提为纯程序集。

下一批应检查 `SessionMobaWorldBootstrapFactory`、`MobaConfigLoader` 等对 `ResourcesTextAssetLoader` 的直接构造，并让启动配置显式传入 `ITextAssetLoader`。`GameplayPresentationProfileCatalog`、VFX 和 HUD 的 Unity 资源读取应继续留在平台表现侧；纯逻辑侧只接收已解析的配置或资源键。

历史 `EntityViewModel` 已整体迁入 `Battle/Presentation/Entities`。历史 `Client/SnapshotRouting` 按职责拆分：纯 decoder/registry 进入 `Battle/Networking/Replication/Routing`，实体快照应用进入 `Battle/Presentation/Snapshots`，依赖 `BattleContext` 和 session 生命周期的 command/composition bridge 留在 `Client/Session/Features/Snapshot`。历史 `Client/Synchronization` 已按 Networking、Prediction、Presentation、Session 和 Testing 边界拆空。`Client/Session` 中其余网络和模拟实现必须先从会话宿主中解耦，再分别迁入对应职责目录。

## 3. 依赖方向

允许的主要依赖方向为：

```text
App -> Battle/Bootstrap -> Battle/Session
Battle/Session -> Networking | Simulation | Input | Presentation
Presentation -> Contracts
Networking -> Contracts
Simulation -> Contracts
Input -> Contracts
```

禁止反向依赖：

- `Contracts` 不得依赖 `Session`、`Networking`、`Simulation`、`Input` 或 `Presentation`。
- `Networking` 不得直接操作 HUD、GameObject 或具体表现实体。
- `Presentation` 不得创建网络连接、推进权威帧或修改逻辑状态。
- `App` 负责选择和装配，不承载对局内 tick、快照或输入实现。
- 新代码不得放入 `Legacy`；旧代码只有在归属明确后迁出，否则删除。

## 4. 命名与程序集策略

本轮目录治理不批量调整 namespace，不新增 package，也不立即拆分主 runtime asmdef。原因是路径迁移、API 重命名和程序集拆分同时发生会显著扩大 Unity 编译与序列化风险。

执行顺序固定为：

1. 迁移物理目录并保留 `.meta` GUID。
2. 通过目标 fixture、完整 EditMode 和桌面测试。
3. 在独立批次收敛 namespace，每次只处理一个稳定子域。
4. 只有依赖方向可由现有目录稳定表达后，再用 asmdef 强制边界。

目录名使用职责名词。`Core`、`Shared`、`Common`、`Features` 不能作为默认收容目录；新增此类目录必须说明其中对象的共同契约和依赖方向。

## 5. 测试布局

测试目录镜像被测职责，不按历史任务或缺陷命名：

- `UnitTest/App/Flow`：根流程、房间流程、入口生命周期。
- `UnitTest/Battle/Bootstrap`：计划、配置、启动 gate。
- `UnitTest/Battle/Session`：会话编排和生命周期。
- `UnitTest/Battle/Networking/Gateway`：房间客户端和 Gateway 映射。
- `UnitTest/Battle/Networking/Replication`：快照 admission、插值、可靠事件和同步健康度。
- `UnitTest/Battle/Input`：输入采集、映射与提交。
- `UnitTest/Battle/Presentation/Hud`：HUD 控件、绑定和投影。
- `UnitTest/Battle/Presentation/View`：实体视图、层级和插值表现。
- `UnitTest/Acceptance`：跨模块业务验收。
- `UnitTest/Harness`：命令入口和可复用测试驱动；不包含业务断言 fixture。

测试 namespace 暂时保持不变，避免目录整理与 fixture filter 迁移耦合。

## 6. 迁移批次

### 批次 1：测试分类

先迁移纯测试文件，建立目录镜像并验证 Unity GUID 保持不变。此批不修改生产 API。

### 批次 2：复制边界

此批按依赖切片执行，不直接搬空历史目录：

- 已迁入 `Battle/Networking/Replication`：snapshot admission、权威快照状态、远端样本与 projection、interpolation playback、权威插值同步策略、复制 pipeline、可靠事件游标和同步健康评估。
- 已迁入 `Battle/Networking/Replication/Routing`：frame snapshot deserializer、shared decoder declarations、shared registry 及其生成注册代码。
- 已迁入 `Battle/Presentation/Snapshots`：spawn、despawn、enter-game、transform/state-hash 实体应用器、远端插值结果应用器，以及负责订阅并应用表现状态的 `BattleSyncFeature`。
- 已并入 `Client/Session/Features/Snapshot`：session-bound dispatcher、`BattleContext` command handler bridge、battle registry、混合 decoder/command declarations 及其生成注册代码。混合声明不能放入 Networking，否则会形成 Networking 到 Presentation 的反向依赖。
- 已迁入 `Battle/Networking/Replication/Streams`：远端输入/快照帧聚合、缓冲和保留窗口。`BattleLogicSession` 只依赖包内 `IRemoteFrameStreams` contract，由 Networking factory 提供默认实现；Session 继续负责客户端事件绑定和会话销毁，四个公开 source/sink facade API 保持不变。
- 已迁入 `Client/Prediction/FrameSync`：预测 mismatch、rollback 和 replay completion 的 reconciliation 采样与边沿报告。
- 已并入 `Client/Session/Features/Sim`：依赖 session state、handles、world catch-up 和 snapshot dispatcher 的远端预测世界同步 controller。
- 已迁入 `Battle/Testing`：只负责把同步策略适配给框架 Demo Harness 的 carrier。
- 历史 `Client/Synchronization` 已无剩余脚本，目录及其 `.meta` 已删除。

本批保持 namespace、类型名和公开 API；只为 Session 生命周期测试增加包内 stream contract 与注入构造器。版本控制内桌面工程的权威插值 controller 和 Demo Harness carrier 显式源码路径已同步更新，其余移动文件由 Unity runtime asmdef 自动收录。

### 批次 3：会话与模拟

将 `Client/Session` 拆成稳定的 `Session` 宿主与 `Simulation` 实现。`BattleSessionFeature.*` partial 文件按 lifecycle、networking、simulation 三组逐步减少，而不是继续增加 partial surface。

### 批次 4：表现层分类

- 已将历史 `EntityViewModel` 整体迁入 `Presentation/Entities`，保留 Components、Entities、Features 子目录以及全部脚本和目录 `.meta` GUID；namespace、类型名和公开 API 不变。
- 已将历史 `Battle/Hierarchy` 整体迁入 `Presentation/Composition`。该目录继续承载 View、VFX、HUD 共用的场景层级组合基础设施，四个脚本及目录 `.meta` GUID、namespace、类型名和公开 API 均保持不变。
- 已将 HUD 根目录中的输入控件构建、布局和 UI aggregate 共八个脚本迁入现有 `Presentation/Hud/Controls`，与输入 View、mapper、bridge 及底层 joystick/skill controls 归并；全部脚本 `.meta` GUID、namespace、类型名和公开 API 保持不变。
- 已将 HUD 根目录中的 input event binding、bridge、dispatcher 和 subscription list 共四个脚本迁入现有 `Presentation/Hud/Controls`，使输入控件事件适配链与其 View、mapper 和底层 controls 保持同一目录边界；全部脚本 `.meta` GUID、namespace、类型名和公开 API 保持不变。
- 已将 HUD 根目录中的 floating text controller、pool、handle、damage formatter 和 damage event presenter 共五个脚本迁入新建的 `Presentation/Hud/FloatingText`，归并浮字生命周期及伤害事件表现入口；全部脚本 `.meta` GUID、namespace、类型名和公开 API 保持不变，新目录使用独立 `.meta` GUID。
- 已将 HUD 根目录中的 skill presentation spec、aim preview、failure presenter、button template binder/resolver/config lookup/applier 和专用 player loadout resolver 共八个脚本迁入新建的 `Presentation/Hud/Skills`，归并技能规格解析、按钮模板应用、瞄准预览和失败提示表现链；全部脚本 `.meta` GUID、namespace、类型名和公开 API 保持不变，新目录使用独立 `.meta` GUID。
- 已将 HUD 根目录中的 HP bar controller、factory 和 handle 共三个脚本迁入新建的 `Presentation/Hud/HpBars`，形成角色生命条创建、更新、投影和销毁的最小闭合边界；全部脚本 `.meta` GUID、namespace、类型名和公开 API 保持不变，新目录使用独立 `.meta` GUID。既有 `Presentation/Hud/Buff` 保持独立职责目录。
- 已将 HUD 根目录中的 actor position resolver 及其契约、Canvas projector 共三个脚本迁入新建的 `Presentation/Hud/Projection`，形成 HP bar、Buff 和 FloatingText 共用的角色世界锚点解析与 HUD 坐标投影边界；全部脚本 `.meta` GUID、namespace、类型名和公开 API 保持不变，新目录使用独立 `.meta` GUID。Canvas 宿主生命周期、总 binder 和 entity lifecycle binding 继续留在 HUD 根目录，避免将宿主和运行时编排混入投影职责。
- 已将 HUD 根目录中的 fallback UI factory 迁入新建的 `Presentation/Hud/FallbackUi`，形成 HP bar 与 FloatingText 在 prefab 或资源缺失时共用的命令式 UI 创建边界；脚本 `.meta` GUID、namespace、类型名和公开 API 保持不变，新目录使用独立 `.meta` GUID。跨输入控件和 fallback UI 共用的 RectTransform 布局工具继续留在 `Presentation/Hud/Controls`，避免以底层工具依赖反向扩大 fallback UI 目录。
- 已将 HUD 根目录中的 Canvas controller 迁入新建的 `Presentation/Hud/Canvas`，归并共享 `UIRoot`/`UILayer.Main` 复用、fallback Canvas 创建与所有权销毁，以及 EventSystem 存在性保证；脚本 `.meta` GUID、namespace、类型名和公开 API 保持不变，新目录使用独立 `.meta` GUID。HUD feature、总 binder、输入协调、entity lifecycle binding 和 snapshot subscription controller 继续留在 HUD 根目录，作为跨 Controls、HP bar、Buff、FloatingText 与 Skills 的装配和运行时编排层，不为单个适配器创建碎片化职责目录。
- 后续只在运行时编排能够形成稳定闭包时继续拆分 HUD 根目录。所有 MonoBehaviour、Prefab 和 ScriptableObject 移动必须保留 `.meta`。

### 批次 5：命名空间与 asmdef 约束

逐子域调整 namespace，并在依赖图无环后评估 `Contracts`、`Networking`、`Presentation` 的 asmdef。不得为追求目录整齐而制造循环程序集引用。

## 7. 迁移检查清单

每批迁移必须满足：

1. 迁移前确认目标文件是否有未提交并行修改。
2. 源文件与 `.meta` 同时移动，保持 Unity GUID。
3. 搜索路径硬编码、资源路径、测试脚本和文档引用。
4. 不在同一批次进行无关重构或格式化。
5. 运行目标 fixture、完整相关程序集测试和 `git diff --check`。
6. 在批次总结中记录未迁移文件及原因。
