# ConfigDatabase 使用指南

> 文档类型：使用指南 / API 参考
> 适用范围：Ability 0.1.0 当前源码；环境声明见 [package.json](../package.json)。
> 核校状态：已核校本文通用配置契约和最小示例；不涵盖全部 Ability 能力或 Unity 平台验收。
> 事实基线：2026-11-25。

## 1. 目标与前置条件

`AbilityKit.Ability.Config.ConfigDatabase` 将 DTO 数组构建为按整数键查询的 DTO 表和运行时条目表。宿主提供注册表与反序列化器，并负责输入来源、调用时机、业务验证和缓存失效。数据库不自动读文件或订阅变更。

主要入口：

- [注册定义及注册表](../Runtime/Ability/Config/Core/IConfigTableRegistry.cs)
- [查询接口、结果与实际数据库事件总线](../Runtime/Ability/Config/Core/IConfigDatabase.cs)
- [全量加载实现](../Runtime/Ability/Config/Database/ConfigDatabase.FullReload.cs)和[增量实现](../Runtime/Ability/Config/Database/ConfigDatabase.cs)
- [强类型构表工厂](../Runtime/Ability/Config/Database/IntKeyConfigTable.cs)、[JSON 反序列化器](../Runtime/Ability/Config/Deserializers/JsonNetConfigDeserializer.cs)

## 2. 最短接入与预期结果

以下完整类可放入调用方程序集，调用 `ConfigDatabaseQuickStart.Run()`。它显式登记一张表，使用成对工厂处理 DTO 到条目的转换，不依赖 MOBA 或编译期生成器。

```csharp
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;

public static class ConfigDatabaseQuickStart
{
    public sealed class ItemDto
    {
        public int Id;
        public int Damage;
    }

    public sealed class Item
    {
        public int Damage { get; }
        public Item(ItemDto dto) { Damage = dto.Damage; }
    }

    private sealed class Registry : ConfigTableRegistryBase
    {
        public Registry() : base(new[]
        {
            new ConfigTableDefinition(
                "items", typeof(ItemDto), typeof(Item), "example",
                dtos => ConfigTableFactory.CreateDtoTable<ItemDto>(dtos, d => d.Id),
                dtos => ConfigTableFactory.CreateEntryTable<ItemDto, Item>(
                    dtos, d => d.Id, d => new Item(d)),
                (dtos, ids) => ConfigTableFactory.CollectChangedIds<ItemDto>(
                    dtos, ids, d => d.Id))
        }) { }
    }

    public static int Run()
    {
        var database = new ConfigDatabase(new Registry(), JsonNetConfigDeserializer.Instance);
        var result = database.ReloadFromTexts(new Dictionary<string, string>
        {
            ["items"] = @"[{""Id"":1,""Damage"":42}]"
        });
        if (!result.Succeeded) throw new InvalidOperationException(result.Error);
        return database.GetTable<Item>().Get(1).Damage;
    }
}
```

预期返回 `42`，成功后 `Version` 为 `1`。条目和 DTO 由调用方定义；转换及自定义表工厂应避免外部副作用，数据库不会为它们提供补偿。`DtoTableFactory` 与 `EntryTableFactory` 必须同时提供或同时不提供，只有一项会在构造定义时抛 `ArgumentException`。

不提供工厂时走反射兼容路径：DTO 需可读的公开整数 `Id`，没有时尝试 `Code`；条目需可由 DTO 调用构造函数。使用显式工厂可以自行选择整数键；AOT/裁剪兼容性仍须在目标环境验证。注册表基类不会替你校验所有路径、DTO 和条目类型是否唯一，调用方应保持唯一，不能依赖重复定义覆盖的副作用。

## 3. 选择加载入口

以下均为具体 `ConfigDatabase` 的能力；`IConfigDatabase` 只暴露版本、条目表查询和 Source 的基础 Load/Reload，不能经接口直接调用所有重载。

| 入口 | 输入与选择规则 | 失败边界 |
| --- | --- | --- |
| `Reload(source, basePath, strict)` | 完整路径 bytes → 完整路径 text → 原始表路径 text；解析失败可继续下一个候选 | 最后仍不可读时 strict 返回失败，非 strict 建空表；Source 自身抛错和构表异常不统一转结果 |
| `ReloadFromTexts(texts, basePath)` | 完整路径文本非空才选取，否则尝试原始路径；不自动补扩展名 | 无 strict 参数；缺失或解析抛错返回失败，构表异常可直接抛出 |
| `ReloadFromBytes(bytes, basePath)` | 先按完整路径查键，只有键不存在才查原始路径 | 完整路径键存在但为空不会再查原始 bytes；缺失、空数据、解析抛错返回失败，构表异常可直接抛出 |
| `ReloadFromMixed(bytes, texts, bytesBasePath, textsBasePath, strict)` | 可用 bytes 优先；没有可用 bytes 才用文本 | 已选 bytes 解析失败即失败，不再回退文本；非 strict 仅允许缺失输入，不能忽略解析失败 |
| `ReloadFromDtoArrays(arrays, strict)` | 键为精确 DTO 类型，值为同元素类型的 `Array` | 非 strict 可补缺失空表，类型不匹配仍失败；构表异常转失败结果 |
| `ReloadFromGroups(groups)` | 遍历各组声明的表，先本组 Loader，再后续组 Loader；仍由声明组的反序列化器解析 | 不向前搜索；Loader 返回成功但数据为空即失败；后续重复声明可以覆盖先前候选 |
| `ReloadIncremental(changes)` | 用注册表原始 `FilePath` 匹配 `TableName`；每项携带一张表的完整新内容 | 这是按表替换，不是行级 patch；见下一节 |

