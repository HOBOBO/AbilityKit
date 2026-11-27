# Network Client

`com.abilitykit.network.client` 提供持有 Room 控制面和 Battle 数据面连接的组合会话。

## 安装与首次使用

从项目配置的包来源安装；版本、Unity 最低版本和直接依赖以 [package.json](package.json) 为准，同时满足传递依赖与所需第三方程序集的条件。本轮核校基于仓库源码与 .NET 镜像，不等于隔离安装或 Unity 发布验收。

按使用指南选择 EnterAsync 或分步 AttachBattle。业务提供 launch spec 和输入/下行 codec，战斗面独占对应推送订阅，Tick 使用真实墙钟间隔。

## 文档

- [文档索引](Documentation~/README.md)：阅读顺序、当前契约与验证边界。
- [使用指南](Documentation~/Usage.md)：接入前提、调用顺序、所有权与失败行为。
