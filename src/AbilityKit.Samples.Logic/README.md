# AbilityKit.Samples.Logic

> AbilityKit 的纯逻辑示例集，用来帮助新人从零到一理解框架理念、模块边界和按需组合方式。

## 示例定位

`AbilityKit.Samples.Logic` 不绑定 Unity、MonoGame 或具体服务器框架。它只保留玩法逻辑、配置模型、上下文、日志和时间推进等纯 C# 内容，运行环境由外层宿主提供。

这意味着同一份 sample 可以：

- 在控制台运行并输出日志。
- 写入文件，方便回归检查和教学留档。
- 未来接入 Unity、MonoGame 或服务器宿主，由宿主提供时间、资源、配置和输出。

## 推荐学习路径

| 阶段 | 分类 | 目标 |
| --- | --- | --- |
| 00 | Onboarding | 理解 AbilityKit 是工具集合，不是必须整包接入的单体框架 |
| 01 | Foundation | 理解日志、事件、对象池、类型注册等基础设施 |
| 02 | Tags / Config | 理解玩法词汇、配置数据和类型映射 |
| 03 | Pipeline / Flow | 理解一次性阶段执行和跨帧流程编排 |
| 04 | Triggering / HFSM / Modifiers | 理解事件规则、状态切换和属性变化 |
| 05 | World / Battle Runtime / Sync | 理解运行时生命周期、服务容器、Host 边界、固定帧战斗循环、输入帧同步、快照路由和状态差异同步 |
| 06 | Combat / Demo | 阅读战斗子模块与端到端示例，学习模块组合方式 |

建议新人先运行 `Onboarding` 与 `Foundation` 分类中的入门示例。菜单里的数字只是当前宿主渲染出的临时 index，不应写死到文档或 UI 中；稳定入口应使用 `sample-manifest.json` 中配置的 `id`。

## Starter 推进顺序

`AbilityKit.Samples.Logic` 已经包含 Pipeline、Triggering、Modifiers/属性、Flow、World、Sync 等正式示例，并且可以通过 `--web` 导出静态 Web 界面。Starter 的作用不是重写这些示例，而是提供一条更短的官方接入路线，把已有能力按 Foundation、SkillCore、BattleRuntime、SyncRuntime 串起来。

| 阶段 | 稳定 id 建议 | 目标 | 依赖边界 |
| --- | --- | --- | --- |
| Foundation Starter | `starter/foundation` | 展示日志、事件、对象池、World DI 和宿主驱动 Tick | 只依赖 `AbilityKit.Core`、`AbilityKit.World.DI` 和 sample 抽象 |
| SkillCore 路线 | `pipeline/basic-phases`、`triggering/basic-event-trigger`、`triggering/condition-blackboard`、`modifiers/attribute-basic` | 复用已有示例展示技能阶段、属性变化、触发规则和结构化输出 | 在 Foundation 后按现有 manifest 路线阅读，不重复造 `starter/skill-core` |
| BattleRuntime Starter | `starter/battle-runtime` | 展示目标选择、命中、伤害和 Trace/诊断输出 | 在 SkillCore 上增加 `Combat.Targeting`、`Combat.Projectile`、`Combat.Damage` |
| SyncRuntime Starter | `starter/sync-runtime` | 展示输入帧、快照应用、状态校验和回放基础 | 只面向需要同步/回放的项目 |

第一阶段优先补 `starter/foundation`，它用于证明基础包可以离开 Demo 独立运行。完成后不是重复实现 SkillCore 示例，而是把现有 Pipeline、Triggering、Modifiers/属性示例收编成推荐路线，并通过 Web 导出展示同一套学习路径。

Starter 验收标准：

- 能通过 `dotnet run --project src/AbilityKit.Samples -- --id <stable-id>` 运行。
- 能通过 `--validate-manifest` 校验 manifest 元数据。
- 输出使用 `Section`、`Bullet`、`KeyValue` 等结构化方法，不依赖解析自由文本。
- 不直接引用 Unity API、Demo 包或项目业务资产。
- README 中能说明该 Starter 对应的包组合和下一步扩展路径。

