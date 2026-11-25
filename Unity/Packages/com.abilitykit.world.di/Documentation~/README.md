# World DI 使用文档索引

> 文档类型：包使用导航
> 核校状态：2026-11-24 核对当前注册、作用域、所有权和失败契约；Unity 独立安装未在本轮重跑。

| 阅读目标 | 入口 |
| --- | --- |
| 安装与最短接入 | [包 README](../README.md) |
| 生命周期、解析失败与初始化补偿 | [使用契约与限制](Usage.md) |
| 基础注册、构造注入、模块组合、可选依赖 | [示例索引](../Examples/README.md) |
| 版本行为变化 | [CHANGELOG](../CHANGELOG.md) |
| 早期背景与旧链接 | [历史入口](../Document/README.md)，其中旧代码不作为当前 API 参考 |

业务类依赖 `IWorldResolver`、`IWorldModule`、`IWorldInitializable` 等包内接口；宿主决定世界、根容器和作用域如何关联。跨包 Host/ECS 装配和仓库验收由仓库设计维护。
