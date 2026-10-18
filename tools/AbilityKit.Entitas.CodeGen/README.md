# AbilityKit.Entitas.CodeGen

Entitas 组件代码生成器（设计时 CLI），取代已失管的 Jenny 链路。与 `tools/AbilityKit.Protocol.CatalogCompiler` 同级同模式：**产物 checked-in，由本工具生成/校验**，不进任何 Unity 包，零外部 NuGet 依赖（离线可跑；测试工程仅依赖仓库既有 xunit 栈）。

## 设计裁定

- **生成器定义规范形态，不迁就历史手改**（2026-10-12 裁定）：不做对现有生成目录的字节级兼容；手改不符合生成规则的，接入替换时修改对应逻辑迁移。
- 手改史的三类漂移仍在 pin 文件里显式化（这是项目侧事实，与模板形态无关）：
  1. **索引顺序漂移**：lookup 0-38 字母序 + 39-42 手工追加 → pin 锁定顺序；
  2. **未生成组件**：`ShieldContainer`/`SharedShieldPools` 定义了但从未生成 → pin 标 `generate: false`；
  3. **手写生成物**：`CharacterHfsm`/`ActorStateMachine` 的 API 文件含 Dispose 生命周期逻辑 → pin 标 `custom: true`（生成器跳过、存在性校验；接入时迁到手写 partial 后可摘掉）。

## 基线与示例包差异

- **主校验 = 生成基线清单**（`tools/entitas-codegen.moba.baseline.json`，path → LF 归一内容 SHA-256）：绿 = 生成器自洽；模板/模型有意的变更用 `-UpdateBaseline` 固化，无意的变化（回归）会红。
- **接入替换已完成（2026-10-12）**：示例包 `Generated/` 目录已由生成器接管（`cleanStale: true`，custom 手写文件受保护），交换后 `--diff-generated-dir` 报告 **65 一致 / 0 差异**；交换前后 `src/AbilityKit.Demo.Moba.Core` 均为 0 错误。**Unity 编辑器内编译是权威验证，尚未跑**（生成物只引用 Entitas 类型、API 签名未变，风险面极小）。
- 已做的归一化（历史怪癖 → 规范形态）：
  - Matcher 三处 10 空格缩进 → 8 空格；
  - `Entitas.IContext []` → `Entitas.IContext[]`；
  - 空 lookup 的三连空行怪癖 → 与有内容时同构；
  - 数组末项无逗号特例 → 统一尾逗号；
  - Context 构造里 AERC 行缩进对齐相邻实参；
  - `catch(` → `catch (`
  - 头部署名默认 `abilitykit`（本工具真实出处；`entitas-parity` 保留为可配选项）。
- **gate 已挂**：`tools/test-gates.json` 的 `entitas-codegen`（P1）= `tools/check_entitas_codegen.ps1`（全部项目基线校验）+ 生成器 29 项单测；`tools/run_test_gate.ps1 -Gate entitas-codegen`。

### 组件序列化策略（业务选项，2026-10-12 起）

组件序列化是**项目级选项而非工具强制**——不是每个 Entitas 项目都需要 ECS 回滚序列化。配置 `serialization.mode` 三档：

| 模式 | 语义 | pin 字段 |
|---|---|---|
| `off`（默认，通用中立） | 不生成序列化、无覆盖要求 | serialize/coverage 出现即报错（未启用的选项不悄悄生效） |
| `opt-in` | pin 里 `serialize:true` 主动加入 | serialize 有效；无 coverage 概念 |
| `auto`（MOBA 采用） | **默认全量 + 豁免制**：凡通过可序列化判定的组件自动入集；不入集必须在 pin 用 `coverage` 声明豁免理由 | serialize 字段无效（报错）；coverage = 豁免注册表 |

`auto` 的 fail-closed 语义：新平值组件**自动受保护**（想不备都不行，除非豁免）；新 blob 组件不声明豁免则直接报错逼作者归类。豁免词汇：`provider:<名>`（服务侧真相源）/ `immutable`（出生赋值，实体重建覆盖）/ `projection`（每帧重投影）/ `transient`。

设计裁定（2026-10-12，对照 UnityLockstep/ET 帧同步分析）：全量备份哲学以业务选项落地，工具保持通用；blob 组件无法进全量是投影架构的边界而非工具缺口；组件层序列化用生成的逐字段二进制（深拷贝语义，外层 MemoryPack payload 包装），不用 MemoryPack 库本体（组件类型未标注，Fixed64/Vec3/句柄会卡死）。

### 序列化已知类型与运行时修复（2026-10-12）

- 序列化器内置定点/向量类型展开规则：`Fixed64`（RawValue 往返）、`Vec3`（XYZ float 往返）——平值组件的覆盖面从"纯基元"扩到定点向量；
- **运行时修复**：`MobaActorTransformRollbackProvider` payload v3 补齐 `MotionState.Velocity/Time` 恢复（此前回滚只恢复 Position/Forward，速度是预测帧残留值，重模拟首帧可能发散）；71/71 回滚测试绿。

