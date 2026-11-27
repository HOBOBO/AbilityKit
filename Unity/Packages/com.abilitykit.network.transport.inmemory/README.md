# InMemory Transport

`com.abilitykit.network.transport.inmemory` 提供同步投递字节的进程内成对 ITransport，适合本地测试。

## 安装与首次使用

从项目配置的包来源安装；版本、Unity 最低版本和直接依赖以 [package.json](package.json) 为准，同时满足传递依赖与所需第三方程序集的条件。本轮核校基于仓库源码与 .NET 镜像，不等于隔离安装或 Unity 发布验收。

先运行使用指南的完整字节往返例子。SDK/ConnectionManager 仍要求有效端口，可用 1；原始 transport 不验证端点、不模拟真实断网。

## 文档

- [文档索引](Documentation~/README.md)：阅读顺序、当前契约与验证边界。
- [使用指南](Documentation~/Usage.md)：接入前提、调用顺序、所有权与失败行为。
