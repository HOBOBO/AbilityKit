# AbilityKit Dataflow

同步、可组合的数据处理器链，用于数值计算、输入校验与规则处理。每条管线固定一组 `TInput/TOutput`，处理器依次执行，通过 Context 显式共享跨阶段状态；不提供 Tick、异步调度或事务回滚。

## 环境与安装

版本和最低 Unity 版本以 [package.json](package.json) 为准。本包没有其他 UPM 包依赖，运行时程序集不引用 Unity 引擎。宿主可从其 UPM 来源安装本包，本地可用 Package Manager 的 **Add package from disk** 选择 package.json。自定义程序集与命名空间均为 `AbilityKit.Dataflow`。

本轮核对仓库同源 .NET 10 镜像，不代表独立 UPM 安装已验证。

## 首次使用

运行[完整计算示例](Documentation~/Usage.md#2-完整示例同形处理与中断)，预期成功结果 25、校验中断后 Reset 可复用，最后输出 `dataflow execution contracts passed`。一笔执行独占一个 Context；复用时由调用者重置。配置完成后再共享管线，Processor 实例是否可并发由其实现决定。

## 文档与限制

- [包内导航](Documentation~/README.md)与[使用指南](Documentation~/Usage.md)：阶段输入、结果、失败、槽位和所有权。
- [局部设计分析](Document/Dataflow数据流处理模块开发设计文档.md)：执行实现与设计依据，保留原有证据日期。
- [Runtime 源码](Runtime/Core/)与[程序集声明](Runtime/com.abilitykit.dataflow.asmdef)。

异形管线不会自动把上阶段输出转换成下阶段输入；只有输出运行时兼容 TInput 才回灌。Execute 会创建处理器数组快照，槽位用 object 保存值；没有“零分配”承诺。
