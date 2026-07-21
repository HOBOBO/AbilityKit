# 配置三套目录分工

位于 `src/AbilityKit.Demo.Moba.Console/Configs/`（csproj 复制到输出，Tests 也引用）。

## 三套目录

| 目录 | 职责 | 归属 skill |
|------|------|----------|
| `Configs/moba/` | MOBA 战局主配置（28 个 JSON）—— 角色技能/buff/属性等数据表 | moba-demo（本 skill） |
| `Configs/luban/` | Luban 原始表（4 个）—— 供 `Runtime/Infrastructure/Config/BattleDemo/LubanGen/` 生成代码消费 | moba-demo（本 skill） |
| `Configs/ability/` | AbilityKit 触发器/技能规则配置 | **ability-kit skill** |

## Configs/moba/（28 个）

```
aoes.json                        ongoing_effects.json
attr_types.json                  passive_skills.json
attribute_templates.json         presentation_templates.json
attributetemplates.json          projectile_launchers.json
battle_start.json                projectiles.json
buffs.json                       search_query_templates.json
characters.json                  skill_button_templates.json
component_templates.json         skill_flows.json
continuous_processes.json        skill_level_tables.json
continuous_tag_templates.json    skills.json
demo_tbitem.json                 spawn_summon_action_templates.json
dtbuff.json                      summons.json
effect_plans.json                tag_templates.json
effects.json                     gameplays.json
emitters.json                    models.json
motion_groups.json
```

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

## 技能 ID 命名规则（仅 Configs/moba/characters.json 成立）

- `characters.json`：`Id=1001 廉颇` → `SkillIds=[10010101, 10010201, 10010301]` / `PassiveSkillIds=[10010000]`
- `skills.json`：含对应 Id（`10010101` 爆裂冲撞 / `10010201` 熔岩重击 / `10010301` 天崩地裂）
- 规律：`{charId 4 位}{slot/branch}`
- **注意**：`Configs/luban/characters.json` 没有 `SkillIds`，此映射只对 `Configs/moba/characters.json` 成立
