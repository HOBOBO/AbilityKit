# Pipeline 与 Triggering 首轮交付整理

本轮整理包的交付边界，不改变管线阶段、触发器调度、配置格式或 MOBA 机制，不调整版本或发布批次。

| 包 | 已完成 | 尚待处理 |
| --- | --- | --- |
| Pipeline | 将 `Pipeline Usage` 放入 `Samples~` 并登记为可选导入，保留原 `.meta`；按当前 API 修复示例，去掉同一时间反复推进的循环。明确 Unity 专属静态入口和 .NET 宿主边界，将 .NET 单测及依赖审计纳入 `core-stability`。 | `Pipeline.cs` 仍包含 Unity 场景加载钩子；不在本轮移动其公开类型或开启 `noEngineReferences`。 |
| Triggering | 声明直接使用的 Newtonsoft JSON UPM 依赖。使 .NET 的抽象类型和 Unity 一样只有一个定义程序集，旧 .NET 程序集名经类型转发解析；回归覆盖所有抽象类型。 | 按程序集枚举类型的调用方应扫描抽象程序集。既有 XML 注释债务、Unity 样例目录及历史兼容入口不在本轮迁移。 |

## 验证

- Release .NET 测试：Pipeline 20 项、Triggering 32 项、Ability 下游 2 项通过。Triggering 不再报告 `CS0436`；既有文档警告未放宽。两包的生产 asmdef 依赖审计及版本审计通过，未运行全量 `core-stability`。
- Unity `2022.3.62f1`：独立工程用本地 tarball 编译两包 Runtime 与 Editor。通过 Package Manager 导入 Pipeline 样例，重新编译并执行延迟与中断验证。本轮未运行 Player 或注册表验证。
- 独立验证需显式补齐 Core 使用的 `com.unity.modules.unitywebrequest` 和 World DI 使用的 `com.cysharp.memorypack`。首次仅按声明闭包创建嵌入工程时分别出现缺类型错误；嵌入包还触发了需要 NUnit 的测试装配。补齐夹具依赖后的 Editor 通过不代表发布依赖闭包已经闭合。先处理上游声明，再验证仅从目标包解析安装。

本机证据：`local/Logs/package-readiness/pipeline-triggering/`。
