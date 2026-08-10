# Ability TestKit 能力测试工具模块开发设计文档

## 一、文档定位

本文是 `com.abilitykit.ability.testkit` 的 package canonical 文档，记录当前 Editor 测试辅助器的职责、资源所有权、采用证据和已知限制。

本包面向 Unity Test Framework 下的 Ability/Triggering 局部测试。它降低创建逻辑世界、推进 Trigger Action 和提供内存配置的样板成本，但不是生产运行时、端到端战斗环境或跨进程测试框架。

## 二、模块边界

本包当前提供：

- `TriggerWorldTestHarness`：创建带 `TriggeringWorldModule` 的 `EntitasWorld`；
- `TestWaitTriggerActionFactory`：注册 `test_wait` 测试动作；
- `InMemoryTextLoader`：从内存字典读取非空文本配置。

本包不负责：

- 创建完整 MOBA 战斗、表现层、网络或持久化环境；
- 自动发现和加载业务配置资产；
- 模拟 Unity 帧循环、协程、物理或异步 IO；
- 提供测试隔离进程、日志归档、覆盖率或 CI 门禁；
- 替测试代码释放 World 和其他外部资源。

包代码位于 Editor 目录，不应被运行时程序集依赖或打入 Player。

## 三、Trigger World Harness

### 3.1 创建流程

`TriggerWorldTestHarness` 构造时执行：

1. 通过 `WorldServiceContainerFactory.CreateDefaultOnly()` 创建默认服务构建器；
2. 使用调用方给定的 `WorldId` 和 world type 构造 `WorldCreateOptions`；
3. 向 Options 添加 `TriggeringWorldModule`；
4. 创建 `EntitasWorld`；
5. 调用 World 初始化；
6. 暴露 `TriggerRunner` 和 `ITriggerActionRunner` 供测试驱动。

Harness 只注入 Triggering 模块。测试需要其他 World Module 或自定义服务时，当前 API 没有扩展参数，应另建专用 Fixture 或扩展 Harness，而不是假定默认世界等同于生产世界。

### 3.2 推进和时间

`Tick(deltaTime)` 直接推进所持有的 World。测试代码负责：

- 选择确定性的 `deltaTime`；
- 明确推进次数和完成条件；
- 对零、负值或大步长建立自己的输入约束；
- 不依赖 Unity Editor 实际帧率。

Harness 没有超时、自动等待或断言机制。测试应设置有限循环，避免动作永不完成时挂起。

### 3.3 所有权和释放

Harness 创建并独占 `EntitasWorld`，因此调用方必须释放 Harness，推荐使用 `using`。`Dispose()` 直接释放 World。

当前实现没有显式的重复 Dispose 保护，也没有在构造中途失败时回滚已创建资源的保护逻辑。测试不应重复释放同一实例；若 World 初始化可能抛异常，Fixture 需要在更外层处理清理和诊断。

## 四、测试动作与内存加载器

### 4.1 test_wait

`TestWaitTriggerActionFactory` 通过 `TriggerActionType` 注册 `test_wait`。Factory 从 Action 参数 `duration` 构造等待动作：

- 支持 `float`、`int` 和可由 `Convert.ToSingle()` 转换的值；
- 参数缺失时按实现默认值执行；
- 零或负时长在首次状态检查时可立即完成；
- `Tick(deltaTime)` 累加时间；
- `Cancel()` 只设置取消标记；
- 运行对象的 `Dispose()` 为空，不拥有外部资源。

该动作适合验证 Trigger Runner 的持续动作调度，不应作为生产等待、真实时间或异步取消语义的替代实现。

### 4.2 InMemoryTextLoader

`InMemoryTextLoader` 持有调用方提供的字典，并按 id 返回文本。当前加载成功要求：

- id 非空；
- 字典存在对应键；
- 文本非空。

因此空字符串配置会被视为加载失败。字典的变更和线程安全仍由调用方负责；Loader 不复制数据、不解析格式，也不提供文件回退。

## 五、推荐测试模式

```csharp
using var harness = new TriggerWorldTestHarness(
    new WorldId("test_world"),
    "test");

// Arrange: 注册定义或准备 Trigger 上下文。
// Act: 启动动作，并用固定 deltaTime 有界推进。
// Assert: 检查动作状态和业务输出。
for (var i = 0; i < 10; i++)
{
    harness.Tick(0.1f);
}
```

示例只展示生命周期模式；实际测试必须添加业务 Arrange 和 Assert。不要以固定 Tick 次数代替对完成条件和超时的明确断言。

## 六、真实采用证据

Moba View Runtime 的 `TriggerRunnerSmokeTests` 已使用 `TriggerWorldTestHarness` 创建测试世界并驱动 Trigger Runner。这证明 TestKit 存在 Unity 测试消费者，并为 Harness 主路径提供 E3 局部证据。

该消费者不能证明：

- TestKit 所有错误输入和释放路径已覆盖；
- 完整战斗、表现层或网络链已集成；
- `test_wait` 与 `InMemoryTextLoader` 的全部边界有测试；
- CI 会阻断 TestKit 回归。

## 七、失败边界

| 场景 | 当前行为或风险 | 测试责任 |
|---|---|---|
| World 初始化失败 | 构造过程可能抛异常 | Fixture 保留诊断并清理外部资源 |
| 重复 Dispose | 没有显式幂等保护 | 单一所有者、只释放一次 |
| 动作永不完成 | Harness 没有超时 | 使用有限 Tick 循环和超时断言 |
| 非法 duration | 转换可能抛异常 | 测试配置生成前先验证参数 |
| 零或负 duration | 动作可立即完成 | 明确是否为预期测试语义 |
| 空文本 | Loader 返回失败 | 不用空字符串表示有效配置 |
| 字典被并发修改 | Loader 不提供同步 | 测试自行隔离数据 |
| 需要完整业务模块 | 默认 Harness 只添加 Triggering | 使用专用 Fixture 组合模块 |

## 八、证据成熟度

| 等级 | 状态 | 说明 |
|---|---|---|
| E0 | 已具备 | Harness、等待动作和内存 Loader 源码存在 |
| E1 | 已具备 | Editor 测试 API 可直接调用 |
| E2 | 不适用/未确认 | 本包不是生产运行时组件 |
| E3 | 局部具备 | Moba `TriggerRunnerSmokeTests` 使用 Harness |
| E4 | 未确认 | 未找到独立 Smoke artifact 或 Acceptance 归档 |
| E5 | 未确认 | 未确认 CI 阻断、覆盖率预算和发布责任 |

## 九、源码与消费者入口

- [TriggerWorldTestHarness.cs](../Editor/UnitTest/TriggerWorldTestHarness.cs)：World 创建、Tick 和释放；
- [TestWaitTriggerActionFactory.cs](../Editor/UnitTest/TestWaitTriggerActionFactory.cs)：测试持续动作；
- [InMemoryTextLoader.cs](../Editor/UnitTest/InMemoryTextLoader.cs)：内存文本加载；
- [TriggerRunnerSmokeTests.cs](../../com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/UnitTest/TriggerRunnerSmokeTests.cs)：真实 Unity 测试消费者。

## 十、后续治理顺序

1. 增加构造失败、重复 Dispose、零/负 Tick 和动作超时测试；
2. 覆盖 `test_wait` 的参数类型、取消和完成边界；
3. 覆盖 Loader 的空 id、缺失键、空文本和字典变更；
4. 为需要多模块 World 的测试提供显式 Options 或 Fixture 扩展点；
5. 将稳定测试接入可追踪的 CI 作业并归档结果后，再升级 E4-E5。