- `onboarding/orientation`：项目定位、解决的问题、阅读 sample 的方法。
- `onboarding/host-boundary`：纯逻辑与控制台、文件、游戏宿主的边界。
- `onboarding/package-composition`：如何根据玩法需求选择模块。
- `onboarding/skill-slice`：把标签、检查、执行、持续效果和事件串成一个技能切片。
- `onboarding/ui-host`：模拟界面宿主列目录、点击运行、收集结构化日志。
- `foundation/environment-tick`：理解时间推进由宿主驱动，sample 只响应 tick。
- `onboarding/web-timeline-snapshot`：理解 Web timeline 观察器怎样消费结构化快照。
- `tags/basic-tags`：学习 GameplayTags 的注册、层级匹配和容器操作。
- `tags/requirements`：学习用 Required / Blocked 标签表达技能释放、目标过滤和互斥状态。
- `pipeline/basic-phases`：学习用 Pipeline 串联预检查、验证、施法、执行和冷却阶段。
- `flow/basics`：学习用 FlowRunner 和 WaitSecondsNode 处理宿主驱动的跨帧流程。
- `flow/sequence-race`：学习用 SequenceNode、RaceNode 和 ParallelAllNode 组合跨帧节点。
- `flow/skill-cast-timing`：学习用 RaceNode 表达施法前摇与取消请求的竞态。
- `triggering/basic-event-trigger`：学习用 EventBus、TriggerRunner 和 ITrigger 构成最小事件触发闭环。
- `triggering/condition-blackboard`：学习用 DictionaryBlackboard 为 Trigger Evaluate 提供运行时条件。
- `modifiers/attribute-basic`：学习用 ModifierData 和 ModifierCalculator 计算装备、Buff 与覆盖效果。
- `continuous/dot-lifecycle`：学习用 IContinuous 与 IContinuousManager 管理 DOT 的 Tick、暂停、恢复和中断。
- `hfsm/basic-state`：学习用 StateMachine、State 和 Transition 管理 Idle、Casting、Dead 等基础状态。
- `hfsm/trigger-bridge`：学习用 EventBus 把战斗事件桥接到 HFSM trigger transition。
- `world/lifecycle`：学习用 WorldTypeRegistry、RegistryWorldFactory 和 WorldManager 管理 World 生命周期。
- `world/di-basics`：学习用 WorldContainerBuilder 注册和解析 World 内服务。
- `world/host-client`：学习用 HostRuntime 管理客户端连接、消息广播和 World 生命周期消息。
- `battle/runtime-loop`：学习用 BattleInputFrameScheduler、BattleInputBuffer 和 BattleTickDriver 组织固定帧输入与战斗 Tick。
- `sync/input-frame`：学习用 FramePacket、RemoteFrameAggregator 和 WorldManagerFrameDriver 聚合远端输入并推进 World。
- `sync/world-snapshot`：学习用 SnapshotRoutingBuilder、FrameSnapshotDispatcher 和 SnapshotPipeline 解码并应用 World 快照。
- `sync/state-diff-apply`：学习用 SnapshotBuffer、StateManager、StateDiffProvider 和 StateHashComputer 捕获、差异化、回滚并校验状态。

## 运行方式

在仓库根目录运行：

```powershell
dotnet run --project src/AbilityKit.Samples -- --list
dotnet run --project src/AbilityKit.Samples -- --id onboarding/orientation
dotnet run --project src/AbilityKit.Samples -- --web sample-web
dotnet run --project src/AbilityKit.Samples -- --all --file --output sample-output
```

常用参数：

| 参数 | 说明 |
| --- | --- |
| `--list` | 打印所有示例菜单 |
| `--run <index>` | 运行指定序号示例 |
| `--id <stable-id>` | 通过 manifest 中的稳定 id 运行指定示例 |
| `--all` | 运行全部示例 |
| `--mode <instant|simulated|realtime>` | 选择时间推进模式 |
| `--file` | 将示例输出写入日志文件 |
| `--output <directory>` | 指定文件输出目录，同时启用 `--file` |
| `--web [directory]` | 导出一个可直接打开的静态网页，不需要持续运行 HTTP 服务 |
| `--no-console` | 不向控制台输出，只保留其他输出通道 |
| `--validate-manifest` | 校验 `sample-manifest.json` 与扫描到的 `[Sample]` 类型是否一致 |

