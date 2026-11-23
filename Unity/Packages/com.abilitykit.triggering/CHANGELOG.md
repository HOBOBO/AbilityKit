# Changelog

## Unreleased

- 移除 Unity 运行时对 Analyzer 的包与程序集引用；`world.di` 显式声明由 OpenUPM 提供的 Unsafe，消费工程需配置对应 registry scope。
- 补齐扁平动作与业务规则调度入口的时钟、状态归属及失败边界；不改变调度行为。
- 移除无生产调用的内部 `Timed` 数据派发试点；通过公开调度接口保留宿主重绑定回归，不改调度时序或快照格式。
- 分离正式调度上下文与条件委托，将旧派发器归入兼容目录；保留公开 API、程序集和原脚本 GUID，不改调度行为。
- 分离计划记录存储与 JSON 解析转换，保留 `TriggerPlanJsonDatabase` 的公开入口及既有加载、重复 ID 和覆盖行为。
- 破坏性变更：移除未被仓库内消费的旧 `ExecutableRegistry`、`ExecutableDescriptor`、`ConditionDescriptor`。保留现有执行器、类型 ID 和正式计划注册链；设计取舍见[入口清单](Documentation~/FormalApiBoundary.md#executableregistry-下线记录)。
- 将 `TriggerPlanAggregateCompiler` 移入 `AbilityKit.Triggering.Authoring`，编辑器与 CLI 共用源码；调用方需更新程序集引用并重新编译。发布结果与宿主覆盖规则不变。
- 将测试夹具 `TriggeringTestHarness<TCtx>` 移入 `AbilityKit.Triggering.TestSupport`；命名空间和签名不变，测试项目须更新程序集引用并重新编译。运行时不再承载该夹具。
- 按订阅、派发、通知拆分运行器实现，保留原 `TriggerRunner<TCtx>` 公开入口、程序集与 Unity 脚本 GUID。
- 移除无调用的内部短路 Cue 转发与重复的 Cue 上下文转发；不改加载、排序、通知或调度约定。
- 补充包内源码目录导航及注册排序、释放与订阅隔离回归测试。
- 移除不编译的旧配置与实例测试，将注册表和计划条件的有效断言纳入 EditMode；无运行时变更。
- 将旧 JSON 校验包装器与选项归入 `Plans/Validation/Adapters`，保留原 API 和脚本 GUID；不改解析、合并或宿主启动行为。
- 将运行时诊断与根目录兼容清单从 `RuntimeServices` 归位至 `Runtime/Diagnostics` 与 `Runtime/Compatibility`，保留原公开 API 和脚本 GUID。
- 清理原 `Samples/` 历史源码，新增可单独导入的队列派发样例；保留正式主线和队列脚本 GUID，不改运行时 API。
