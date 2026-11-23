# 本地帧命令

通过 Unity Package Manager 导入 `Local Frame Commands`，由本地宿主调用 `LocalFrameCommandExample.Run()`。不需要场景、服务或传输；返回 `true` 代表已齐备的空帧推进、同帧命令按明确的比较器执行、有命令但未齐备的帧不推进，且主动裁剪保留下界帧。

其中的就绪集合由示例宿主维护，不是生产结算接收器。接入实际项目时先检查帧身份与完成条件，再固定步进；参见 [接入契约](../../Documentation~/README.md)。
