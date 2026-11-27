# Luban Runtime 使用文档

> 文档类型：包内导航
> 事实基线：2026-11-25。

| 目标 | 入口 |
| --- | --- |
| 环境和能力边界 | [包入口](../README.md)、[包声明](../package.json) |
| 本地读取、缓冲区和失败契约 | [使用指南](Usage.md) |
| 核心读写 API | [Luban.Serialization.ByteBuf](../Runtime/LubanCore/Serialization/ByteBuf.cs) |
| 生成代码所用精简 API | [Luban.ByteBuf](../Runtime/LubanCompat/ByteBuf.cs) |
| 了解旧说明的有效范围 | [历史包说明](../Document/ThirdPartyLubanRuntime第三方配置运行时包说明.md) |

项目表生成与发布在包外，由仓库 `LubanConfig/Moba/README.md` 维护操作，`Docs/design/05-CommonModules/07-CodeGenAndLubanProductionPipeline.md` 维护所有权交接。本包不要求这些仓库路径在独立安装后存在。
