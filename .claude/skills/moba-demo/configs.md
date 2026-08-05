# 配置目录与 DTO/MO 加载

位于 `src/AbilityKit.Demo.Moba.Console/Configs/`（csproj 复制到输出，Tests 也引用）。

## 三套目录

| 目录 | 职责 | 归属 skill |
|------|------|----------|
| `Configs/moba/` | MOBA 战局主配置——角色、技能、buff、属性、地图、AI 等数据表 | moba-demo（本 skill） |
| `Configs/luban/` | Luban 原始表（4 个）—— 供 `Runtime/Infrastructure/Config/BattleDemo/LubanGen/` 生成代码消费 | moba-demo（本 skill） |
| `Configs/ability/` | AbilityKit 触发器/技能规则配置 | **ability-kit skill** |

## Configs/moba/

主配置 JSON 的数量会随业务扩展变化，以目录实际内容和 `MobaConfigTableDeclarations.cs` 为准。当前覆盖角色、技能、被动、buff、continuous、弹射物、召唤、目标查询、表现、地图、玩法、状态机、AI brain 与 motion 等配置。

## Configs/luban/（4 个）

`attributetemplates.json` / `buffs.json` / `characters.json` / `demo_tbitem.json`

对应 `Runtime/Infrastructure/Config/BattleDemo/LubanGen/` 下的 `DR*.cs`（生成代码）。

## Configs/ability/（详见 ability-kit skill）

`ability_trigger_plans.json` + `_source_example` / `ability_triggers.json` / `trigger_manifest.json` / `trigger_source_manifest.json` / `triggering_event_map.json`

子目录：

- `trigger_sources/`
- `rules/{commit,release}/skill_*_default.json`
- `triggers/{buffs,passives,skills}/trigger_*.json`（上百个分技能/buff/被动触发配置）

## 加载链

```
Console 启动
    ↓
Bootstrap/ConsoleLubanConfigLoader 加载 Configs/luban/
    ↓
Bootstrap/ConsoleConfigLoader 加载 Configs/moba/
    ↓
MobaConfigDatabase 注册（共享给 Unity 与 Console）
    ↓
runtime 的 ConfigStage（Bootstrap 第 2 阶段）
    ↓
Infrastructure/Config/BattleDemo/LubanGen/Tables.cs 消费 Luban 表
    ↓
TriggerPlansStage + PlanTriggeringStage 经 TriggerPlanJsonDatabase 加载 Configs/ability/
```

## 配置表声明与生成

正式注册入口是 `Runtime/Infrastructure/Config/Core/MobaConfigTableDeclarations.cs`。每张表只增加一条 assembly `MobaConfigTable` 声明：

```csharp
[assembly: MobaConfigTable(
    MobaConfigPaths.SkillsFile,
    typeof(SkillDTO),
    typeof(SkillMO),
    ConfigGroupNames.LegacyJson,
    30)]
```

不要再同步手写 `MobaConfigRegistry` 或 `MobaConfigGroups` 条目。Generator 会生成共享 Manifest、强类型 DTO/MO table factory 和 changed-ID collector。

DTO 与 MO 可以字段数量不同、字段类型不同，也可以包含业务规范化。所有特殊转换写在 `MO(DTO)` 构造器中：

```csharp
public SkillMO(SkillDTO dto)
{
    SkillType = (SkillType)dto.SkillType;
    Tags = dto.Tags ?? Array.Empty<int>();
}
```

Generator 只调用 `new SkillMO(dto)`，不推断或复制字段映射。新增表和排查 `AKSG1001`/`AKSG1002`/`AK1004` 时读取 [codegen_analyzer.md](codegen_analyzer.md)。

## 技能 ID 命名规则（仅 Configs/moba/characters.json 成立）

- `characters.json`：`Id=1001 廉颇` → `SkillIds=[10010101, 10010201, 10010301]` / `PassiveSkillIds=[10010000]`
- `skills.json`：含对应 Id（`10010101` 爆裂冲撞 / `10010201` 熔岩重击 / `10010301` 天崩地裂）
- 规律：`{charId 4 位}{slot/branch}`
- **注意**：`Configs/luban/characters.json` 没有 `SkillIds`，此映射只对 `Configs/moba/characters.json` 成立