### 接入后待办

1. ~~CharacterHfsm 迁生成~~ 已完成（2026-10-12）：Dispose 语义迁至 `Runtime/Domain/Components/EntityApiExtensions.cs` 的 `RemoveAndDisposeCharacterHfsm` 扩展，三处调用点已更新；生成目录只剩 `ActorStateMachine` 一个 custom 手写文件；
2. ~~护盾双组件处置~~ 已完成（2026-10-12）：两个 `[Actor]` 死定义已删除（领域模型保留，护盾 ECS 化待重新规划）；
3. Unity 编辑器权威编译：batchmode 尝试因 Unity 版本不匹配失败（gate 默认 2022.3.62f1，项目 Library 为用户编辑器 f3 所建；日志 0 条 error CS，API-updater 节点失败属环境问题）——下次用 f3 编辑器打开即闭环；
4. ~~序列化生成~~ 已落地（2026-10-12）：`ActorComponentSerialization.cs`（10 个平值组件）+ 覆盖归类审计 + Transform provider v3 运动学补全；
5. 已知冗余：`SkillLoadout` 已全量序列化，`MobaSkillCooldownRollbackProvider` 与之重复覆盖（写入同值，无害）——下次动回滚注册表时可一并清理。
6. ~~context 收敛~~ 已完成（2026-10-14）：配置从 `Actor/Game/Input/Service` 收敛为 **`Actor` + `Game`**。`Input`/`Service` 两个空壳（0 组件、0 消费者）删除——输入经 `MobaSkillInputCommandHandler` 直达 `SkillCastCoordinator`，服务经 `[WorldService]` 进 DI，两者与 ECS 职责重复。`Game` 保留为"战斗内全局"的显式容器（当前 0 组件），等真有全局实体事实时再标 `[Game]`；判断准则：**服务事实进 DI，实体事实进 `[Game]`**，不要把单例塞进 entity 重蹈"状态权威分裂"。`MobaEntitasContextsExtensions` 只保留 `Actor()`/`Game()`。

## 目录划分（与命名空间一致）

```
AbilityKit.Entitas.CodeGen/
  Program.cs                    CLI 参数解析（瘦壳）
  Runner/                       CodeGenRunner 编排（CLI 与测试共用入口）
  Configuration/                CodeGenConfig(schema 2) + PinFile + ConfigStore(读取抽象/发现/有效配置导出)
  Model/                        管线数据契约(CodeGenModel/ComponentInfo) + ModelBuilder(数据提供者)
  Scanning/                     IComponentSourceScanner + 正则实现 + TypeIndex + TypeResolver
  Pipeline/                     三段式管线(FileSet/IGenerator/IPostProcessor)
    Generators/                 四个生成器（Context / ComponentApi / Contexts / Feature）
    PostProcessors/             行尾规范化（扩展位：manifest/meta）
  Templates/                    模板（Feature.cs 为内嵌资源，不参与编译）
  Output/                       写盘器（EOL/BOM 按配置、cleanStale）
  Compare/                      CompareReport + BaselineStore(基线清单)
  Diagnostics/                  退出码约定 + 结构化错误 + 控制台门面
```

扩展方式：

| 要加什么 | 动哪里 |
|---|---|
| 新输出物（archetype matcher / entity index / manifest） | `Pipeline/Generators/` 新增 `IGenerator` + `Pipeline.CreateDefault()` 注册一行 |
| 扩大序列化覆盖 | pin 里给组件加 `"serialize": true`（平值组件；ModelBuilder 会校验可行性并拦截 blob 组件） |
| 新后处理（如生成 .meta、hash manifest） | `Pipeline/PostProcessors/` 新增 `IPostProcessor` |
| ~~编译器级源解析~~ | **已落地**：`scanner: "roslyn"`（语义级 FQN 解析，字段类型短名定位闭包，19s；parity 测试保证与 regex 生成物逐字节一致）；默认仍 regex（5s、零依赖） |
| ~~analyzer 护栏~~ | **已落地**：`EntitasComponentGuardAnalyzer`（AKSG9201 blob 字段须 [EntitasBlob] 标注 / AKSG9202 非 public 字段），在 com.abilitykit.demo.moba.codegen；9 个 blob 组件已显式标注 |
| 新配置格式（YAML 等） | 实现 `Configuration/IConfigReader` |

## 配置体系（schema 2）

- **存放与发现**：约定 `tools/entitas-codegen.<project>.json`；`--project moba` 按约定解析（repoRoot 从 CWD 向上找 `.git`），`--config` 显式指定任意路径，两者互斥；都不传时若只有一个项目配置则自动选中。
- **格式**：JSON（`IConfigReader` 抽象，将来可加 YAML 实现）。严格模式：未知键、schema 版本、枚举值、路径存在性全部校验，exit 2。
- **路径**：相对仓库根（配置文件位置向上找 `.git`）。
- **有效配置导出**：`--export-config <path>` 输出默认值填满、路径归一为仓库相对的最终生效配置——评审/CI 固化用，看到的即运行时所用。

