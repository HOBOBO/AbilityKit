"""Reference validation checks for the production MOBA export."""

import unittest
import json
import tempfile
from pathlib import Path
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[3]

import export_pipeline
from export_pipeline import (ATTRIBUTE_KEYS, expand_references, validate_attribute_types,
                             validate_reference_graph, validate_skill_level_tables,
                             validate_table_inventory)
from business.skills import validate_character_skill_preview_vfx


class ReferenceValidationTests(unittest.TestCase):
    def setUp(self):
        names = (
            "characters", "attribute_templates", "skills", "passive_skills", "buffs",
            "continuous_processes", "projectiles", "aoes", "emitters", "summons",
            "spawn_summon_action_templates", "presentation_templates", "motion_groups",
            "gameplays", "brains", "battle_start", "skill_level_tables",
            "skill_button_templates", "search_query_templates", "continuous_tag_templates",
            "models", "vfx", "component_templates", "summon_attr_inherits",
            "attr_types", "modifier_templates", "modifier_parameters",
            "battle_maps", "dungeons",
        )
        self.tables = {name: [] for name in names}
        self.tables["characters"] = [{"Id": 1, "AttributeTemplateId": 1,
                                       "SkillIds": [10], "PassiveSkillIds": []}]
        self.tables["attribute_templates"] = [{"Id": 1, "BasicAttackSkillId": 10,
                                                "ActiveSkills": [10], "PassiveSkills": []}]
        self.tables["skills"] = [{"Id": 10, "CastFlowId": 10}]
        self.flows = [{"Id": 10, "PipelineContinuousTagTemplateId": 0}]
        self.triggers = [{"TriggerId": 20}]
        self.rules = [{"id": 30}]

    def validate(self):
        for modifier in self.tables["modifier_templates"]:
            for field in ("SelectorSlot", "SelectorBaseSkillId", "SelectorEffectiveSkillId",
                          "SelectorLauncherTemplateId", "SelectorProjectileTemplateId",
                          "SelectorAreaTemplateId", "SelectorSummonTemplateId",
                          "SelectorBuffTemplateId",
                          "CandidateSkillId", "CandidateReferenceId"):
                modifier.setdefault(field, 0)
        validate_reference_graph(self.tables, self.flows, self.triggers, self.rules)

    def test_valid_graph(self):
        self.validate()

    def test_character_active_skills_require_valid_press_preview_vfx(self):
        characters = [{"Id": 1, "SkillIds": [10]}]
        skills = [{"Id": 10, "PreviewVfxId": 9001}]
        vfx = [{"Id": 9001}]
        validate_character_skill_preview_vfx(characters, skills, vfx)
        with self.assertRaisesRegex(ValueError, "PreviewVfxId"):
            validate_character_skill_preview_vfx(characters, [{"Id": 10}], vfx)
        with self.assertRaisesRegex(ValueError, "missing vfx 9002"):
            validate_character_skill_preview_vfx(characters, [{"Id": 10, "PreviewVfxId": 9002}], vfx)

    def test_missing_skill_level_is_rejected(self):
        self.tables["skills"][0]["LevelTableId"] = 999
        with self.assertRaisesRegex(ValueError, "skills/10: LevelTableId"):
            self.validate()

    def test_missing_buff_trigger_is_rejected(self):
        self.tables["buffs"] = [{"Id": 30, "OnAddEffects": [999]}]
        with self.assertRaisesRegex(ValueError, "buffs/30: OnAddEffects"):
            self.validate()

    def test_emitter_uses_presentation_template_namespace(self):
        self.tables["emitters"] = [{"Id": 40, "TemplateId": 10}]
        with self.assertRaisesRegex(ValueError, "emitters/40: TemplateId"):
            self.validate()
        self.tables["presentation_templates"] = [{"Id": 10}]
        self.validate()

    def test_missing_pipeline_tag_template_is_rejected(self):
        self.flows[0]["PipelineContinuousTagTemplateId"] = 999
        with self.assertRaisesRegex(ValueError, "skill_flows/10: PipelineContinuousTagTemplateId"):
            self.validate()

    def test_trigger_motion_tag_references_and_conflict_are_checked(self):
        self.tables["continuous_tag_templates"] = [{"Id": 3015}]
        self.tables["continuous_processes"] = [{"Id": 41}]
        args = {"continuous_tag_template_id": {"Kind": "Const", "ConstValue": 3015}}
        self.triggers[0]["Actions"] = [{"ActionId": 1, "Args": args}]
        self.validate()
        args["continuous_process_id"] = {"Kind": "Const", "ConstValue": 41}
        with self.assertRaisesRegex(ValueError, "cannot both be set"):
            self.validate()
        del args["continuous_tag_template_id"]
        self.validate()
        args["continuous_process_id"]["ConstValue"] = 999
        with self.assertRaisesRegex(ValueError, "references a missing ID"):
            self.validate()

    def test_duplicate_flow_id_is_rejected(self):
        self.flows.append({"Id": 10})
        with self.assertRaisesRegex(ValueError, "skill_flows: invalid or duplicate ID"):
            self.validate()

    def test_nested_phase_references_are_checked(self):
        self.flows[0]["Phases"] = [{"Children": [{"RulePlan": {"TriggerIds": [999]}}]}]
        with self.assertRaisesRegex(ValueError, "Children/0/RulePlan: TriggerIds"):
            self.validate()
        self.flows[0]["Phases"] = [{"Repeat": {"Phase": {"Timeline": {
            "Events": [{"EffectId": 999}]}}}}]
        with self.assertRaisesRegex(ValueError, "Repeat/Phase/Timeline/Events/0: EffectId"):
            self.validate()

    def test_rule_and_timeline_use_distinct_namespaces(self):
        self.flows[0]["Phases"] = [
            {"RulePlan": {"TriggerIds": [30]}},
            {"Timeline": {"Events": [{"EffectId": 20}]}},
        ]
        self.validate()
        self.flows[0]["Phases"][0]["RulePlan"]["TriggerIds"] = [20]
        with self.assertRaisesRegex(ValueError, "RulePlan: TriggerIds references missing rules"):
            self.validate()

    def test_window_trigger_reference_is_checked(self):
        self.flows[0]["Phases"] = [{"Window": {"OpenTriggerIds": [999]}}]
        with self.assertRaisesRegex(ValueError, "Window: OpenTriggerIds"):
            self.validate()

    def test_modifier_attribute_id_is_checked(self):
        self.tables["modifier_templates"] = [{"Id": 1, "Target": {"Kind": 1, "Id": 999},
                                               "Magnitude": {"AttributeTypeId": 0}}]
        with self.assertRaisesRegex(ValueError, "modifier_templates/1: TargetId"):
            self.validate()

    def test_attribute_modifier_requires_canonical_target_id(self):
        self.tables["modifier_templates"] = [{"Id": 1, "Target": {"Kind": 1, "Id": 0},
                                               "Magnitude": {"AttributeTypeId": 0}}]
        with self.assertRaisesRegex(ValueError, "attribute target requires"):
            self.validate()

    def test_attribute_source_requires_valid_role(self):
        self.tables["attr_types"] = [{"Id": 1}]
        modifier = {"Id": 1, "Target": {"Kind": 1, "Id": 1},
                    "Magnitude": {"SourceType": 2, "AttributeTypeId": 1},
                    "AttributeSourceRole": 1}
        self.tables["modifier_templates"] = [modifier]
        self.validate()
        modifier["AttributeSourceRole"] = 3
        with self.assertRaisesRegex(ValueError, "invalid AttributeSourceRole"):
            self.validate()
        modifier["AttributeSourceRole"] = 2
        modifier["Magnitude"]["AttributeTypeId"] = 0
        with self.assertRaisesRegex(ValueError, "MagnitudeAttributeTypeId"):
            self.validate()

    def test_modifier_parameter_reference_and_operation_are_checked(self):
        self.tables["modifier_templates"] = [{"Id": 2, "Target": {"Kind": 4, "Id": 20001},
                                               "Modifier": {"Op": 0},
                                               "Magnitude": {"AttributeTypeId": 0}}]
        with self.assertRaisesRegex(ValueError, "modifier_templates/2: TargetId"):
            self.validate()
        self.tables["modifier_parameters"] = [{"Id": 20001, "Key": "DurationMs",
                                                "ResolveAt": 3, "ValueKind": 3, "Ops": [2]}]
        with self.assertRaisesRegex(ValueError, "operation is not allowed"):
            self.validate()
        self.tables["modifier_templates"][0]["Modifier"]["Op"] = -1
        with self.assertRaisesRegex(ValueError, "invalid modifier operation"):
            self.validate()
        self.tables["modifier_templates"][0]["Modifier"]["Op"] = 2
        self.validate()

    def test_buff_template_selector_requires_buff_apply_record(self):
        self.tables["buffs"] = [{"Id": 30}]
        parameter = {"Id": 20001, "Key": "DurationMs",
                     "ResolveAt": 3, "ValueKind": 2, "Ops": [2]}
        modifier = {"Id": 2, "Target": {"Kind": 4, "Id": 20001},
                    "Modifier": {"Op": 2}, "Magnitude": {"AttributeTypeId": 0},
                    "Selectors": "BuffTemplateId|30"}
        self.tables["modifier_parameters"] = [parameter]
        self.tables["modifier_templates"] = [modifier]
        self.validate()
        modifier["Selectors"] = "BuffTemplateId|999"
        with self.assertRaisesRegex(ValueError, "SelectorBuffTemplateId"):
            self.validate()
        modifier["Selectors"] = "BuffTemplateId|30"
        parameter["ResolveAt"] = 1
        with self.assertRaisesRegex(ValueError, "selector fields .* unavailable"):
            self.validate()

    def test_attribute_definition_matches_runtime_ids(self):
        catalog = json.loads((ROOT / "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/luban/moba/attr_types.json").read_text(encoding="utf-8-sig"))
        validate_attribute_types(catalog)
        rows = [dict(row) for row in catalog]
        rows[0]["Key"] = "WRONG"
        with self.assertRaisesRegex(ValueError, "attr_types/1: Key"):
            validate_attribute_types(rows)

    def test_skill_tag_must_use_skill_selection_namespace(self):
        tables = {name: [] for name in ("characters", "attribute_templates", "modifier_templates",
                                       "tag_definitions", "skills", "summons", "gameplays",
                                       "buffs", "continuous_processes",
                                       "continuous_tag_templates")}
        tables["tag_definitions"] = [{"Id": 1000, "Name": "State", "Namespace": 1, "Kind": 0, "Status": 0}]
        tables["skills"] = [{"Id": 10, "Tags": [1000]}]
        with self.assertRaisesRegex(ValueError, "skills/10: tag 1000 belongs to another namespace"):
            expand_references(tables)

    def test_unused_summon_tags_are_rejected(self):
        tables = {name: [] for name in ("characters", "attribute_templates", "modifier_templates",
                                       "tag_definitions", "skills", "summons", "gameplays",
                                       "buffs", "continuous_processes", "continuous_tag_templates")}
        tables["summons"] = [{"Id": 1, "Tags": [1]}]
        with self.assertRaisesRegex(ValueError, "summons/1: unused Tags"):
            expand_references(tables)

    def test_shared_attribute_template_requires_same_character_loadout(self):
        tables = {name: [] for name in ("characters", "attribute_templates", "modifier_templates",
                                       "tag_definitions", "skills", "summons", "gameplays",
                                       "buffs", "continuous_processes",
                                       "continuous_tag_templates")}
        tables["characters"] = [
            {"Id": 1, "AttributeTemplateId": 1, "SkillIds": [10], "PassiveSkillIds": []},
            {"Id": 2, "AttributeTemplateId": 1, "SkillIds": [11], "PassiveSkillIds": []},
        ]
        with self.assertRaisesRegex(ValueError, "characters disagree on skill loadout"):
            expand_references(tables)

    def test_skill_level_parameter_width_and_names_are_checked(self):
        row = {"Id": 10, "ParamNames": ["damage", "radius"],
               "Levels": [{"Params": [100, 3]}, {"Params": [120, 4]}]}
        validate_skill_level_tables([row])
        row["Levels"][1]["Params"].pop()
        with self.assertRaisesRegex(ValueError, "Levels/2: Params has 1 values"):
            validate_skill_level_tables([row])
        row["Levels"][1]["Params"].append(4)
        row["ParamNames"][1] = "damage"
        with self.assertRaisesRegex(ValueError, "ParamNames contains duplicates"):
            validate_skill_level_tables([row])
        row["ParamNames"][1] = ""
        with self.assertRaisesRegex(ValueError, "ParamNames must be nonempty identifiers"):
            validate_skill_level_tables([row])