## 接入带界面的宿主

sample 的纯逻辑层不直接依赖控制台。带界面的宿主可以用 `SampleCatalogProvider` 获取菜单数据，用 `SampleExecutionService` 在按钮点击时运行指定示例。

```csharp
using AbilityKit.Samples.Abstractions;
using AbilityKit.Samples.Logic;

var catalog = SampleCatalogProvider.CreateCatalog();
var executor = new SampleExecutionService(
    catalog,
    mode => MyEnvironmentFactory.Create(mode));

foreach (var entry in catalog.Entries)
{
    // UI 可绑定 entry.Index、entry.Id、entry.Title、entry.Description、entry.Category、entry.Tags
    AddButton(entry.Title, onClick: () =>
    {
        var logger = new BufferedSampleLogger();
        var result = executor.RunById(entry.Id, logger, new SampleRunOptions
        {
            HostKind = SampleHostKind.Web,
            ExecutionMode = ExecutionMode.Simulated
        });

        RenderLog(logger.Entries);
        RenderResult(result.Succeeded, result.ErrorMessage);
    });
}
```

宿主需要自己实现或复用：

- `ILogger`：把日志渲染到 UI 面板、控制台、文件或引擎日志。
- `ISampleEnvironment`：由宿主推进时间，Unity/MonoGame 可以在 Update 中 Tick。
- `IConfigProvider` / `IResourceProvider`：按需要接入文件、内存、Addressables 或远端资源。

## Web 模式与持续驱动

当前先接入的是“静态网页导出模式”。它适合这个仓库当前还是控制台程序的阶段：

```powershell
dotnet run --project src/AbilityKit.Samples -- --web sample-web
```

命令会执行 sample，生成 `sample-web/index.html`。之后可以直接用浏览器打开这个 HTML 文件；改代码后重新执行命令，再刷新浏览器即可，不需要持续开启 HTTP 服务。

生成的页面采用无依赖的 Canvas 时间轴观察器：导出阶段会把 sample 的结构化日志转换为 timeline event，浏览器端提供播放、暂停、单步、倍速、事件卡片和日志联动。第一版先用通用日志事件构建回放壳，后续高价值 combat/world/flow 示例可以继续补充更细的帧快照，例如实体位置、投射物轨迹、AOE 范围、状态变更和命中事件。

这个模式的边界也要明确：浏览器打开本地 `file://` 页面时，不能直接启动本机 .NET 进程，所以网页里的“点击示例”展示的是导出时已经执行并嵌入页面的数据。真正的在线点击即跑，需要 WebAssembly 或后端服务。

如果后续需要真正的网页实时运行，可以升级为以下模式：

- Blazor WebAssembly / .NET WebAssembly：sample 逻辑直接在浏览器侧运行。
- Web 前端 + 后端 .NET：浏览器点击按钮，后端执行 sample 并回传结构化日志。
- Unity WebGL / 其他引擎 Web 构建：由引擎层实现 `ILogger` 和 `ISampleEnvironment`。

对于真正实时运行的 Web/Unity/MonoGame 宿主，持续驱动不应该由 sample 自己创建主循环，而应该由宿主提供：

```csharp
var logger = new BufferedSampleLogger();
var handle = executor.StartById("flow/basics", logger, new SampleRunOptions
{
    HostKind = SampleHostKind.Web,
    ExecutionMode = ExecutionMode.Simulated
});

// Web: requestAnimationFrame / timer
// Unity: Update()
// MonoGame: Game.Update()
handle.Tick(deltaTime);
RenderLog(logger.Entries);
```

