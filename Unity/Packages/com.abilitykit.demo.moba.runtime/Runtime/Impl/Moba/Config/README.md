# Config 目录结构重组

## 当前目录结构

```
Config/
├── BattleDemo/                      # 业务层 - 业务实现和工具
│   ├── MobaConfigRegistry.cs        # 配置表注册表
│   ├── Loaders/
│   │   ├── DefaultMobaConfigLoader.cs      # 默认加载器
│   │   └── ResourcesLoaders.cs              # Resources 加载器
│   ├── Deserializers/
│   │   ├── JsonNetMobaConfigDtoDeserializer.cs    # Json.NET 反序列化
│   │   └── LubanMobaConfigDtoDeserializer.cs       # Luban 反序列化（已弃用）
│   │   └── LubanMobaConfigDtoBytesDeserializer.cs  # Luban 字节反序列化（已弃用）
│   └── Editor/
│       └── ConfigValidator.cs       # 配置验证工具
│
├── MO/                             # 运行时业务对象（24 个文件）
│   ├── CharacterMO.cs
│   ├── SkillMO.cs
│   ├── BuffMO.cs
│   └── ... (更多 MO 类)
│
├── LubanGen/                       # Luban 生成的代码
│   ├── Characters.cs
│   ├── Buffs.cs
│   ├── Tables.cs
│   └── ...
│
├── Root Files/                     # 根目录核心文件
│   ├── MobaConfigDatabase.cs       # 配置数据库
│   ├── MobaConfigPaths.cs         # 路径常量
│   ├── MobaConfigGroups.cs         # 配置组定义
│   ├── IConfigGroup.cs             # 配置组接口
│   ├── ConfigGroups.cs             # 配置组实现
│   ├── ConfigGroupLoaders.cs       # 加载器实现
│   ├── ConfigGroupDeserializers.cs # 反序列化器实现
│   ├── ConfigTableEntry.cs         # 配置表条目
│   ├── MobaCoreDtos.cs            # Core DTOs
│   ├── IMobaConfigTableRegistry.cs # 表注册器接口
│   ├── IMobaConfigDtoDeserializer.cs # DTO 反序列化器接口
│   └── ...
│
└── [Legacy Files]                  # 遗留文件（暂保留）
    ├── MobaRuntimeConfigTableRegistry.cs  # 表注册表
    ├── IMobaConfigSource.cs        # 配置源接口
    ├── IMobaConfigBytesLoader.cs   # 字节加载器接口
    └── ...
```

## 分类说明

### BattleDemo/ - 业务实现层

新增的业务层，包含：
- `MobaConfigRegistry` - 配置表注册表
- `Loaders/` - 加载器实现
- `Deserializers/` - 反序列化器
- `Editor/ConfigValidator` - 配置验证工具

**命名空间**: `AbilityKit.Ability.Impl.BattleDemo.Moba.Config`

### MO/ - 运行时业务对象

包含从 DTO 到运行时业务对象的转换类：
- `CharacterMO`, `SkillMO`, `BuffMO` 等
- 每个 MO 类接收对应的 DTO 并提供强类型访问接口

**命名空间**: `AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO`

### LubanGen/ - 生成的代码

Luban 配置工具自动生成的 DTO 代码，不应手动修改。

**命名空间**: `AbilityKit.Ability.Impl.BattleDemo.Moba.Config.LubanGen`

### Root Files - 核心配置系统

原有的配置系统核心文件，包括：
- `MobaConfigDatabase` - 配置数据库
- `IConfigGroup` - 配置组接口
- `MobaConfigGroups` - 配置组定义

**命名空间**: `AbilityKit.Ability.Impl.BattleDemo.Moba.Config`

## 使用方式

### 验证配置加载

```csharp
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Editor;

// 验证配置
var result = ConfigValidator.ValidateFromResources("moba");
if (!result.IsSuccess)
{
    Debug.LogError(result);
}
```

### 切换配置数据源

1. **使用默认 Resources 加载**:
```csharp
var db = new MobaConfigDatabase();
db.LoadFromResources("moba");
```

2. **使用自定义 TextSink**:
```csharp
var sink = ConfigValidator.CreateTextSinkFromDictionary(myConfigDict);
var db = new MobaConfigDatabase();
db.LoadFromTextSink(sink);
```

3. **使用配置组**:
```csharp
var db = new MobaConfigDatabase();
db.LoadFromGroups(MobaConfigGroups.All);
```

## 后续优化方向

1. **配置验证工具扩展**
   - 添加引用检查（验证配置间 ID 引用）
   - 添加完整性检查
   - 添加编辑器 UI 工具

2. **配置组系统完善**
   - 支持多数据源优先级覆盖
   - 支持热重载

3. **性能优化**
   - 配置预加载机制
   - 增量更新支持
