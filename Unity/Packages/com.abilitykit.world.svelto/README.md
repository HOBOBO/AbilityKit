# World Svelto

`com.abilitykit.world.svelto` 提供向 World DI 注册 Svelto 上下文及其非拥有服务别名。

## 安装与首次使用

从项目配置的包来源安装；版本、Unity 最低版本和直接依赖以 [package.json](package.json) 为准，同时满足传递依赖与所需第三方程序集的条件。本轮核校基于仓库源码与 .NET 镜像，不等于隔离安装或 Unity 发布验收。

先运行使用指南的独立上下文完整示例，再由宿主明确提交实体和推进引擎。此包没有独立 IWorld 实现或自动世界 Tick。

## 文档

- [文档索引](Documentation~/README.md)：阅读顺序、当前契约与验证边界。
- [使用指南](Documentation~/Usage.md)：接入前提、调用顺序、所有权与失败行为。
