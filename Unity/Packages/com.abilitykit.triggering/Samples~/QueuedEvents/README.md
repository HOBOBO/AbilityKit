# Triggering Queued Events

导入后调用 `AbilityKit.Triggering.Runtime.Example.QueuedEventBusExample.RunOnce()`。不需要场景、资源文件或其他样例。

示例注册三个计划，然后发布一个整数事件。`Publish` 在队列模式下不立即派发；宿主调用 `Flush` 时，先执行阶段 0 的高优先级计划，再执行阶段 0 的低优先级计划，最后执行阶段 1 的计划。示例逐步检查结果，偏离预期时抛出异常。
