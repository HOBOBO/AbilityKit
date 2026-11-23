# AbilityKit Combat SkillLibrary

`com.abilitykit.combat.skilllibrary` 提供按主键保存技能数据、按数据字段派生查询索引的容器。它不负责加载技能配置或执行技能。

## 安装与接入

从项目配置的包来源安装本包；Unity 版本及依赖见 [`package.json`](package.json)。

```csharp
using AbilityKit.Ability.Battle.SkillLibrary;

var skills = new SkillLibrary<int, string>();
var byCategory = skills.CreateDerivedKeyedIndex(category => category);
skills.Add(100, "Movement");
var movementSkillIds = byCategory.Get("Movement");
```

`movementSkillIds` 包含主键 `100`。本例用字符串简化技能数据；实际项目通常从数据对象中提取分类。

- [使用契约与限制](Documentation~/Usage.md)：索引维护、更新及失败边界。
- [历史设计入口](Document/README.md)。
