# 通用计算加速后端与 Unity Jobs 扩展框架

> **文档类型：Canonical 设计**  
> **事实基线：2026-09-11**  
> **适用范围：可批处理的纯数值计算、可选平台后端和业务迁移边界。**

## 一、结论

AbilityKit 不把 Unity Jobs 设为 motion、dataflow 或其他业务包的默认实现。框架拆成两层：

| 包 | 运行时边界 | 职责 |
|---|---|---|
| `com.abilitykit.compute` | 纯 C#、无 Unity 依赖 | kernel 契约、显式后端链、接管结果、输出校验、观察接口、托管串行与并行后端 |
| `com.abilitykit.compute.unityjobs` | Unity 可选包 | Persistent Native buffer owner、可复用 Job DAG，以及具体业务 Job 的扩展边界 |

业务包继续拥有语义基线。应用组合根显式安装候选后端；后端不可用、低于收益阈值、执行失败或输出无效时，调用方采用原业务实现。安装包本身不会覆盖任何注册，也不会改变默认行为。

## 二、为什么不抽象成“任意委托丢进 Job”

Unity Job/Burst 的约束发生在编译期：数据必须适合 Native 容器，执行代码不能捕获托管对象，Burst 还需要发现具体 Job 类型。运行时传入 `Func`、`IDataflowProcessor` 或 `IMotionSource` 无法成为可靠的 Burst kernel。

Burst 1.8.21 明确不支持通过泛型方法调度泛型 Job 的 Player AOT 发现。因此框架区分两条路径：

框架不提供泛型 `IJobParallelFor` 数组桥，也不通过 delegate 隐藏业务 Job 的调度。Unity Collections 的 reflection-data codegen 和 Burst AOT 都无法可靠发现通过泛型方法或委托间接调度的 Job；这种实现可能在 Editor 工作、到 Player 才失败或失去 Burst。需要 Jobs/Burst 的业务扩展必须定义具体 `[BurstCompile] struct`，用 `UnityJobPlan` 组织依赖，并把具体 `.Schedule(...)` 调用点保留在业务程序集，让中间结果持续留在 Native 内存。

这避免 Editor 中看似 Burst、Player 中退回托管代码的假加速。

## 三、计算模型

### 3.1 可移植 kernel

`IComputeKernel<TInput,TOutput>` 表示相互独立的逐元素计算。kernel、输入和输出都受 `unmanaged` 约束；kernel 不应依赖元素执行顺序或共享可变状态。

```csharp
public struct DamageKernel : IComputeKernel<DamageInput, DamageOutput>
{
    public DamageOutput Execute(in DamageInput input)
    {
        return new DamageOutput(/* pure calculation */);
    }
}
```

同一个 kernel 可由 `ManagedSequentialComputeBackend`、显式 `ManagedParallelComputeBackend` 或未来的 SIMD/服务器专用后端执行。Unity 业务 Job 应调用同一纯函数/数学原语，但使用具体 Job 类型以满足 codegen/AOT 约束。这里共享的是计算语义，不是平台调度器。

### 3.2 后端链

`ComputeBackendChain` 按构造顺序尝试后端。每次尝试有五种结果：

| 结果 | 含义 | 后续行为 |
|---|---|---|
| `Succeeded` | 已填充候选输出 | 执行业务校验，校验通过才接收 |
| `Unavailable` | 当前平台或生命周期不可用 | 尝试下一个后端 |
| `Declined` | 批量过小或不适合该实现 | 尝试下一个后端 |
| `InvalidOutput` | 业务校验拒绝或校验器异常 | 尝试下一个后端 |
| `Faulted` | 后端抛出或报告故障 | 记录后尝试下一个后端 |

后端链为空时返回失败，不会发明一个全局默认后端。`ManagedSequentialComputeBackend` 也必须显式加入；业务若要保证原实现作为最终语义基线，应在链失败后直接调用原实现，而不是依赖平台包替它决定。

