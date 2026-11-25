<a id="core-基础设施文档索引"></a>

# Core 使用文档索引

> 文档类型：包使用导航
> 核校状态：2026-11-24 核对当前入口、源码目录与支持分级；正文的历史测试结果保留各自范围。

<a id="包定位"></a><a id="快速入门"></a><a id="想了解-core-是什么"></a><a id="想验证-foundation-starter"></a>

从[包入口](../README.md)安装，先运行[首次使用示例](Usage.md)。本索引组织消费者所需契约，仓库装配与维护操作由仓库文档负责。

<a id="文档列表"></a><a id="概念速查"></a><a id="core-子能力"></a><a id="starter-推荐-api"></a><a id="源码路径"></a>

## 按任务阅读

| 任务 | 权威正文 | 当前源码 |
| --- | --- | --- |
| <a id="1-core-边界与准入规则"></a>选择支持面、理解 Core 职责 | [首发支持清单](FirstReleaseSupport.md)、[边界与准入](CoreBoundary.md) | `Runtime/` |
| <a id="4-集合缓冲区与数组所有权契约"></a>管理集合、帧缓冲或租用数组 | [集合与缓冲区](CollectionsAndBuffers.md) | `Runtime/Collections/`、`Runtime/Buffers/` |
| <a id="5-对象池生命周期与配置契约"></a>创建对象池并管理作用域 | [对象池](Pooling.md) | `Runtime/Pooling/` |
| <a id="3-同步事件契约"></a>发布事件、退订和处理载荷 | [事件契约](Eventing.md) | `Runtime/Eventing/` |
| <a id="7-稳定标识与单调时间契约"></a>稳定名称标识与单调时间换算 | [标识与计时](IdentifiersAndTiming.md) | `Runtime/Identifiers/`、`Runtime/Timing/` |
| 持有单次释放句柄 | [首次使用与所有权](Usage.md)、[释放句柄源码](../Runtime/Lifetime/DisposableRegistration.cs) | `Runtime/Lifetime/` |
| <a id="6-数学原语契约"></a>向量、旋转、几何与退化输入 | [数学原语](Mathematics.md) | `Runtime/Math/` |
| <a id="8-定点数学桥接契约"></a>浮点与定点边界转换 | [定点桥接](DeterministicMathBridge.md) | `Runtime/Mathematics/` |
| <a id="13-观测关联原语契约与准入复核"></a>使用已有观测关联原语 | [观测契约与准入限制](Observability.md) | `Runtime/Observability/` |
| <a id="15-core-验证门禁与发布缺口"></a><a id="16-core-与-deterministic-独立消费验证"></a>确认消费环境和验收限制 | [独立消费范围](IndependentConsumption.md)、[发布缺口](ValidationAndReleaseReadiness.md) | 包声明与具体制品 |

<a id="旧-core-numericsnumbervalue-等已删除"></a>

数学原语和定点桥接的命名空间同为 `AbilityKit.Core.Mathematics`，物理目录分别为 `Math` 与 `Mathematics`。`Runtime/Generic`、`Runtime/Numerics` 和已退役的 `Runtime/Unity` 不再作为当前源码入口。

<a id="相关文档"></a>

## 兼容与迁移

| 既有入口 | 阅读正文 | 新接入方向 |
| --- | --- | --- |
| <a id="9-core-continuous-兼容与迁移契约"></a>Core Continuous | [生命周期与迁移差异](ContinuousCompatibility.md) | 独立 `com.abilitykit.continuous` |
| <a id="10-反射与释放工具兼容契约"></a>反射与 DisposeUtils | [反射和释放兼容契约](ReflectionAndDisposalCompatibility.md) | 所属领域的窄接口与资源所有者 |
| <a id="11-配置文件缓存与模块描述兼容契约"></a>设置、对象缓存与模块描述 | [配置兼容契约](ConfigurationFileCompatibility.md) | 项目或宿主配置策略 |
| <a id="2-marker-注册与扫描兼容契约"></a>Marker 登记与扫描 | [Marker 契约](Markers.md) | 所属所有者显式发现和注册；旧全局启动入口冻结 |
| <a id="12-旧-debugdraw-兼容与迁移契约"></a>DebugDraw | [绘制兼容契约](DebugDrawCompatibility.md) | `com.abilitykit.diagnostics` |
| <a id="14-旧-unity-设置引导退役说明"></a>已退役的 Unity 设置引导 | [退役说明](UnitySettingsBootstrapCompatibility.md) | 应用宿主接管 |
| 旧标签设计资料 | [历史标签设计](标签系统模块开发设计文档.md) | `com.abilitykit.gameplaytags`；不是 Core 当前 API |

兼容 API 是否仍交付、何时移除，以支持清单和变更记录为准。迁移不能只按名称替换，必须核对生命周期与失败行为。
