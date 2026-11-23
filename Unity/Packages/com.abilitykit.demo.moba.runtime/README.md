# AbilityKit MOBA Runtime

`com.abilitykit.demo.moba.runtime` 是 MOBA 示例的逻辑运行时，提供世界装配、对局、技能、Buff 和触发执行，不是可直接启动的 Unity 场景，也不提供表现层。

从项目配置的包来源安装本包，Unity 版本和依赖以 [`package.json`](package.json) 为准。接入已有 MOBA 示例宿主时，从[启动链路](Runtime/Docs/StartupChainGuide.md)定位世界蓝图和 Bootstrap 模块；仅安装本包不会创建或推进世界。

- [随包使用与源码导航](Documentation~/Usage.md)：装配顺序和运行时代码归属。
- [早期专题索引](Document/README.md)：保留历史入口；跨包架构与仓库测试从仓库的 `Docs/design/00-index.md` 查阅。