### 3.3 输出验证

`IComputeResultValidator<TInput,TOutput>` 属于业务层。它用于检查有限值、范围、稳定排序、守恒关系或与权威输入的对应关系。框架不会假定“返回 true 就可信”。验证器异常被记作 `InvalidOutput`，不会阻断后端回退。

`IComputeObserver` 接收 operation id、批量大小、后端 id 和结果。生产观察至少应按 operation/backend/outcome 统计次数，并记录耗时、拷贝字节和同步等待；observer 自身异常不会改变业务结果。

## 四、Unity 两条执行路径

### 4.1 原生 Job DAG

`UnityJobPlanBuilder` 在初始化期建立 DAG，dependency 只能指向已添加节点，因此图天然无环。Build 后节点和依赖数组冻结；执行时复用 JobHandle 和节点状态数组，不为每帧重建图。

```text
main-thread gather
       |
       v
 [build input] --> [stage A] --+
                         [stage B] --> [validate/apply]
       +------------> [stage C] --+
```

业务先调用 `BeginSchedule`，从每个节点取得 `GetDependency`，直接调度具体 Job 后用 `Record` 记录 handle，最后由 `EndSchedule` 返回最终组合 handle。框架不强制立即完成，调用方可以与其他系统重叠；一个 plan 同时只允许一次 in-flight 执行。`UnityComputeBuffer<T>` 负责 Persistent NativeArray 的显式所有权；调整容量或释放前必须完成所有引用它的 Job。

业务扩展必须在业务方法中实例化具体 Job struct，并直接调用对应的 `.Schedule(...)`；plan 只组合 dependency/handle，不持有业务 schedule delegate。Job 上的 `[BurstCompile]`、float mode、batch count、容器访问属性和写入范围仍由业务决定，因为这些都是算法约束，而不是通用调度策略。托管数组与 Native 数据之间的 gather/copy/apply 也由业务 adapter 管理，这样连续阶段可以消除中间复制，而不是被通用桥强制逐阶段回传。

## 五、业务迁移方式

### 5.1 Dataflow / 伤害计算

现有 `DataflowPipeline<TInput,TOutput>` 是单请求、按顺序执行的托管对象链。processor 可以读写 `IDataflowContext`、中断管线、抛异常，并把输出回灌为下一个输入。这些语义不能透明并行。

正确迁移单位是“一批彼此独立的伤害请求”，而不是单个 processor：

1. 把本帧请求稳定排序并拍平成 unmanaged `DamageInput[]`；
2. 把纯公式拆为 kernel 或具体 Burst Job；
3. buff 查询、标签判断、随机数消费等先在主线程固化为输入字段；
4. 并行计算候选 `DamageOutput[]`；
5. 校验有限值、上下界、版本和输入索引；
6. 在主线程按稳定顺序应用生命值、触发事件和修改 context；
7. 任一步拒绝或失败时，对整批运行现有 managed pipeline，禁止半批混用。

因此 dataflow 包本身不依赖 Unity Jobs。项目可建立 `*.dataflow.compute` 或 `*.dataflow.unityjobs` 适配包，并在组合根选择后端。

### 5.2 Motion

现有 `MotionPipeline.Tick` 同时执行 source 生命周期、分组压制、碰撞策略选择、碰撞世界查询、状态写回和事件通知。`IMotionSource`/`IMotionSolver` 是托管多态对象，不能整体进入 Job。

迁移应拆为：

1. **Gather**：主线程 tick source、确定有效贡献和碰撞策略，写入稳定 SoA/unmanaged 输入；
2. **Pure batch**：并行做轨迹采样、向量合成、速度限制以及可并行的无状态几何计算；
3. **World solve**：若碰撞后端有原生批查询能力，用 DAG 接续；否则保留 managed solver；
4. **Validate**：拒绝 NaN/Infinity、越界位移和身份错配；
5. **Apply**：主线程按实体稳定顺序更新 `MotionState` 并派发 hit/finish 事件；
6. **Fallback**：整批回到 `MotionPipeline.Tick` 语义基线。