也就是说，持续能力来自 `ISampleEnvironment` 和 `SampleRunHandle.Tick(deltaTime)`。Web 宿主可以用 `requestAnimationFrame` 计算 delta，再调用 handle；MonoGame/Unity 则在各自的 Update 中调用。sample 内部只订阅环境 tick 或使用 Flow/Pipeline 的 Step，不直接依赖具体平台。

## Manifest 治理

`sample-manifest.json` 是正式示例入口的来源。控制台菜单、`--list`、`--all` 和 Web 导出默认只展示 manifest 中 `Stable` / `Candidate` 状态的正式入口；仍只有 `[Sample]` 标记的旧示例保留为开发期扫描项，可通过 `SampleCatalogProvider.CreateDevelopmentCatalog()` 做迁移排查。后续新增正式示例时，除了 `[Sample]` 标记外，还应在 manifest 中补齐：

- `status`：示例生命周期，建议使用 `Stable`、`Candidate`、`Legacy`、`Deprecated`。
- `level`：学习难度，例如 `Beginner`、`Intermediate`、`Advanced`。
- `modules`：该示例实际展示的框架模块。
- `tags`：分类、能力、质量和宿主标签；Web 示例应同时带 `web` 与 `deterministic`。
- `next`：推荐继续阅读的稳定示例 id。

可以用以下命令检查治理状态：

```powershell
dotnet run --project src/AbilityKit.Samples -- --validate-manifest
```

校验会输出 manifest 条目数、扫描到的 sample 类型数、未进入 manifest 的 attribute-only 示例、重复 id/order、缺失类型、缺失元数据、非法状态、失效的 `next` 引用，以及默认正式目录是否只包含 manifest-backed 的 `Stable` / `Candidate` 入口。它也会检查结构化输出契约是否存在 `SampleOutputContract.SchemaVersion`，以及 `BufferedSampleLogger` 是否能生成从 0 开始递增的 `Sequence`。默认正式目录不会展示 attribute-only 示例，后续应逐步把仍有教学价值的旧示例迁移到 `Stable` / `Candidate`，把只保留兼容意义的入口标记为 `Legacy` / `Deprecated`。

## 宿主能力矩阵

示例逻辑通过 `SampleRunOptions.HostCapabilities` 和 `SampleRuntimeContext.HostCapabilities` 识别宿主能力，而不是直接判断宿主名称。默认能力由 `SampleHostCapabilities.ForHost(...)` 推断，后续 Unity、MonoGame、Web、Console 等宿主都应按这个矩阵声明自己能做什么，而不是让 sample 反向猜测宿主实现细节。推荐优先使用这些能力位：

- `SupportsInteractiveSelection`：是否支持交互式菜单或按钮选择。
- `SupportsInstantRun`：是否支持一次性同步执行 sample。
- `SupportsHostDrivenTicks`：是否支持宿主持有 `SampleRunHandle` 并手动驱动 tick。
- `SupportsRealtimeTicks`：是否支持按真实时间推进。
- `SupportsFileOutput`：是否支持文件日志。
- `SupportsStructuredOutput`：是否支持结构化输出消费。
- `SupportsResourceLoading`：是否支持资源加载服务。

后续新增宿主时，应先声明能力，再决定 sample 是单次运行、逐帧驱动，还是走结构化输出/UI 渲染。

## 输入治理

示例运行输入统一收敛为 `SampleRunRequest`，用于表达“运行哪个 sample”以及“使用哪些运行选项”。CLI 的 `--run`、`--id`、`--all` 会先转换成请求对象；后续 Unity、MonoGame、Web UI 的按钮、下拉框或列表选择也应生成同一种请求，再交给 `SampleExecutionService` 或宿主 runner 执行。

请求模型只覆盖运行选择，不承载 `--help`、`--list`、`--web`、`--validate-manifest` 这类应用命令，避免把 sample 运行契约和宿主管理命令混在一起。推荐使用方式：

- `SampleRunRequest.ByIndex(...)`：临时菜单 index，适合当前会话内选择。
- `SampleRunRequest.ById(...)`：稳定 manifest id，适合 UI、收藏、配置和自动化。
- `SampleRunRequest.All(...)`：运行当前 catalog 内所有正式入口。
- `SampleRunOptions`：跟随请求传入执行模式、宿主类型、宿主能力和输出选项。

