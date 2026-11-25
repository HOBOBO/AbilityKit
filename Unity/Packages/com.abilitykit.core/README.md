# AbilityKit Core

> 文档类型：包入口与使用导航
> 核校状态：2026-11-24 核对包声明、当前目录、支持分级和最短使用路径；专题契约按各页范围阅读。

<a id="定位"></a><a id="不负责什么"></a>

`com.abilitykit.core` 提供跨运行时的数学、日志、稳定集合、标识、计时、所有权、对象池及事件原语。具体玩法、世界装配、网络同步和表现策略由使用方负责。

## 环境与安装

从项目配置的包来源安装，版本、Unity 最低版本和直接依赖以 [package.json](package.json) 为准。当前声明依赖 `com.abilitykit.deterministic`；Core 不是零依赖包，也不要求安装任何 Demo。

纯运行时使用 `AbilityKit.Core` 程序集，编辑器适配使用独立的 `AbilityKit.Core.Editor`。自定义 asmdef 应引用所用类型的程序集；直接使用定点类型时还需引用 `AbilityKit.Deterministic`。正式安装渠道与平台支持应按项目约定验证，本地镜像工程不等于已发布 NuGet 渠道。

<a id="推荐接入"></a>

## 首次使用

从[首次使用指南](Documentation~/Usage.md)复制完整 `CoreQuickStart` 类，调用 `CoreQuickStart.Run()`。示例注册一个稳定名称、租用两个整数的缓冲区并执行单次释放回调，预期返回 `True:18:1`。

缓冲区所有者退出时归还数组；已取得的视图不得越过释放边界。名称注册表不能证明全局唯一，也不提供并发写保证。详细 API 契约从下方入口选择。

<a id="验收要求"></a>

## 支持范围与兼容

[首发支持清单](Documentation~/FirstReleaseSupport.md)区分推荐入口、持续交付的高级接口、历史兼容面和待准入项。默认先使用实例所有者与明确作用域，不用全局门面替代世界级生命周期。

- 数学、日志、稳定集合与标识、单调计时、租用数组所有权、实例对象池和类型化事件键按支持清单接入。
- Continuous、Config、Reflection、Markers 和 DebugDraw 保留相应兼容契约；新代码按所属领域的迁移入口接入。
- `Runtime/Observability` 的既有原语仍待职责准入复核，注释或测试通过不改变其支持分级。
- 浮点数学和定点桥接有量化、中间溢出及编译目标差异，不由 Core 单独承诺完整模拟跨平台逐位一致。

<a id="相关文档"></a>

## 文档入口

- [使用指南](Documentation~/Usage.md)：最短代码、资源所有权和排错。
- [能力与兼容文档索引](Documentation~/README.md)：按任务选择契约正文。
- [Core 边界](Documentation~/CoreBoundary.md)：职责、准入与兼容性要求。
- [消费范围说明](Documentation~/IndependentConsumption.md)：独立消费的必要条件与历史验收边界。
- [验证门禁与发布缺口](Documentation~/ValidationAndReleaseReadiness.md)：影响使用方的支持限制与候选状态。
- [CHANGELOG](CHANGELOG.md)：版本行为与迁移记录。

仓库的组合教程、回归命令和正式发布记录由仓库文档维护，包内本地链接均可在独立安装后解析。
