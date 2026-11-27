# Room Protocol 包文档

- [包入口](../README.md)：定位、安装前提和首次验证。
- [使用指南](Usage.md)：当前编解码、消息身份、缓冲区所有权及迁移说明。

Room 类型位于 `AbilityKit.Protocol.Room`；时间同步类型位于 `AbilityKit.Protocol.GatewayTimeSync`。先完成本地字节往返，再在所属连接会话中装配认证、目录和业务处理。事实基线：2026-11-25；核校与平台限制见使用指南。