## 图文讲解治理

示例可以在 `sample-manifest.json` 中通过 `guide` 扩展图文讲解内容，字段保持宿主无关，便于 Web、Unity、MonoGame 或文档站按各自 UI 渲染：

- `purpose`：这个示例解决什么学习问题。
- `observe`：运行时重点观察哪些输入、流程或输出。
- `takeaway`：迁移到真实项目时应该带走的设计结论。
- `visualKind`：宿主可选的图示类型，例如 `flow`、`timeline`、`stack`。
- `visualSteps`：用于绘制简易流程图、阶段图或时间线的步骤标签。

Web 导出会把 `guide` 写入每个 sample 的 JSON，并在画布中按 `visualKind` 渲染与示例语义相关的结构图：`flow` 用于能力链路，`stack` 用于分层边界，`timeline` 用于阶段推进。日志时间线只作为底部辅助信息，不应再生成与示例无关的装饰性折线图。没有配置 guide 的旧示例仍可正常运行，只会显示默认提示；质量门禁会把不完整 guide 作为推荐元数据问题提示，方便后续逐步补齐。

当示例仅有日志和描述仍不足以解释“代码做了什么”时，应在 manifest 中补充 `codeWalkthrough`。每个步骤通过 `sourceFile`、`startLine`、`endLine` 定位源码块，通过 `explanation` 说明代码意图，并用 `outputHint` / `visualStep` 关联结构化输出和图示节点。Web 导出会展示“代码讲解”面板，点击步骤可跳转到相关输出帧；Unity、MonoGame 或文档站也可以复用同一份元数据渲染源码导览。

## 输出治理

示例输出使用 `SampleOutputContract.SchemaVersion` 标识结构化协议版本，当前版本为 `sample-output.v1`。控制台仍保持面向人的文本格式；`BufferedSampleLogger` 会把同一份输出捕获为结构化 `SampleLogEntry` 列表，每条记录包含：

- `Sequence`：单次 sample run 内从 0 开始递增的稳定序号。
- `Kind`：`Info`、`Warn`、`Error`、`Section`、`Line`、`Divider`、`Bullet`、`Numbered`、`KeyValue`。
- `Text` / `Key` / `Number`：按输出类型保存文本、键值对 key 或编号。

Web 导出会在文档根节点写入 `outputSchemaVersion`，并在每条日志里保留 `sequence`，便于 UI、测试和后续 timeline 渲染按稳定协议消费。正式示例应优先使用 `Section`、`Bullet`、`KeyValue` 表达输入、运行步骤和结果，避免拼接不可解析的大段说明文本。

宿主切换时的判断顺序建议是：先看能力矩阵，再看输出协议，最后才决定是否提供额外 UI、文件或交互入口。这样 sample 逻辑可以保持一致，只让宿主适配层发生变化。

## 当前接入排查

已经比较明确接入框架包能力的示例：

- `Onboarding/FromConceptToSkillSlice.cs`：`GameplayTags + Pipeline`。
- `Onboarding/SampleHostIntegration.cs`：`SampleCatalogProvider + SampleExecutionService + BufferedSampleLogger`。
- `Flow/FlowBasics.cs`：`AbilityKit.Flow` 的 `FlowRunner / FlowContext / SequenceNode / WaitSecondsNode / ActionNode`。
- `Tags/*`：`AbilityKit.GameplayTags`。
- `Pipeline/*` 和 `Demo/ProgressiveSkill_Phase4.cs`：`AbilityKit.Pipeline`。
- `Modifiers/*` 与 `Continuous/*`：`AbilityKit.Modifiers` / `AbilityKit.Continuous`。
- `World/*`：`AbilityKit.Host` / `AbilityKit.World.DI`。
- `Sync/*`：`AbilityKit.World.FrameSync` / `AbilityKit.World.NetworkFragments` / `AbilityKit.World.Snapshot` / `AbilityKit.World.StateSync`。
- `Targeting/TargetingBasics.cs`：`AbilityKit.Combat.Targeting`。

