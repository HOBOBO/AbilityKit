# Ability 包使用文档

> 文档类型：包内导航
> 事实基线：2026-11-25；导航已核对，正文可信范围分别声明。

| 目标 | 阅读入口 | 核校范围 |
| --- | --- | --- |
| 安装和判断适用范围 | [包入口](../README.md)、[包声明](../package.json) | 当前声明与本批入口 |
| 登记配置、加载与查询 | [ConfigDatabase 使用指南](ConfigDatabase.md) | 通用数据库、factory-first、输入选择与失败语义 |
| 热重载、缓存和通知 | [重载契约](ConfigDatabase.md#4-重载身份与失败契约) | 表/条目身份、同步事件、版本和串行化责任 |
| 效果来源与 Cue | [效果溯源](../Document/效果溯源（EffectSource）模块开发设计文档.md)、[Cue](../Document/效果表现层（Cue）模块开发设计文档.md) | 保留既有正文；非本批正文核校范围 |

本页不定义项目表目录、MOBA CodeGen 或 Luban 发布顺序。这些跨包问题由仓库的 `Docs/design/05-CommonModules/04-ConfigurationSystem.md` 和 `07-CodeGenAndLubanProductionPipeline.md` 维护；包独立安装时不依赖这些仓库路径。
