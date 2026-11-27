# AbilityKit Ability

本包包含能力运行时、配置数据库、效果溯源与 Cue 等能力及相关编辑器工具。按使用目标进入对应文档，不必为了加载配置采用 MOBA 的表目录或发布方式。

## 定位与环境

- Unity 最低版本、包版本和直接依赖以 [package.json](package.json) 为准；当前声明为 Unity 2022.3、包版本 0.1.0。
- 本仓库以 embedded package 开发。独立接入时需同时满足声明中的 AbilityKit 包依赖和 Newtonsoft.Json 依赖；单独复制配置目录不是完整安装方式。
- Runtime 程序集、Editor 工具和项目装配各有归属。ConfigDatabase 不负责 Excel 导表、下载、文件监听、业务引用校验或 MOBA 初始化。

## 首次使用

加载普通整数键配置时，从 [ConfigDatabase 使用指南](Documentation~/ConfigDatabase.md) 的完整示例开始：定义 DTO 与条目类型、登记表、注入反序列化器、检查重载结果，再按 ID 查询。工厂可以显式提供，不要求项目安装额外的生成器。

## 文档与样例

- [包内导航与核校范围](Documentation~/README.md)
- [ConfigDatabase：加载、查询、重载和订阅契约](Documentation~/ConfigDatabase.md)
- [效果溯源既有说明](Document/效果溯源（EffectSource）模块开发设计文档.md)
- [效果表现层 Cue 既有说明](Document/效果表现层（Cue）模块开发设计文档.md)
- [包内样例目录](Samples~)

## 限制与核校状态

2026-11-25 本批核校 ConfigDatabase 及其输入选择、构表、提交和事件通道；对应 .NET 局部测试通过。效果溯源、Cue、编辑器和全部样例未因新增本入口而获得核校结论。独立安装、Unity GUI、IL2CPP 和发布资格未在本批验证。