示例框架包的整体重构规划见 `Document/SamplesLogicRefactorPlan.md`。这份规划负责样例包定位、学习路径、manifest/catalog 治理、旧示例迁移、复杂示例扩展方法和验收标准。

框架能力矩阵与从零到一示例路线图见 `Document/SamplesCapabilityRoadmap.md`。这份规划按宿主、基础设施、执行编排、响应式状态、World、Combat、同步、回放和完整 Demo 分层，用于指导后续 manifest 扩容、旧示例重写、Web 可视化快照和复杂 Demo 建设。

战斗相关 sample 的后续拆分规划见 `Document/CombatSampleCoveragePlan.md`。这份规划按实体索引、技能库、目标查找、伤害、碰撞、运动、投射物、World/同步/回放拆分，后续补示例时优先按其中的最小优先清单推进。

仍建议后续继续清理的旧示例：

- `Flow/SequenceAndRace.cs`、`Flow/TimedFlow.cs`、`Flow/FlowAdvancedExample.cs` 还偏说明文风格，应逐步改成真实节点运行。
- `Triggering/*` 中有一批示例仍偏概念日志，应优先对齐 Unity package 下 `com.abilitykit.triggering/Samples` 的真实 API。
- 早期 `Foundation/EventSystem.cs`、`ObjectPool.cs`、`MarkerRegistry.cs` 需要继续确认是否应直接接入 `Core.Common` 包能力，还是作为 sample 基础设施概念保留。

## 目录结构

```text
AbilityKit.Samples.Logic/
├── Samples/
│   ├── Onboarding/        # 新手导览：框架定位、宿主边界、按需组合、技能切片、UI宿主
│   ├── Foundation/        # 基础设施：HelloWorld、事件、对象池、类型注册
│   ├── Tags/              # GameplayTags、TagContainer、TagRequirements、TagStack
│   ├── Config/            # 配置模型、Attribute 注册、配置表加载
│   ├── Pipeline/          # 技能阶段、执行器、配置驱动 Pipeline
│   ├── Flow/              # 流程节点、Sequence、Race、TimedFlow
│   ├── StateMachine/      # HFSM、行为、触发和配置化状态机
│   ├── Triggering/        # 触发器、条件、Blackboard、调度器、TriggerPlan
│   ├── Continuous/        # 持续行为
│   ├── Modifiers/         # 属性修改器、叠层、衰减、与 HFSM/Continuous 集成
│   ├── Targeting/         # 目标搜索
│   ├── World/             # World 生命周期、DI、Host、客户端管理
│   ├── Sync/              # 帧同步、输入帧聚合、快照和状态差异
│   └── Demo/              # 综合示例和渐进式技能示例
├── Ability/               # 早期技能系统分层示例和测试
├── Infrastructure/        # sample 内部配置模型、资源提供器和注册表辅助代码
└── sample-manifest.json   # 稳定 id、展示顺序、标题和标签配置
```

## 编写新示例的约定

- 示例逻辑继承 `SampleBase`，通过 `[Sample(priority, tags)]` 自动注册。
- 示例只表达纯逻辑，不直接依赖 Unity API、MonoGame API 或控制台静态输出。
- 输出使用 `Log`、`Section`、`Bullet`、`KeyValue` 等方法，方便宿主重定向到控制台、文件、结构化缓冲区或 Web 导出。
- 运行前先确认宿主能力矩阵，避免在不支持逐帧或交互的宿主中写死行为分支。
- 时间推进使用 `Environment` / `AdvanceTime` / `SimulateFrames`，不要在逻辑层直接阻塞线程。
- 复杂示例先说明“它解决什么问题”，再展示“用哪些模块组合解决”。

## 与真实项目的关系

当前仓库仍处于开发期，`src` 下的 sample 目标是教学和验证框架设计。真实项目不需要复制全部示例，也不需要接入所有包；应根据项目需求按需选择 AbilityKit 模块，并参考 `Demo` 与 `demo.moba.*` 的组合方式落地。