class TableInventoryTests(unittest.TestCase):
    def test_exported_json_and_binary_must_match_registered_tables(self):
        with tempfile.TemporaryDirectory() as directory:
            stage = Path(directory)
            for folder in ("json", "bin"):
                (stage / folder).mkdir()
            (stage / "json/skills.json").write_text("[]", encoding="utf-8")
            (stage / "bin/skills.bytes").write_bytes(b"binary")
            validate_table_inventory({"skills": {}}, stage, {}, {})
            (stage / "json/unregistered.json").write_text("[]", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "unexpected=.*unregistered"):
                validate_table_inventory({"skills": {}}, stage, {}, {})
            (stage / "json/unregistered.json").unlink()
            with self.assertRaisesRegex(ValueError, "unknown tables"):
                validate_table_inventory({"skills": {}}, stage, {"stale": "Id"}, {})


class DungeonRuleTests(unittest.TestCase):
    """副本表的引用与输入校验。"""

    def _row(self, **overrides):
        row = {"Id": 1, "GameplayId": 1, "FlowProfileId": "moba.standard",
               "MinPlayerCount": 1, "MaxPlayerCount": 10,
               "AllowedHostModes": ["Local"], "GameplayHandlerId": "team_deathmatch",
               "OutcomeRuleId": "team_deathmatch", "RequiredPortGroups": ["deathmatch"]}
        row.update(overrides)
        return row

    def _context(self, rows):
        from pipeline_rules import ExportContext
        return ExportContext({"dungeons": rows}, Path("."), ())

    def test_dungeon_references_gameplay_and_map(self):
        from business.dungeons import Rule
        mappings, required = Rule().references()
        self.assertEqual(mappings["dungeons"],
                         {"GameplayId": "gameplays"})
        self.assertEqual(required, set())

    def test_unknown_host_mode_is_rejected(self):
        from business.dungeons import Rule
        with self.assertRaisesRegex(ValueError, "unknown host mode"):
            Rule().normalize("dungeons", self._row(AllowedHostModes=["Cloud"]))

    def test_blank_extension_id_becomes_none(self):
        from business.dungeons import Rule
        row = Rule().normalize("dungeons", self._row(GameplayHandlerId="  "))
        self.assertIsNone(row["GameplayHandlerId"])

    def test_min_player_count_above_max_is_rejected(self):
        from business.dungeons import Rule
        with self.assertRaisesRegex(ValueError, "exceeds MaxPlayerCount"):
            Rule().normalize("dungeons", self._row(MinPlayerCount=5, MaxPlayerCount=2))

    def test_other_tables_pass_through_untouched(self):
        from business.dungeons import Rule
        row = self._row()
        self.assertIs(Rule().normalize("gameplays", row), row)

    def test_validate_requires_flow_profile_handler_and_outcome(self):
        from business.dungeons import Rule
        for field in ("FlowProfileId", "GameplayHandlerId", "OutcomeRuleId"):
            with self.assertRaisesRegex(ValueError, field):
                Rule().validate(self._context([self._row(**{field: None})]))

    def test_validate_rejects_empty_and_duplicate_tables(self):
        from business.dungeons import Rule
        with self.assertRaisesRegex(ValueError, "table is empty"):
            Rule().validate(self._context([]))
        with self.assertRaisesRegex(ValueError, "duplicate ID"):
            Rule().validate(self._context([self._row(), self._row()]))

    def test_validate_accepts_well_formed_dungeons(self):
        from business.dungeons import Rule
        Rule().validate(self._context([self._row(), self._row(Id=2, MinPlayerCount=2)]))


if __name__ == "__main__":
    unittest.main()
