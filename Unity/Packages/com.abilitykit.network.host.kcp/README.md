# Network Host KCP

`com.abilitykit.network.host.kcp` 提供NetworkHost 的可选 KCP UDP listener 与服务端 channel。

## 安装与首次使用

从项目配置的包来源安装；版本、Unity 最低版本和直接依赖以 [package.json](package.json) 为准，同时满足传递依赖与所需第三方程序集的条件。本轮核校基于仓库源码与 .NET 镜像，不等于隔离安装或 Unity 发布验收。

使用指南给出仅绑定本机的监听示例。客户端与服务端必须配置相同 ConversationId，收到有效数据报才会创建 channel；该 ID 不代表认证。

## 文档

- [文档索引](Documentation~/README.md)：阅读顺序、当前契约与验证边界。
- [使用指南](Documentation~/Usage.md)：接入前提、调用顺序、所有权与失败行为。