`Load*` 是对应 Reload 入口的别名，不是另一种追加模式。键名和大小写由注册及来源决定，默认字典来源使用 Ordinal；路径不自动添加 `.json` 或 `.bytes`。

默认 `JsonNetConfigDeserializer` 只支持 JSON 文本：非数组根返回空数组，单行转换失败会被跳过。`strict=true` 不是 JSON schema 或业务有效性的严格校验；需要拒绝坏行的项目应提供自己的反序列化器和业务验证。默认 bytes 反序列化不受支持。

## 4. 重载身份与失败契约

### 全量加载

所有候选表准备完成后才替换数据库中的表集合并增加版本。正常加载或构表失败发生在提交前时，旧表和版本保持不变；方法可能返回 `Succeeded=false`，也可能直接抛错，具体取决于上表入口。

成功的全量加载换用新表对象；之前缓存的 `IConfigTable`、DTO 表与行对象仍指向旧数据。读取方应在成功后重新取得表和条目。`GetTable<T>()` 和 `GetDtoTable<T>()` 在没有表时会创建并缓存空表，并不证明该类型已登记或输入已经加载；只判断存在性时用 `TryGetTable<T>()`。缺 ID 的 `Get` 抛 `KeyNotFoundException`，可选查询用 `TryGet`。

### 增量加载

每个 change 的有效数组表示该表的新全集，未包含的旧行会消失。空文本、null/空 bytes 被标为删除：已存在或本批已准备的表不支持这样删除，返回失败并要求全量重载；此前根本不存在的注册表可跳过删除项。非空文本 `[]` 可把表内容替换为空表。

整批先构表并验证替换能力，再按顺序提交。已有 DTO 表和条目表可原位替换时保留表对象，**不保证行对象身份**。自定义工厂返回的已有表必须实现 [IConfigTableContentReplacer](../Runtime/Ability/Config/Database/IConfigTableContentReplacer.cs)，且候选与旧表实际类型一致，否则预检失败；自定义替换在提交时意外抛错时，已替换的前序表不统一回滚。

同一条目类型多次出现在一批时，最终表内容来自最后一个候选；`ChangedIds` 却汇集本批输入 DTO 的整数键，既不是值差异比较，也不包含已消失旧行的 ID，不按表分组且不保证顺序。空 change 列表直接返回当前版本成功、不发通知；非空请求即使只跳过不存在表的删除，仍递增版本并通知。

### 通知、线程与所有权

数据库使用 **`AbilityKit.Ability.Config.ConfigReloadBus.OnPublished`**，`Key` 为 `config`。包中另有 `AbilityKit.Ability.HotReload.ConfigReloadBus.Reloaded`，它不是数据库的事件通道，不能用同名短类名混订。

通知在提交和 `Version++` 之后同步执行，订阅者抛错会传播；这时数据已经提交，后续订阅者也可能未收到通知。空 Source、空文本字典等部分早退只返回失败而不广播，不能把总线当完整的请求审计。直接检查结果，同时处理异常；若要判断异常是否发生在提交后，应结合调用前后的版本和业务状态。

总线为静态事件，订阅方拥有退订责任。数据库不实现 `IDisposable`，不持有文件句柄，但仍需在宿主结束时移除自己的事件处理器。表和 DTO 返回原对象而非不可变副本，`All()` 为 live view；数据库无读写锁，宿主应将读取、枚举与重载串行化，并避免在通知中重入 Reload。缓存重建、外部副作用和跨系统回滚由项目决定。

`RegisterTable` 当前只校验 null 和重复路径，不实际扩展注册表。请在构造注册表时登记，不能用这个方法宣称已动态注册。

## 5. 排错与验证

| 现象 | 检查方向 |
| --- | --- |
| `GetTable` 返回空表 | 是否尚未成功加载、用了错误类型，或默认 JSON 适配器把非数组/坏行变为空/缺行 |
| 更新了 JSON 却仍读旧值 | 是否优先命中 bytes、带前缀资源或已缓存的全量旧表；比较 Version 并重取表 |
| 非 strict 仍失败 | 仅允许缺失输入，不放宽数组元素类型、已选二进制解析或业务工厂异常 |
| Reload 抛错但值已变化 | 检查同步通知或自定义增量提交；不要仅按异常认定未提交 |
| 事件监听不到 | 确认完整命名空间、事件名，以及该失败分支是否实际 Publish |

2026-11-25，在 .NET 10 对当前通用 ConfigDatabase 的全量与扩展契约执行 17 项测试并通过；覆盖六种全量入口的缺失后表不提交、成功换表、Source/Mixed 解析回退差异、提交通知时序、组装载和外部表替换。另从本页提取最小示例编译运行。订阅者异常传播属于本批源码核校，未由这 17 项测试覆盖。它们是局部 E3，不证明全部 Ability、Unity GUI、IL2CPP、独立安装或完整发布链。

跨包所有权与实际证据记录由仓库设计专题维护；这里仅保留本包独立可读的使用契约。
