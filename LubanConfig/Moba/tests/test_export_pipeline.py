"""Reference validation checks for the production MOBA export."""

import unittest
import json
import tempfile
from pathlib import Path
from unittest.mock import patch

import export_pipeline
from export_pipeline import (ATTRIBUTE_KEYS, expand_references, validate_attribute_types,
                             validate_reference_graph, validate_skill_level_tables,
                             validate_table_inventory)


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
        self.tables["modifier_templates"] = [{"Id": 1, "TargetKind": 1,
                                               "TargetId": 999,
                                               "MagnitudeAttributeTypeId": 0}]
        with self.assertRaisesRegex(ValueError, "modifier_templates/1: TargetId"):
            self.validate()

    def test_attribute_modifier_requires_canonical_target_id(self):
        self.tables["modifier_templates"] = [{"Id": 1, "TargetKind": 1,
                                               "TargetId": 0,
                                               "MagnitudeAttributeTypeId": 0}]
        with self.assertRaisesRegex(ValueError, "attribute target requires"):
            self.validate()

    def test_attribute_source_requires_snapshot_and_valid_role(self):
        self.tables["attr_types"] = [{"Id": 1}]
        modifier = {"Id": 1, "TargetKind": 1, "TargetId": 1,
                    "MagnitudeSourceType": 2, "MagnitudeAttributeTypeId": 1,
                    "EvaluationPolicy": 1, "AttributeSourceRole": 1}
        self.tables["modifier_templates"] = [modifier]
        self.validate()
        modifier["EvaluationPolicy"] = 0
        with self.assertRaisesRegex(ValueError, "requires OnApplySnapshot"):
            self.validate()
        modifier["EvaluationPolicy"] = 1
        modifier["AttributeSourceRole"] = 3
        with self.assertRaisesRegex(ValueError, "invalid AttributeSourceRole"):
            self.validate()
        modifier["AttributeSourceRole"] = 2
        modifier["MagnitudeAttributeTypeId"] = 0
        with self.assertRaisesRegex(ValueError, "MagnitudeAttributeTypeId"):
            self.validate()

    def test_modifier_parameter_reference_and_operation_are_checked(self):
        self.tables["modifier_templates"] = [{"Id": 2, "TargetKind": 4,
                                               "TargetId": 20001, "Op": 0,
                                               "MagnitudeAttributeTypeId": 0}]
        with self.assertRaisesRegex(ValueError, "modifier_templates/2: TargetId"):
            self.validate()
        self.tables["modifier_parameters"] = [{"Id": 20001, "Domain": 4, "Key": "DurationMs",
                                                "ResolveAt": 3, "ValueKind": 3, "OpMask": 4}]
        with self.assertRaisesRegex(ValueError, "operation is not allowed"):
            self.validate()
        self.tables["modifier_templates"][0]["Op"] = -1
        with self.assertRaisesRegex(ValueError, "invalid modifier operation"):
            self.validate()
        self.tables["modifier_templates"][0]["Op"] = 2
        self.validate()

    def test_buff_template_selector_requires_buff_apply_record(self):
        self.tables["buffs"] = [{"Id": 30}]
        parameter = {"Id": 20001, "Domain": 4, "Key": "DurationMs",
                     "ResolveAt": 3, "ValueKind": 2, "OpMask": 4}
        modifier = {"Id": 2, "TargetKind": 4, "TargetId": 20001, "Op": 2,
                    "MagnitudeAttributeTypeId": 0, "SelectorBuffTemplateId": 30}
        self.tables["modifier_parameters"] = [parameter]
        self.tables["modifier_templates"] = [modifier]
        self.validate()
        modifier["SelectorBuffTemplateId"] = 999
        with self.assertRaisesRegex(ValueError, "SelectorBuffTemplateId"):
            self.validate()
        modifier["SelectorBuffTemplateId"] = 30
        parameter["ResolveAt"] = 1
        with self.assertRaisesRegex(ValueError, "selector fields .* unavailable"):
            self.validate()

    def test_attribute_definition_matches_runtime_ids(self):
        rows = [{"Id": identity, "Key": key, "ValueKind": 0}
                for identity, key in enumerate(ATTRIBUTE_KEYS, 1)]
        validate_attribute_types(rows)
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


if __name__ == "__main__":
    unittest.main()
