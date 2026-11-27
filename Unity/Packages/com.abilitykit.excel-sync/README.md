# AbilityKit Excel Sync

`com.abilitykit.excel-sync` 提供 Unity Editor 中 Excel 与 ScriptableObject 表资产的导入、三方合并导出和代码生成工具。它不负责运行时读取、Luban 发布或项目业务校验。

## 环境与依赖

包声明最低 Unity 版本为 2021.3，版本和其他声明以 [package.json](package.json) 为准。编辑器实现还使用项目已有的 Odin Inspector、Newtonsoft.Json 和包内 EPPlus；这些程序集未列为 `package.json` 依赖，安装前需确认当前 Unity 工程可解析它们。本批隔离工程手动提供了这些依赖，未验证干净 UPM 安装或发行包依赖的自动装配。

## 安装与首次使用

在支持该包的项目包源中安装，或在 Unity Package Manager 选择本目录的 `package.json`。创建 `ExcelSyncProjectConfig` 资产（菜单 `Assets > Create > 工具 > Excel > Excel同步项目配置`），配置 Excel 根目录、代码输出目录和 Asset 输出目录；随后打开 `Tools > AbilityKit > Framework > Excel > 模板管理器`。首次接入应按生成壳代码、补齐 Row 字段、等待 Unity 编译完成、创建并绑定 Table Asset、导入的顺序操作。生成器不会自动等待首次编译；最小试用与预期结果见[使用指南](Documentation~/Usage.md#最小试用与预期结果)。

导入会建立 `.excelBaseline.asset`；导出要求 Excel 文件、已绑定资产和既有 baseline。冲突会写 `.conflicts.json` 并中止该工作簿保存。批量操作不是跨表事务，导出也不替代业务校验和发布审查。

## 文档与验证

- [包内文档索引](Documentation~/README.md)
- [使用指南](Documentation~/Usage.md)

跨包设计专题为仓库 `Docs/design/05-CommonModules/09-ExcelScriptableObjectSync.md`；包级 EditMode 测试及门禁配置也由仓库维护。本批在 Unity 2022.3.62f1 隔离工程完成 11 项局部调用检查，见指南的验证范围；Unity GUI、独立安装和实际发布链未在本批验证。
