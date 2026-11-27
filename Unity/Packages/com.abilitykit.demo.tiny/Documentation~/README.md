# Tiny 使用文档

- [项目接入指南](IntegrationGuide.md)：依赖、服务端前提、正式项目入口、同步模式和排错。
- [`07 Project Entry`](../Samples~/07-ProjectEntry/README.md)：项目自己的大厅如何进入 Tiny 并返回。
- [`02 Room`](../Samples~/02-Room/README.md)、[`03 State`](../Samples~/03-State/README.md)、[`04 Frame`](../Samples~/04-Frame/README.md)、[`05 Hybrid`](../Samples~/05-Hybrid/README.md)、[`06 Recovery`](../Samples~/06-Recovery/README.md)：逐步了解局部 API，不能替代完整会话实现。

纯规则和 `01 Logic` 样例由依赖包 `com.abilitykit.demo.tiny.logic` 提供。Orleans Host、Gateway 和 Tiny 服务端玩法模块不随 Unity UPM 包分发。