```jsonc
{
  "schema": 2,
  "project": "moba",
  "sources":   { "components": ["<组件定义目录>"] },            // 可多目录
  "typeIndex": { "roots": ["<类型索引扫描根>"], "skipDirs": [...] },
  "output": {
    "generatedDir": "<对比基准与正式产物目录>",
    "encoding": "utf8-bom",           // utf8-bom | utf8
    "lineEndings": "crlf",            // crlf | lf
    "attribution": "abilitykit",      // abilitykit | entitas-parity
    "cleanStale": false               // 写盘删陈旧生成文件（含 .meta），接入替换时才开
  },
  "editor": {                         // 缺省 = 复刻现状；纯服务端/无 Editor 目标按需改
    "emitContextObserver": true,      // Contexts.cs 的 UnityEngine observer partial
    "emitFeatureClass": true,         // Feature.cs 可视化调试基类
    "defineSymbols": ["UNITY_EDITOR"] // #if 守卫宏，多宏 && 连接
  },
  "contexts": ["Actor", "Game", "Input", "Service"],
  "pin": "tools/entitas-codegen.moba.pin.json",
  "baseline": "tools/entitas-codegen.moba.baseline.json"  // 主校验目标；未配置则退回对比 generatedDir
}
```

### 特定环境（生产/纯服务端）不需要 Unity Editor 宏

生成物里仅两处依赖 `UNITY_EDITOR` 守卫：Contexts observer partial（引用 `UnityEngine`）与 Feature.cs。两条路：

1. **整个不要**：`editor.emitContextObserver=false` + `editor.emitFeatureClass=false` → 产物零 UnityEngine 引用、无编辑器代码；
2. **要但换宏**：`editor.defineSymbols=["AK_SERVER"]` 之类 → 守卫换成项目自定义宏，产物在自定义条件下才带调试代码。

## 用法

```powershell
./tools/generate-entitas.ps1 -Project moba                     # 基线校验（退出码非零 = 生成行为漂移）
./tools/generate-entitas.ps1 -Project moba -Write              # 生成到 local/Logs/entitas-codegen/out（草稿，不动示例包）
./tools/generate-entitas.ps1 -Project moba -UpdatePin          # 新增组件后追加 pin（索引尾部追加，稳定）
./tools/generate-entitas.ps1 -Project moba -Validate           # 只校验配置/源/pin（doctor）
./tools/generate-entitas.ps1 -Project moba -UpdateBaseline     # 模板/模型有意的变更后固化新基线
./tools/generate-entitas.ps1 -Project moba -DiffGeneratedDir   # 报告与示例包目录的差异（预期增量，不影响退出码）
./tools/generate-entitas.ps1 -Project moba -ExportConfig local/Logs/effective.json
```

**退出码**：0 一致 / 1 漂移或差异 / 2 用法或配置错误 / 3 源码校验失败 / 4 内部错误（gate 与 CI 依赖此约定）。

**安全约束**：默认只校验不写盘。`--write` 写到 `local/Logs/entitas-codegen/out`；写回示例包必须同时 `--out <GeneratedDir>` + `--force-generated-dir`（接入替换时才用）。

## 测试

```powershell
dotnet test tools/AbilityKit.Entitas.CodeGen.Tests/AbilityKit.Entitas.CodeGen.Tests.csproj
```

29 个测试覆盖：**Golden 基线**（全管线对照 checked-in 基线清单）、**示例包差异仅信息性**（`--diff-generated-dir` 不影响退出码）、**模板归一化**（Matcher 统一缩进/`IContext[]`/空 lookup 同构/署名默认 abilitykit）、**基线防篡改**（固化→绿、篡改→红）、项目发现、漂移与 pin（未知组件/过期条目/update-pin 恢复/generate:false）、源校验（属性/方法/非 public 字段/缺 context 属性）、配置严格性（未知键/schema 互斥/导出有效配置）、编辑器开关与守卫宏、类型解析（基元/数组/泛型/BCL/歧义）、flag 组件 API、管线确定性。

## 已知边界

- 字段类型解析是"using 顺序 + 类型索引"启发式（别名/扩展方法不支持，遇到报错而非静默）；
- 不支持 `[Unique]`/`[Event]`/`[Cleanup]`（当前 45 个组件均未使用）；
- 组件类声明 `: IComponent` 之外的接口列表被忽略（当前无此写法）；
- 尚未挂 `tools/test-gates.json` gate（接入替换时随 `-Check` gate 一起挂）；
- `[PrimaryEntityIndex]` 被解析但无对应生成器（全仓 `GetEntityWith` 零命中——历史从未生成过索引代码）；补 entity index 时新增 `IGenerator`。
