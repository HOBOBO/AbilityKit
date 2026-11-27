# Network Host

`com.abilitykit.network.host` 提供通过 listener、channel、服务端 session 和显式 router 接收请求。

## 安装与首次使用

从项目配置的包来源安装；版本、Unity 最低版本和直接依赖以 [package.json](package.json) 为准，同时满足传递依赖与所需第三方程序集的条件。本轮核校基于仓库源码与 .NET 镜像，不等于隔离安装或 Unity 发布验收。

先运行使用指南的 InProcess 请求往返完整示例，无需套接字。NetworkHost 持有 listener 和接受的 session；世界时钟与认证策略由应用提供。

## 文档

- [文档索引](Documentation~/README.md)：阅读顺序、当前契约与验证边界。
- [使用指南](Documentation~/Usage.md)：接入前提、调用顺序、所有权与失败行为。
