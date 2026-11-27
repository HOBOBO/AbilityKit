# LiteNet Transport

`com.abilitykit.network.transport.litenet` 提供基于 LiteNetLib ReliableOrdered 的可选 UDP 客户端。

## 安装与首次使用

从项目配置的包来源安装；版本、Unity 最低版本和直接依赖以 [package.json](package.json) 为准，同时满足传递依赖与所需第三方程序集的条件。本轮核校基于仓库源码与 .NET 镜像，不等于隔离安装或 Unity 发布验收。

按使用指南配置同一 connection key、帧协议与服务端。Unity 随包已有 Runtime/Plugins/LiteNetLib.dll；没有 AbilityKit LiteNet 服务端网关的完整接入保证。

## 文档

- [文档索引](Documentation~/README.md)：阅读顺序、当前契约与验证边界。
- [使用指南](Documentation~/Usage.md)：接入前提、调用顺序、所有权与失败行为。