不应先改 `MotionPipeline.Tick` 的默认行为。只有业务宿主能够自然形成足够大的批次，并证明拷贝与同步成本可回收时，才显式选择加速路径。

### 5.3 Shooter 首个接入

`ShooterUnityJobsRvoNeighborAccelerationService` 仍是显式 World module，默认 Shooter 世界不会安装它。本次只把“构建空间网格 → 收集邻居”两段具体 Burst Job 接入 `UnityJobPlan` 依赖管理：算法、阈值、托管/Native 拷贝、同步完成、结果校验和 managed 回退均不改变；具体 `.Schedule(...)` 仍在 Shooter 程序集中直接调用。

Shooter 的历史基准表明 2,048 Agent 下 Jobs hashmap 邻居收集慢于共享 managed parallel 收集，原因正是复制、预扫描和同步点。因此该接入证明扩展机制，不构成默认启用或性能收益证据。

## 六、确定性与故障规则

- float mode 不属于通用 options：任意 kernel 的浮点编译方式无法由运行时后端可靠改写。具体 Burst Job 必须显式固定 `FloatMode`/`FloatPrecision`，并把它纳入算法版本和验收矩阵。
- 网络权威、回滚和 hash 场景应固定算法版本、稳定输入顺序、tie-break、float mode 和工具链版本。
- 不允许后端部分提交业务状态。输出先写候选缓冲，校验完成后一次性 apply。
- fallback 粒度默认是整批；逐元素 fallback 会改变随机消费、事件顺序和聚合舍入。
- 后端异常不能穿透战斗 tick；参数错误属于调用方编程错误，可以在调度前直接抛出。
- Persistent buffer 必须由 World/service 生命周期持有并 Dispose，不由临时 Job 节点拥有。

## 七、性能准入

每个业务 operation 独立测量至少以下数据：

| 指标 | 用途 |
|---|---|
| 32/64/128/512/2048 批量耗时 | 找到接管交叉点 |
| gather / copy-in / schedule / wait / copy-out / validate / apply | 定位收益被哪段抵消 |
| 预热后 managed 与 native 分配 | 验证缓存是否真正复用 |
| 稀疏、密集、极端输入 | 防止只对一种分布调参 |
| Editor Mono、目标 Player、IL2CPP/Burst | 防止用 Editor 结果外推 Player |
| accelerated 与 baseline 输出/hash | 证明后端切换不改变语义 |

未形成目标平台 artifact 前，默认阈值只可视为保守配置，不是性能承诺。

## 八、当前证据和边界

| 等级 | 当前证据 | 能说明什么 |
|---|---|---|
| E0 | 两个 compute 包、核心契约、Unity adapter、Shooter DAG 接线 | 架构和源码存在 |
| E2 | `AbilityKit.Compute` net10 构建；netstandard2.1 对 Unity 2022 Burst/Collections 程序集绑定通过 | 纯 C# 与 Unity API 形状可编译 |
| E3 | Compute .NET 测试 6 项 | 显式选择、拒绝/异常/无效输出回退、无隐式后端、托管并行一致 kernel |
| E3 | Unity Job/Buffer Editor 测试 4/4 通过 | 验证 fan-out/fan-in 依赖、部分调度清理、调度生命周期和 Persistent buffer 所有权 |
| E4 | 无 motion/dataflow 目标场景性能 artifact | 不能声称这两类业务已获得性能收益 |

当前框架没有自动分析 managed object graph、自动生成 SoA、GPU backend、跨运行时通用异步 handle 或自动阈值调优。它提供的是平台可替换、失败可回退、生命周期明确的执行骨架；业务仍必须完成数据布局、语义校验和真实性能验收。

---

*文档版本：v1.0 | 最后更新：2026-09-11*
