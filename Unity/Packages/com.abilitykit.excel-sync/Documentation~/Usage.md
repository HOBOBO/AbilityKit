# Excel Sync 使用指南

> 文档类型：使用指南
>
> 适用范围：`com.abilitykit.excel-sync` 0.1.0；最低 Unity 版本见 [package.json](../package.json)
>
> 核校状态：已核校本文限定的配置、模板、导入/导出和失败契约；Unity 2022.3.62f1 隔离工程局部调用验证通过，GUI 与独立安装未验收
>
> 事实基线：2026-11-25

## 环境与前置条件

该包主要功能位于 Unity Editor 程序集中。Unity 工程还须能解析 Odin Inspector 与 Newtonsoft.Json；EPPlus 4 实现在包内。`package.json` 未声明这些外部程序集版本，因此需按项目现有依赖核对。包没有独立 .NET 镜像工程，不能作为 Server 或 Player 表加载器使用。

## 首次接入

1. 安装包并在 Unity 等待初次导入完成。创建 `ExcelSyncProjectConfig` 资产；创建菜单为 `Assets > Create > 工具 > Excel > Excel同步项目配置`。工程也可将其放在 `Resources/ExcelSyncProjectConfig.asset` 供单例按名查找；Editor 回退搜索只取第一个匹配资产，因此工程应保持唯一配置资产。
2. 设置 Excel 根目录、代码输出目录、Table Asset 输出目录、默认命名空间和表头参数。根目录可以是绝对路径，也可以相对 Unity 工程目录。为每张表添加模板，配置 Excel 相对路径、sheet、表头行、数据起始行、主键列、命名空间和输出目录。
3. 打开 `Tools > AbilityKit > Framework > Excel > 模板管理器`，扫描或维护模板。首次接入时先生成 Row/Table 类型，补齐 Row 的实际列字段并等待 Unity 完成编译，再从 `Tools > AbilityKit > Framework > Excel > 批量操作 > 创建 Table Asset 并绑定` 创建/绑定资产，最后导入数据。首次 `GenerateCode` 的 Row 只是空 partial 壳，不会自动从任意 Excel 推导完整字段；已绑定资产时才进入 Raw partial 生成分支。不要把 `GenerateAndImportAll` 当成可靠的编译等待器：当前实现不会等待编译管线。
4. 导入前检查列名、主键唯一性和项目业务约束。导入成功会建立或刷新与 Table Asset 相邻的 `.excelBaseline.asset`。该 baseline 是 Excel/SO 三方比较数据，不是运行时配置快照。

### 最小试用与预期结果

在临时工作簿 `Sample.xlsx` 的 `Sample` sheet 中准备以下内容，使用单独的临时配置且只启用该模板。模板明确设置表头行为 1、数据起始行为 2、主键列为 `code`，不要沿用默认的第 6/8 行。临时命名空间可设为 `Example.Excel`，避免与项目现有类型重名。

| code | Name | Amount | Tags |
| --- | --- | --- | --- |
| 1 | First | 100 | 1,2 |
| 2 | Second | 50 | 4 |

以该模板生成 `SampleRow`/`SampleTable` 后，为空的 `SampleRow` 补齐 `public int code;`、`public string Name;`、`public int Amount;` 和 `public int[] Tags;`，其命名空间与 Table 保持一致；不要在每次重写的 Table 文件里放手写字段。等待编译，创建/绑定 Table Asset，运行 Import：预期 DataList 两条，第一条 Tags 为 `[1, 2]`，资产旁生成 baseline。

将第一条 Amount 改为 200 后 Export，预期 Excel 与 baseline 都为 200；再改回 100 后 Export，两侧均应回到 100。若 Excel 改为 300、SO 改为 400，则预期导出失败、生成 `<Table.asset>.excelBaseline.asset.conflicts.json`，且本次 Excel 字节不变。冲突协调前保留两侧修改；不要直接重新 Import 覆盖尚未合并的 SO 数据。

## 日常同步

- 策划修改 Excel 后执行 Import，再检查 DataList 和 baseline 的变更。
- 程序修改 Table Asset 后执行 Export。Local 相对 Base 有改动且 Remote 未冲突时写回 Excel；相同目标值视为无冲突。Remote 与 Local 对同一单元格分别偏离 Base 时，写出 `.conflicts.json` 并中止该工作簿保存。
- Excel 导入已有的 Remote-only 行会纳入新 baseline；Local 删除与 Remote 修改冲突时，查看冲突报告并人工协调。
- Export 成功保存 Excel 后会重新读取 Excel 刷新 baseline，供下一次三方比较使用。保存和 baseline 刷新不是一个事务；刷新失败时需要核对 Excel 与 baseline 后再继续。
- **成功导出不会把 Excel 单边修改同步回 SO。** 当 Local 没改而 Remote 已改时，本次保留 Remote 并刷新 Base，SO 仍是旧值；直接再次 Export 可能将旧 SO 值写回 Excel。导出后比较两侧、保留本地待合并内容，再按项目流程 Import 对齐 SO；不要仅凭导出成功就继续重复导出。
- 批量操作逐模板收集结果，不具备跨表事务。某些表成功不代表其余表成功；按结果逐项检查。

