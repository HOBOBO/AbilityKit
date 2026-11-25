# MOBA 技能等级与英雄等级成长参数

MOBA 数值按两个互相独立的等级维度配置：

| 参数表 | 定位键 | 用途 |
| --- | --- | --- |
| `moba-skill-level-parameters.xlsx`（`skill_level_tables`） | 技能 ID + 技能 Rank | 技能升级影响的消耗、冷却和技能本身参数 |
| `hero-level-skill-growth-parameters.xlsx`（`hero_level_skill_growth_parameters`） | 技能 ID + 英雄等级 | 英雄升级影响的伤害、护盾、持续时间、召唤物属性等效果数值 |

技能施放时，运行时保存本次技能的 `SkillId`、技能 Rank 对应参数快照，以及施法时的 `ChampionLevel`。效果动作可以按需读取其中一个维度，不把 Rank 参数和英雄等级参数混在一张表中。当前实现不规定英雄等级或技能 Rank 的上限；调用方请求的组合必须在相应表中有配置。

## 英雄等级成长表

每一行描述一个技能在一个英雄等级的效果成长值。`Id` 是 Luban 行主键；运行时使用 `SkillId + ChampionLevel` 查找行。每类效果值占独立列，例如 `Damage`、`Shield`、`DurationMs`、`SummonHealth`、`SummonDamage` 和 `SummonDurationMs`。新机制需要新的数值时，在 Excel 中增加有明确单位或语义的列，并同步扩展共享 DTO `HeroLevelSkillGrowthParameterDTO` 与运行时 MO `HeroLevelSkillGrowthParameterMO.TryGetValue` 的列映射，然后运行配置导出校验。

`SkillId` 和 `ChampionLevel` 的组合应唯一。列值为 `0` 表示该等级该参数为零；缺少整行或请求了未支持的列名则读取失败，不会退回技能 Rank 表或静默使用默认值。列名大小写不敏感。

## 效果动作配置

Trigger Action 的数值参数使用 `magnitude_type = HeroLevelSkillParameter`，并通过 `magnitude_parameter` 指定表列名，例如 `Damage`。可通过 `magnitude_coefficient` 缩放读取值。组合数值的第二来源使用 `magnitude_secondary_type = HeroLevelSkillParameter` 和 `magnitude_secondary_parameter`。该来源读取当前技能 Runtime 的技能 ID 与施法时英雄等级，因此由技能衍生出的效果只要保留当前技能 Runtime 上下文，就能读取同一来源技能的成长值。

已有 `SkillLevelParameter` 用途保持不变：它通过 `magnitude_type = SkillLevelParameter` 与 `magnitude_parameter` 从本次技能 Rank 参数快照中读取。冷却和消耗仍走技能等级表及原有施法流程。

## 生成与验证

权威 Excel、Luban 定义和导出规则位于 `LubanConfig/Moba/Production/`。运行 `python LubanConfig/Moba/export_pipeline.py` 验证 Excel、表注册和 JSON/二进制导出。常规导出生成到 `local/luban-export`；只有执行发布流程时才将产物同步到 Unity 或 Console 运行时目录。
