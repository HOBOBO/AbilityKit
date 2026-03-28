# Config 目录结构

## 当前目录结构

```
Config/
├── Core/                              # 核心配置系统 - 通用的接口和实现
│   ├── MobaConfigDatabase.cs           # 配置数据库
│   ├── MobaConfigPaths.cs             # 路径常量
│   ├── MobaConfigGroups.cs            # 配置组定义
│   ├── IConfigGroup.cs                 # 配置组接口
│   ├── ConfigGroups.cs                 # 配置组实现
│   ├── ConfigGroupLoaders.cs           # 加载器实现
│   ├── ConfigGroupDeserializers.cs     # 反序列化器实现
│   ├── ConfigTableEntry.cs             # 配置表条目
│   ├── ConfigGroupNames.cs             # 配置组名称常量
│   ├── IConfigGroupProvider.cs         # 配置组提供者接口
│   ├── MobaAttrTypes.cs                # 属性类型定义
│   ├── MobaConfigFormat.cs             # 配置格式枚举
│   ├── MobaConfigFormatProvider.cs     # 配置格式提供者
│   ├── MobaConfigTextSinkAdapter.cs    # 配置文本槽适配器
│   ├── IMobaConfigDtoDeserializer.cs          # DTO 反序列化器接口
│   ├── IMobaConfigDtoBytesDeserializer.cs     # DTO 字节反序列化器接口
│   ├── IMobaConfigTableRegistry.cs            # 表注册器接口
│   ├── IMobaConfigSource.cs                   # 配置源接口
│   ├── IMobaConfigLoader.cs                  # 配置加载器接口
│   ├── IMobaConfigBytesLoader.cs             # 字节加载器接口
│   └── IMobaConfigBytesSource.cs             # 字节源接口
│
├── BattleDemo/                        # 业务实现层 - BattleDemo 具体实现
│   ├── MobaConfigRegistry.cs          # 配置表注册表
│   ├── UnityResourcesMobaConfigSource.cs      # Unity Resources 配置源
│   ├── UnityResourcesMobaConfigBytesSource.cs # Unity Resources 字节源
│   │
│   ├── DTOs/                          # DTO 定义
│   │   └── (从 MobaCoreDtos.cs 等文件中的 DTO)
│   │
│   ├── MO/                            # 运行时业务对象
│   │   ├── CharacterMO.cs
│   │   ├── SkillMO.cs
│   │   ├── BuffMO.cs
│   │   ├── MobaCoreDtos.cs            # 核心 DTO 定义
│   │   ├── MobaSkillEffects.cs        # 技能效果 DTO
│   │   ├── MobaSkillLevelTable.cs     # 技能等级表 DTO
│   │   └── ... (更多 MO 类)
│   │
│   ├── Loaders/                       # 加载器实现
│   │   ├── DefaultMobaConfigLoader.cs         # 默认加载器
│   │   └── DefaultMobaConfigBytesLoader.cs   # 默认字节加载器
│   │
│   ├── Deserializers/                 # 反序列化器实现
│   │   ├── JsonNetMobaConfigDtoDeserializer.cs      # Json.NET 反序列化
│   │   ├── LubanMobaConfigDtoDeserializer.cs       # Luban 反序列化（已弃用）
│   │   └── LubanMobaConfigDtoBytesDeserializer.cs  # Luban 字节反序列化（已弃用）
│   │
│   ├── LubanGen/                      # Luban 生成的代码
│   │   ├── Characters.cs
│   │   ├── Buffs.cs
│   │   ├── Tables.cs
│   │   └── ...
│   │
│   └── Editor/                        # 编辑器工具
│       └── ConfigValidator.cs         # 配置验证工具
│
└── README.md
```

## 分类说明

### Core/ - 核心配置系统

核心配置系统，提供通用的接口和抽象，不依赖具体业务实现。

包含：
- `MobaConfigDatabase` - 配置数据库
- `IConfigGroup` - 配置组接口
- `MobaConfigGroups` - 配置组定义
- 各种 `I*` 接口定义

**命名空间**: `AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core`

### BattleDemo/ - 业务实现层

BattleDemo 具体的业务实现，包括：
- `MobaConfigRegistry` - 配置表注册表
- `Loaders/` - 加载器实现
- `Deserializers/` - 反序列化器
- `MO/` - 运行时业务对象
- `Editor/ConfigValidator` - 配置验证工具

**命名空间**: `AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo`

### MO/ - 运行时业务对象

包含从 DTO 到运行时业务对象的转换类：
- `CharacterMO`, `SkillMO`, `BuffMO` 等
- 每个 MO 类接收对应的 DTO 并提供强类型访问接口
- `MobaCoreDtos.cs` - 核心 DTO 定义

**命名空间**: `AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO`

### LubanGen/ - 生成的代码

Luban 配置工具自动生成的 DTO 代码，不应手动修改。

**命名空间**: `AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.LubanGen`

## 使用方式

### 验证配置加载

```csharp
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.Editor;

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
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;

var db = new MobaConfigDatabase();
db.LoadFromResources("moba");
```

2. **使用自定义 TextSink**:
```csharp
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.Editor;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;

var sink = ConfigValidator.CreateTextSinkFromDictionary(myConfigDict);
var db = new MobaConfigDatabase();
db.LoadFromTextSink(sink);
```

3. **使用配置组**:
```csharp
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;

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
