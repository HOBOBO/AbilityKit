# AbilityKit ThirdParty Luban Runtime

本包保存 Luban C# 二进制读取运行时及本仓库生成代码所需的兼容层。它不包含业务表、配置数据库、导表 CLI 或发布流程。

## 定位与环境

版本与 Unity 最低版本见 [package.json](package.json)，当前为 0.0.1 / Unity 2021.3；[程序集声明](Runtime/AbilityKit.ThirdParty.Luban.Runtime.asmdef) 启用 unsafe。生成代码、生成器和运行时必须按实际 API 与序列化契约匹配，包号本身不能证明任意上游 Luban 版本兼容。

本仓库按 embedded package 使用；仅复制此包不会得到 MOBA 业务 DTO 或生产表。第三方源码中的许可声明应保留，本批未变更它们。

## 首次使用与文档

- [包内导航](Documentation~/README.md)
- [ByteBuf、兼容层、所有权与故障定位](Documentation~/Usage.md)：含只用当前代码生成本地数据的完整最小示例。
- [现行核心源码](Runtime/LubanCore)、[兼容层](Runtime/LubanCompat)
- [历史包说明](Document/ThirdPartyLubanRuntime第三方配置运行时包说明.md)

## 核校与限制

2026-11-25 已核校缓冲区、命名空间、asmdef 和本地读写示例。尚未进行 Unity 2021.3、IL2CPP、独立安装或跨 Luban 版本兼容矩阵验收；不将本地 round-trip 扩大为任意数据或版本的兼容保证。业务数据必须由调用方受控来源提供，数据有效性及异常处理由上层负责。
