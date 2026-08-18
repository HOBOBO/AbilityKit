global using Xunit;

// com.abilitykit.protocol 在进程内保存大量可变静态状态：
//   - WireSerializer.Current / WireSerializer.TextSerializer（静态字段）
//   - ProtocolRegistry.Instance（单例：注册表 + 当前序列化器）
//   - ServerPushHandlerRegistry.Instance（单例注册表）
// xUnit 默认并行执行不同测试类，会把这些共享状态交叉污染。
// 关闭测试级并行化后，所有测试按顺序执行，各测试自行建立/恢复基线，保证确定性。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
