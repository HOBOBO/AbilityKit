# KCP Transport

`com.abilitykit.network.transport.kcp` 提供承载 AbilityKit 成帧字节的可选 KCP 可靠 UDP 客户端。

## 安装与首次使用

从项目配置的包来源安装；版本、Unity 最低版本和直接依赖以 [package.json](package.json) 为准，同时满足传递依赖与所需第三方程序集的条件。本轮核校基于仓库源码与 .NET 镜像，不等于隔离安装或 Unity 发布验收。

按使用指南配置 ConversationId 和匹配的 KCP 服务端。每次工厂调用创建新 transport；Connected 表示本地 UDP/KCP 初始化，不证明远端已接受。

## 文档

- [文档索引](Documentation~/README.md)：阅读顺序、当前契约与验证边界。
- [使用指南](Documentation~/Usage.md)：接入前提、调用顺序、所有权与失败行为。
