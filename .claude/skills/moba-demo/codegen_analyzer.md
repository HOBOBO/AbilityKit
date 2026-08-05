# MOBA CodeGen 与 Analyzer

MOBA 专用编译期能力由 `com.abilitykit.demo.moba.codegen` 维护。框架通用生成器仍归 `com.abilitykit.codegen`，框架通用分析器仍归 `com.abilitykit.analyzer`。

## 正式路径

```text
runtime 中的 attribute/类型声明
    -> MOBA Generator 读取共享 Contract
    -> 生成 partial Manifest、强类型工厂或 accessor
    -> runtime 默认消费生成结果

同一份 Contract
    -> MOBA Analyzer 报告不合法声明
    -> Generator 只过滤无效输入，不重复报告诊断
```

遵循以下边界：

- 新的 MOBA 内建类型走生成路径，不再手写中心注册表。
- 反射仅用于外部程序集、legacy 注册或生成结果缺失时的兼容 fallback。
- 不要为了清零所有反射而扩展生成器；只有稳定重复模式或高频错误值得新增编译期能力。
- 修改 Generator、Analyzer、共享 Contract、生成入口或 Manifest 时，必须运行 `moba-codegen` P1 门禁。

## 已覆盖的声明

| 业务 | 声明入口 | 生成结果 | 主要诊断 |
|------|----------|----------|----------|
| 配置表 | assembly `MobaConfigTable` | `MobaGeneratedConfigTableManifest` | `AKSG1001`-`AKSG1002` |
| PlanAction | `PlanActionModule` | `MobaGeneratedPlanActionManifest` | `AK2001`-`AK2006` |
| Payload 字段 | `GeneratePayloadFieldIds` | partial accessor/字段 ID | `AKSG2001` |
| 事件映射 | `MobaTriggerEvent` | `MobaGeneratedEventMappingManifest` | `AKSG3001`-`AKSG3002` |
| 目标查询工厂 | `MobaTargetSourceProvider` / `MobaTargetFilter` / `MobaTargetOrder` / `MobaTargetSelect` | `MobaGeneratedTargetQueryFactoryManifest` | `AKSG4001`-`AKSG4002` |
| 弹射物发射器 | `MobaProjectileEmitter` | `MobaGeneratedProjectileEmitterManifest` | `AKSG5001`-`AKSG5002` |
| Bootstrap Stage | `MobaBootstrapStage` | `MobaGeneratedBootstrapStageManifest` | `AKSG6001`-`AKSG6002` |
| 行为树节点 | 指定 namespace 下的节点类型约定 | `MobaGeneratedBTreeNodeManifest` | `AKSG7001`-`AKSG7002` |
| Snapshot emitter | `MobaSnapshotEmitter` | `MobaGeneratedSnapshotEmitterManifest` | `AKSG8001` |
| Battle route / input handler | `MobaBattleRoute` / `MobaInputCommandHandler` | route 与 input-handler Manifest | `AKSG9001`-`AKSG9005` |

生成器和分析器必须复用 `DotNet~/AbilityKit.Demo.Moba.CodeGen/Contracts/` 下的共享契约。不要把成对业务的验证逻辑直接写进 Generator 或绕过 Contract。

## 新增配置表

1. 定义 DTO，并提供公开的 `int` key 成员。
2. 定义 MO，并保留可访问的 `MO(DTO)` 构造器。
3. 在 `MobaConfigTableDeclarations.cs` 增加一条 assembly `MobaConfigTable` 声明，指定路径、DTO、MO、组和稳定顺序。
4. 不要再修改 `MobaConfigRegistry`、`MobaConfigGroups` 或增量重载分支来注册同一张表。
5. 运行 CodeGen 契约测试和 MOBA 配置表测试。

配置表生成结果包含：

- 强类型 DTO table factory；
- 强类型 MO entry table factory；
- changed-ID collector；
- Registry 与配置组共享的表定义。

DTO 与 MO 字段不要求一一对应。类型变化、枚举解析、空集合规范化、聚合字段和其他业务转换继续写在 `MO(DTO)` 构造器中；Generator 只生成 `new MO(dto)`，不得推断字段映射。

`ConfigTableDefinition` 的 DTO/MO factory 必须成对存在或同时为空：

- 明确的单侧 `null` 由框架 Analyzer `AK1004` 在编译期拦截；
- 变量、反射和外部定义由构造器在运行时校验；
- changed-ID collector 可以独立为空，不参与 factory 配对判断。

增量重载以整个变更批次为事务：先构建并验证候选 DTO/MO 表，再统一提交。未知表、删除、反序列化或 `MO(DTO)` 转换失败时，不得修改当前数据库。

## 新增其他声明

1. 优先复制同业务现有 attribute/基类/接口组合，不要手写 Registry 条目。
2. 先满足 Analyzer 给出的类型可见性、非泛型、构造器、常量参数和唯一性要求。
3. 如果业务需要 DI，确认生成路径与反射 fallback 的构造策略是否不同；不要为消除 warning 随意添加无意义构造器。
4. 只有外部扩展场景才依赖反射/DI fallback，MOBA runtime 内建实现应能在禁用 fallback 时工作。

## 验证

```powershell
dotnet test src/AbilityKit.CodeGen.Tests/AbilityKit.CodeGen.Tests.csproj -c Release --no-restore
dotnet test src/AbilityKit.Demo.Moba.Tests/AbilityKit.Demo.Moba.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~MobaConfigTableManifestTests"
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate moba-codegen
```

需要继续 runtime 功能开发时再跑 P0：

```powershell
powershell -ExecutionPolicy Bypass -File tools/run_test_gate.ps1 -Gate precheck
```
