# AbilityKit Combat Targeting

通过候选提供者、过滤规则、有序评分项和选择器组合目标查询，返回中性 EntityId 或调用方映射结果。实体索引、阵营/存活规则和业务对象由宿主提供。

## 环境与安装

版本 0.1.0、Unity 2022.3、直接依赖 `com.abilitykit.core` 见 [package.json](package.json)。宿主从其 UPM 来源安装相容包；本地可用 Package Manager 的 **Add package from disk** 选择 package.json。自定义程序集引用 `AbilityKit.Combat.Targeting`；主命名空间是 `AbilityKit.Battle.SearchTarget`，内置组件另有 Rules / Scorers / Selectors 子命名空间。

本轮只验证同源 .NET 10 镜像，未做独立 UPM 安装与 Unity Player 验收。

## 首次使用与文档

运行[完整圆形范围示例](Documentation~/Usage.md#2-完整示例圆内最近两个目标)，预期从四个 ID 中排除自己和范围外目标，返回 `2,3`，最后输出 `targeting query contracts passed`。调用方负责 Context / Result 租约，在释放前同步消费池化结果。

- [文档导航](Documentation~/README.md)、[使用指南](Documentation~/Usage.md)：安装后第一条查询和失败处理。
- [运行时手册](Documentation~/Manual.md)：排序、去重、对象池、目录和性能契约。
- [组合示例](Documentation~/Examples.md)：基于现有公开 API 的组合方式；示例策略不等于内置组件。
- [历史设计](Documentation~/Design/目标查找模块开发设计文档.md)：早期目标，不能作为 API 清单。

引擎不会扫描世界或校验实体代次；无 Provider 得到空结果，缺位置时形状规则不能命中。显式列表查询会清空调用方结果。排序稳定性依赖候选、业务规则与键的一致输入，不代表跨平台完整战斗确定性或零 GC。
