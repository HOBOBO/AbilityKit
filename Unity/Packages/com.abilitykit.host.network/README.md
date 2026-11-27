# Host Network

`com.abilitykit.host.network` 提供将服务端网络 session、认证身份与 HostRuntime 的连接索引衔接。

## 安装与首次使用

从项目配置的包来源安装；版本、Unity 最低版本和直接依赖以 [package.json](package.json) 为准，同时满足传递依赖与所需第三方程序集的条件。本轮核校基于仓库源码与 .NET 镜像，不等于隔离安装或 Unity 发布验收。

按使用指南装配已有 world factory、codec 与 handler，再启动监听。关闭时等待原 StopAsync，再切换生命周期，并单独关闭世界。

## 文档

- [文档索引](Documentation~/README.md)：阅读顺序、当前契约与验证边界。
- [使用指南](Documentation~/Usage.md)：接入前提、调用顺序、所有权与失败行为。

- [变更记录](CHANGELOG.md)。
