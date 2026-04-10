# AbilityKit GameplayTags

游戏标签系统模块，对标 Unreal Engine GAS 的 GameplayTags。

## 命名规范

| Unreal Engine | AbilityKit |
|--------------|-----------|
| `FGameplayTag` | `GameplayTag` (struct) |
| `FGameplayTagContainer` | `GameplayTagContainer` |
| `FGameplayTagManager` | `GameplayTagManager` |
| `FGameplayTagRequirements` | `GameplayTagRequirements` |
| `FGameplayTagDelta` | `GameplayTagDelta` |
| `UGameplayTagsManager` | `GameplayTagService` |

## 目录结构

```
com.abilitykit.gameplaytags/
├── package.json
├── Runtime/
│   ├── AbilityKit.GameplayTags.asmdef
│   ├── GameplayTags/
│   │   ├── GameplayTag.cs           ← 核心结构体
│   │   ├── GameplayTagContainer.cs  ← 标签容器
│   │   ├── GameplayTagManager.cs    ← 单例管理器
│   │   ├── GameplayTagRequirements.cs
│   │   ├── GameplayTagDelta.cs
│   │   ├── GameplayTagSource.cs
│   │   ├── GameplayTagTemplate.cs  ← 模板资产
│   │   ├── GameplayTagCollection.cs ← 标签集合模板
│   │   ├── GameplayTags.cs          ← 静态工具类
│   │   ├── GameplayTagsLib.cs      ← 预定义标签库
│   │   ├── IGameplayTagService.cs
│   │   └── GameplayTagService.cs
│   └── Compatibility/
│       └── TagSystemCompat.cs       ← 兼容性别名
└── Editor/
    ├── AbilityKit.GameplayTags.Editor.asmdef
    └── GameplayTags/
        ├── GameplayTagEditorWindow.cs
        ├── GameplayTagCollectionEditor.cs
        └── GameplayTagDrawer.cs
```

## 核心类型

### GameplayTag

游戏标签结构体，使用 int ID 高效存储。

```csharp
// 创建标签
var stunTag = GameplayTags.Tag("Status.Stun");

// 层级匹配
var damageTag = GameplayTags.Tag("Damage.Type.Fire");
var fireTag = GameplayTags.Tag("Fire");

// 检查匹配（父子层级）
bool isMatch = fireTag.IsChildOf(damageTag); // true
```

### GameplayTagContainer

标签容器，支持高效查询。

```csharp
var container = new GameplayTagContainer();
container.Add(GameplayTags.Tag("Status.Stun"));
container.Add(GameplayTags.Tag("Buff.Shield"));

// 精确匹配
bool hasExact = container.HasTagExact(stunTag);

// 层级匹配
bool hasTag = container.HasTag(fireTag);
```

### GameplayTagTemplate

标签模板资产，用于配置标签组合。

```csharp
// 创建 ScriptableObject 模板
// Assets/AbilityKit/GameplayTags/MyTemplate.asset

// 在代码中使用
service.ApplyTemplate(ownerId, template, source);
```

### GameplayTagCollection

标签集合模板，方便批量管理和引用。

```csharp
// 硬控标签集合
[CreateAssetMenu(menuName = "AbilityKit/GameplayTags/TagCollection")]
public class HardControlCollection : ScriptableObject
{
    public List<GameplayTag> tags = new()
    {
        GameplayTags.Tag("Status.Stun"),
        GameplayTags.Tag("Status.Freeze"),
        GameplayTags.Tag("Status.Fear"),
    };
}

// 使用
var collection = Resources.Load<GameplayTagCollection>("Collections/HardControl");
var container = collection.ToContainer();
```

## 使用示例

### 基本用法

```csharp
// 请求标签
var tag = GameplayTags.Tag("Skill.Attack.Basic");

// 使用服务
var service = GameplayTagService.Instance;

// 添加标签
service.AddTag(ownerId, tag, GameplayTagSource.System);

// 检查标签
bool hasStun = service.HasTag(ownerId, stunTag);

// 应用模板
service.ApplyTemplate(ownerId, template, source);

// 监听变化
service.TagsChanged += (ownerId, delta, source) =>
{
    if (!delta.Added.IsEmpty)
    {
        foreach (var tag in delta.Added)
        {
            Debug.Log($"Tag added: {tag}");
        }
    }
};
```

### 标签需求检查

```csharp
var requirements = new GameplayTagRequirements(
    required: GameplayTags.MakeContainer(tag1, tag2),
    blocked: GameplayTags.MakeContainer(blockedTag)
);

if (requirements.IsSatisfiedBy(currentTags))
{
    // 可以应用效果
}
```

## 编辑器工具

通过菜单 `Window > AbilityKit > Gameplay Tags` 打开标签编辑器。

功能：
- 查看所有注册的标签
- 添加新标签
- 生成代码

## 依赖

无依赖，基础模块。

## 更新其他模块

如果其他模块需要使用新的 GameplayTags 模块，需要：

1. 更新 `package.json` 添加依赖：
```json
"dependencies": {
    "com.abilitykit.gameplaytags": "0.0.1"
}
```

2. 更新 `*.asmdef` 添加引用：
```json
"references": [
    "AbilityKit.GameplayTags"
]
```
