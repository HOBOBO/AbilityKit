# Ability-Kit Record MemoryPack 输入记录编解码模块开发设计文档

> **阅读对象**：需要把 Lockstep 输入记录序列化为 MemoryPack 二进制格式的开发者。
>
> **文档目标**：说明该包作为 record 包的可选 MemoryPack codec 后端的边界。
>
> **发布边界**：本包暂不纳入默认录制格式首发候选。保留现有显式接入入口，未验证独立 UPM 安装或 Unity Player。

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
- 不负责决定录制文件的保存时机、存档策略和回放调度；codec 本身读写其专有格式文件。
- 不负责 MemoryPack 运行库安装。

---

## 三、注意事项

- 独立发布前需补齐 `package.json` 直接依赖、核对 `asmdef` 中的 `AbilityKit.Protocol.MemoryPack` 引用归属，并验证序列化器/格式化器可用及 Unity Player 往返。
- 记录格式变更会影响旧回放文件读取。
- 安装器应在记录系统初始化前调用；它替换进程级 `FrameRecordCodecs.Current`，不保留默认按扩展名分派，也不转换既有录制文件。

---

*文档版本：1.0*
*最后更新：2026-06-05*
