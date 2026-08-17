# Ability-Kit Record MemoryPack 输入记录编解码模块开发设计文档

> **阅读对象**：需要把 Lockstep 输入记录序列化为 MemoryPack 二进制格式的开发者。
>
> **文档目标**：说明该包作为 record 包的可选 MemoryPack codec 后端的边界。

---

## 一、设计理念

`record.memorypack` 是记录系统的可选序列化后端。它围绕 Lockstep input record 提供 MemoryPack codec 和安装器，让记录/回放系统可以按需切换二进制格式。

---

## 二、模块边界

负责：

- 提供 `FrameRecordMemoryPackCodec`。
- 提供 `FrameRecordMemoryPackCodecInstaller`。
- 引用 `AbilityKit.Record` 和 `MemoryPack`。

不负责：

- 不定义输入记录业务模型。
- 不负责文件写入和回放调度。
- 不负责 MemoryPack 运行库安装。

---

## 三、注意事项

- package.json 当前应与 asmdef 引用保持一致，按需组合时要确保 record 与 MemoryPack 都可用。
- 记录格式变更会影响旧回放文件读取。
- 安装器应在记录系统初始化前调用。

---

*文档版本：1.0*
*最后更新：2026-06-05*