## 数据与失败契约

- 模板默认第 6 行表头、第 8 行数据、主键列 `code`、空 sheet 选择首表；每项可由模板覆盖。指定 sheet 读不到时，当前 EPPlus reader 回退首表。确认实际读取的是预期 sheet。
- 表头匹配不区分大小写。结构校验不保证主键唯一、外键有效或业务值域正确；空主键、空白主键和数值零主键会跳过。重复主键不会阻止导入，baseline map 仅保留首个同 key 行，故项目需另设唯一性校验。
- 导入先构造 DataList，再赋值并更新 baseline、保存资产；赋值后的异常可能让资产与 baseline 暂时不一致。批量创建/导入的既有成功项不会因后续失败回滚。
- 导出发生单元格冲突时 workbook 的内存修改不调用 `package.Save()`；冲突报告、资产刷新、文件系统和外部并发编辑不构成整体事务。避免 Unity、Excel 与版本控制合并同时写同一文件。
- baseline 仅检查是否存在，不证明文件 hash、schema、sheet、表头和资产仍匹配。迁移列布局或更换源文件时，先核对模板和基线，不要直接覆盖冲突。
- 导出会把 Excel 已存在而 Local 没有的行视为删除候选：该行在 Base 中且 Remote 未改才安全删除；Remote 已改或该行从未出现在 Base 中则报告冲突。Remote 删除而 Local 保留时，当前实现会把 Local 行重新插入 Excel，不会自动删除 SO 条目。
- 默认导出合并直接使用 EPPlus 打开工作簿；传入的 backend 仍参与导入和导出后的 baseline 重读。实现自定义 factory 不代表已经替换了 Safe Export 的合并算法或文件格式。
- 默认 codec registry 是进程级可变单例。自定义 codec 和列表分隔符应在团队统一初始化；注册顺序和列级分隔符影响同一 Editor 进程中的后续转换。

## 代码接口

工具服务入口为 `ExcelSyncTemplateService`，常用操作包括 `GenerateCode`、`CreateOrBindTableAsset`、`Import`、`Export` 及其批量版本。低层 `ExcelSyncService.Import/Export` 需调用方传入 backend；默认 EPPlus 实现通过 `ExcelSyncTemplateService.DefaultBackend` 提供。使用前仍须在 Editor 中准备配置、模板、资产与路径。

扩展表类型编码时实现 `IExcelValueCodec` 并注册到 `ExcelCodecRegistry`；替换读写后端时实现 reader/writer factory。项目级主键、外键、权限、业务校验与发布策略应保留在项目层。

## 发布交接与验证边界

Excel 与 Table Asset 都是编辑输入/中间产物。提交前检查两侧 diff、冲突报告及唯一主键/业务校验；成功导出后复核刷新后的 baseline。进入 ConfigDatabase、Luban 或其他运行时消费链，应显式执行对应转换和发布校验，不能把 Table Asset 自动当成线上权威配置。

2026-11-25 在 Windows / Unity 2022.3.62f1 的隔离工程中，以当前包源码副本和临时表/资产通过 11 项局部调用检查：导入与数组列、baseline 建立、导出刷新、连续导出、冲突文件哈希不变、删除传播、注入 baseline 重读失败后的部分提交、重新导入对齐、Remote 单边修改未导入 SO 与第二次导出的覆盖风险、重复主键折叠限制。该证据为局部 E3；限制复现不等于限制已修复，模板管理器 GUI 与首次代码生成流程未实测。

仓库已有 `moba-config-sync` EditMode gate，覆盖 MOBA Buff 配置切片而非整个包。测试源码位于 `com.abilitykit.demo.moba.editor/Tests/ConfigSync`，gate 定义位于 `tools/test-gates.json`；跨包设计专题位于 `Docs/design/05-CommonModules/09-ExcelScriptableObjectSync.md`。本批没有在活动主工程重跑该 gate；干净 UPM 安装、完整项目 schema/发布链与并发写入未验证。

源码入口：[模板服务](../Editor/Core/ExcelSyncTemplateService.cs)、[单资产导入/导出](../Editor/Core/ScriptableObjectExcelSync.cs)、[baseline](../Editor/Core/ExcelSoSyncBaselineAsset.cs)、[EPPlus backend](../Editor/Backends/EpplusTableReaderWriter.cs)。
