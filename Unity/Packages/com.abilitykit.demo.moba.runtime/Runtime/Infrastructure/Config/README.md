# MOBA 运行时配置

本目录包含 MOBA 示例逻辑世界的运行时配置层。

## 所有权

- `com.abilitykit.demo.moba.share` 负责 `Runtime/Game/Config/Dto` 下的共享 DTO 定义。
- `com.abilitykit.demo.moba.runtime` 负责运行时 MO 模型、配置表注册、加载配置和逻辑世界访问 API。
- 平台或宿主包负责实际资产来源，例如 Unity Resources、ET 文件系统路径或外部服务。

## 目录结构

```text
Config/
|-- Core/
|   |-- MobaConfigDatabase.cs
|   |-- MobaConfigLoadPipeline.cs
|   |-- IMobaConfigLoadProfile.cs
|   |-- IMobaConfigDtoProvider.cs
|   |-- IMobaConfigDtoDeserializer.cs
|   |-- IMobaConfigDtoBytesDeserializer.cs
|   |-- IMobaConfigTableRegistry.cs
|   |-- MobaConfigPaths.cs
|   |-- MobaConfigGroups.cs
|   |-- IConfigGroup.cs
|   |-- IConfigGroupProvider.cs
|   |-- MobaAttrTypes.cs
|   `-- ...
|
|-- BattleDemo/
|   |-- MobaConfigRegistry.cs
|   |-- Loaders/
|   |-- Deserializers/
|   `-- MO/
|       |-- CharacterMO.cs
|       |-- SkillMO.cs
|       |-- BuffMO.cs
|       |-- MobaCoreDtos.cs
|       `-- ...
|
`-- README.md
```

## 主要扩展点

新宿主或项目应选择以下一种集成路径。

### 默认 Resources 配置

当宿主可以提供 `ITextAssetLoader`，且导出的 JSON 文件位于默认 Resources 目录下时使用此方式。

```csharp
var database = new MobaConfigDatabase(textAssetLoader: textAssetLoader);
database.LoadFromResources(MobaConfigPaths.DefaultResourcesDir);
```

### 基于来源加载

当宿主可以通过通用 Ability 配置来源抽象公开配置文件时使用此方式。

```csharp
var database = new MobaConfigDatabase();
database.LoadFromSource(configSource, basePath: "moba");
```

### DTO 提供者加载

当宿主已负责反序列化，只需向逻辑层提供 DTO 数组时使用此方式。

```csharp
var database = new MobaConfigDatabase();
database.LoadFromDtoProvider(dtoProvider);
```

提供者只需实现 `IMobaConfigDtoProvider`。运行时注册表会决定当前项目需要哪些 DTO 数组。

## 运行时访问

游戏逻辑应通过 `MobaConfigDatabase` 或依赖它的注入服务读取配置。

```csharp
var skill = database.GetSkill(skillId);
if (database.TryGetCharacter(characterId, out var character))
{
    // 使用运行时 MO 数据。
}
```

功能代码不应依赖 MOBA 专用便捷方法时，也可以使用通用配置表访问方式。

```csharp
var table = database.GetTable<SkillMO>();
var skill = table.Get(skillId);
```

## 设计规则

- DTO 应留在 share 包中，使编辑器、视图、服务端、ET 和运行时代码复用同一套契约。
- 运行时 MO 类型应留在本包中，因为它们描述逻辑世界行为和便捷访问方式。
- 宿主集成应优先使用 `IMobaConfigLoadProfile` 或 `IMobaConfigLoadPipeline`，不要直接调用大量数据库加载方法。
- 外部存储系统优先使用 `IConfigSource`；已完成配置反序列化的宿主优先使用 `IMobaConfigDtoProvider`。
- 添加新表时，先更新 `MobaConfigRegistry` 和 share 包的 DTO 目录；仅当运行时逻辑需要更丰富的访问方式时再添加 MO 转换。

## 当前整理方向

`MobaConfigDatabase` 为现有调用方保持向后兼容，但新集成应把它视为运行时门面，而不是绑定特定来源的加载器。加载策略归配置或管线负责，配置表访问归数据库门面负责。
