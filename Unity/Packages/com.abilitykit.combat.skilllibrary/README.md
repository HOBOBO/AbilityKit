# AbilityKit Combat SkillLibrary

`com.abilitykit.combat.skilllibrary` 提供按主键保存技能数据、按数据字段派生查询索引的容器。它不负责加载技能配置或执行技能。

## 安装与接入

在 Unity Package Manager 中从项目配置的来源安装；本地开发可选择本目录 `package.json`，并确保 Core 可解析。本次核校版本 0.1.0、Unity 最低声明 2022.3，声明来源为 [`package.json`](package.json)；本批没有重做干净 UPM 安装。

```csharp
using AbilityKit.Ability.Battle.SkillLibrary;

var skills = new SkillLibrary<int, string>();
var byCategory = skills.CreateDerivedKeyedIndex(category => category);
skills.Add(100, "Movement");
var movementSkillIds = byCategory.Get("Movement");
```

`movementSkillIds` 包含主键 `100`。本例用字符串简化技能数据；实际项目通常从数据对象中提取分类。完整替换更新、后加索引回填与删除示例见使用指南。结束时由宿主解除容器引用并释放自己加载的数据资源。

- [包内文档索引](Documentation~/README.md)。
- [使用契约与限制](Documentation~/Usage.md)：索引维护、更新及失败边界。
- [包内样例](Runtime/SkillLibrary/SkillLibraryExample.cs)。
- [历史设计入口](Document/README.md)。

## 限制与核校范围

原地修改分类后调用 NotifyUpdated 无法移除旧 bucket；应使用不可变数据或新对象调用 Update。查询结果为内部集合视图，异常不回滚，不保证并发与原子热重载。2026-11-25 已核校上述限定契约及完整本地示例；本包暂无专属测试工程，不据此宣称完整业务、Unity 或发布验收。
