# Tiny 样例目录约定

本包发布 `02-Room` 到 `06-Recovery` 五个逐章导入样例；`01-Logic` 位于独立的 `com.abilitykit.demo.tiny.logic` 包。07 使用本包的正式场景、装配资源和项目侧启动入口。

样例不复制 `Runtime` 的规则、协议 DTO、网络连接或会话状态机，只调用公开 API。02–05 的源码分别由独立 `RoomSample`、`StateSample`、`FrameSample`、`HybridSample` 工程运行，后两者复用正式会话的 .NET 验收驱动；06 的本地历史耗尽样例由 `ChapterSamples` 运行，完整故障由 `RecoverySample` 验收。07 的无 Starter 消费模板位于仓库的 `tools/tiny-consumer-template`，由生成脚本组合 Tiny 包闭包。导入样例不改变包级依赖；目前只有 01 可以仅安装 Logic 包。
