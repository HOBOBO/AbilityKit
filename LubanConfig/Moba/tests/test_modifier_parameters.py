"""Contract tests for the published modifier parameter identifiers."""

import copy
import json
import unittest
from pathlib import Path

from modifier_parameters import (DOMAIN_NAMES, EMITTER_LEGACY_IDS, LEGACY_KEYS,
                                 CONSUMER_CONTRACTS, PARAMETER_ID_MIGRATION, PARAMETER_ID_RANGES, generate_parameter_keys,
                                 validate_consumer_contracts, validate_modifier_parameters,
                                 validate_modifier_template_ids)
from business.modifiers import Rule as ModifierRule, project_modifier_bindings
from relations import RelationGraph


CATALOG = (Path(__file__).resolve().parents[3] /
           "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/luban/moba/modifier_parameters.json")
TEMPLATES = CATALOG.with_name("modifier_templates.json")


class ModifierParameterCatalogTests(unittest.TestCase):
    def setUp(self):
        self.rows = json.loads(CATALOG.read_text(encoding="utf-8"))

    def test_published_catalog_generates_all_runtime_keys(self):
        validate_modifier_parameters(self.rows)
        validate_consumer_contracts(self.rows)
        generated = generate_parameter_keys(self.rows)
        for row in self.rows:
            self.assertIn(f"case {row['Id']}: key =", generated)
            self.assertIn(f'case {row["Id"]}: typeName = ', generated)

    def test_unregistered_or_wrong_stage_consumer_is_rejected(self):
        rows = copy.deepcopy(self.rows)
        rows.append(dict(rows[-1], Id=70002, Key="Unregistered", Category=110, SubCategory=1))
        with self.assertRaisesRegex(ValueError, "no matching consumer contract"):
            validate_consumer_contracts(rows)
        rows = copy.deepcopy(self.rows)
        rows[0]["ResolveAt"] = 7
        with self.assertRaisesRegex(ValueError, "no matching consumer contract"):
            validate_consumer_contracts(rows)

    def test_duplicate_or_invalid_consumer_type_is_rejected(self):
        original = CONSUMER_CONTRACTS[40002]
        try:
            CONSUMER_CONTRACTS[40002] = (*original[:3], "MobaProjectileService,MobaProjectileService")
            with self.assertRaisesRegex(ValueError, "invalid consumer type names"):
                validate_consumer_contracts(self.rows)
            CONSUMER_CONTRACTS[40002] = (*original[:3], "MobaProjectileService,")
            with self.assertRaisesRegex(ValueError, "invalid consumer type names"):
                validate_consumer_contracts(self.rows)
        finally:
            CONSUMER_CONTRACTS[40002] = original

    def test_duplicate_id_and_key_are_rejected(self):
        rows = copy.deepcopy(self.rows)
        rows.append(copy.deepcopy(rows[0]))
        with self.assertRaisesRegex(ValueError, "duplicate or invalid ID"):
            validate_modifier_parameters(rows)
        rows[-1]["Id"] = 19999
        with self.assertRaisesRegex(ValueError, "duplicate name"):
            validate_modifier_parameters(rows)

    def test_new_ids_must_use_their_domain_allocation(self):
        for domain, (lower, upper) in PARAMETER_ID_RANGES.items():
            rows = copy.deepcopy(self.rows)
            candidate = copy.deepcopy(rows[0])
            candidate.update(Id=upper, Domain=domain, Key=f"NewKey{domain}",
                             Category=100 + domain, SubCategory=1)
            rows.append(candidate)
            validate_modifier_parameters(rows)
            candidate["Id"] = lower - 1
            with self.assertRaisesRegex(ValueError, "ID outside"):
                validate_modifier_parameters(rows)
        rows = copy.deepcopy(self.rows)
        rows.append(dict(rows[0], Id=17, Key="OldGap", Category=120, SubCategory=1))
        with self.assertRaisesRegex(ValueError, "ID outside"):
            validate_modifier_parameters(rows)

    def test_reference_id_cannot_use_arithmetic_or_trigger_action(self):
        rows = copy.deepcopy(self.rows)
        rows[0]["OpMask"] = 15
        with self.assertRaisesRegex(ValueError, "invalid operation mask"):
            validate_modifier_parameters(rows)
        rows[0]["OpMask"] = 4
        rows[0]["TriggerWritable"] = True
        with self.assertRaisesRegex(ValueError, "invalid trigger permission"):
            validate_modifier_parameters(rows)

    def test_published_abi_and_migrated_key_cannot_drift(self):
        rows = copy.deepcopy(self.rows)
        rows[0]["Key"] = "OtherLauncherId"
        with self.assertRaisesRegex(ValueError, "migrated key ABI"):
            validate_modifier_parameters(rows)
        rows = copy.deepcopy(self.rows)
        rows.append(dict(rows[-1], Id=70002, Key="NewRequestedBase", Category=110, SubCategory=1))
        published = copy.deepcopy(rows)
        rows[-1]["ValueKind"] = 2
        with self.assertRaisesRegex(ValueError, "published parameter ABI"):
            validate_modifier_parameters(rows, published=published)

    def test_previous_published_catalog_is_accepted_only_for_audited_migration(self):
        migrated = {row["Id"]: row for row in self.rows}
        previous = []
        for old_id, new_id in PARAMETER_ID_MIGRATION.items():
            row = dict(migrated[new_id], Id=old_id)
            if old_id in EMITTER_LEGACY_IDS:
                row["Domain"] = 2
            previous.append(row)
            expected = LEGACY_KEYS[old_id]
            self.assertEqual((DOMAIN_NAMES[row["Domain"]], row["Key"], row["Category"],
                              row["SubCategory"], row["ValueKind"]), expected)
        validate_modifier_parameters(self.rows, published=previous)
        previous[0]["Key"] = "Changed"
        with self.assertRaisesRegex(ValueError, "published parameter ABI"):
            validate_modifier_parameters(self.rows, published=previous)

    def test_modifier_definition_ids_follow_target_domains(self):
        templates = json.loads(TEMPLATES.read_text(encoding="utf-8"))
        validate_modifier_template_ids(templates, self.rows)
        wrong = copy.deepcopy(templates)
        wrong[0]["Id"] = 200001
        with self.assertRaisesRegex(ValueError, "ID must be in"):
            validate_modifier_template_ids(wrong, self.rows)

    def test_base_skill_selector_is_authorable_at_projectile_spawn(self):
        template = copy.deepcopy(json.loads(TEMPLATES.read_text(encoding="utf-8"))[0])
        skill = json.loads(CATALOG.with_name("skills.json").read_text(encoding="utf-8"))[0]
        projectile = json.loads(CATALOG.with_name("projectiles.json").read_text(encoding="utf-8"))[0]
        template.update(TargetKind=4, TargetId=40002, SelectorBaseSkillId=skill["Id"],
                        SelectorProjectileTemplateId=projectile["Id"])
        tables = {"modifier_templates": [template], "modifier_parameters": self.rows,
                  "skills": [skill], "projectiles": [projectile], "buffs": [{"Id": 101}]}
        ModifierRule().validate_graph(RelationGraph(tables), tables, ())
        template["SelectorBuffTemplateId"] = 101
        with self.assertRaisesRegex(ValueError, "unavailable"):
            ModifierRule().validate_graph(RelationGraph(tables), tables, ())

    def test_projectile_lineage_selector_is_authorable_for_area_and_buff(self):
        template = copy.deepcopy(json.loads(TEMPLATES.read_text(encoding="utf-8"))[0])
        projectile = json.loads(CATALOG.with_name("projectiles.json").read_text(encoding="utf-8"))[0]
        area = json.loads(CATALOG.with_name("aoes.json").read_text(encoding="utf-8"))[0]
        buff = json.loads(CATALOG.with_name("buffs.json").read_text(encoding="utf-8"))[0]
        tables = {"modifier_templates": [template], "modifier_parameters": self.rows,
                  "projectiles": [projectile], "aoes": [area], "buffs": [buff]}
        template.update(TargetKind=4, TargetId=50001,
                        SelectorProjectileTemplateId=projectile["Id"],
                        SelectorAreaTemplateId=area["Id"])
        ModifierRule().validate_graph(RelationGraph(tables), tables, ())
        template.update(TargetId=20001, SelectorAreaTemplateId=0,
                        SelectorBuffTemplateId=buff["Id"])
        ModifierRule().validate_graph(RelationGraph(tables), tables, ())

    def test_continuous_process_keeps_curve_fields_when_expanded(self):
        template = copy.deepcopy(json.loads(TEMPLATES.read_text(encoding="utf-8"))[19])
        template.update(Kind=1, MagnitudeSourceType=3, MagnitudeDuration=2.0,
                        MagnitudeCurve=[1.0, 0.5, 0.0])
        tables = {"modifier_templates": [template], "buffs": [],
                  "continuous_processes": [{"Id": 1, "ModifierIds": [template["Id"]],
                                            "TagIds": []}]}
        project_modifier_bindings(tables, lambda *args, **kwargs: [])
        expanded = tables["continuous_processes"][0]["Modifiers"][0]
        self.assertEqual(2.0, expanded["MagnitudeDuration"])
        self.assertEqual([1.0, 0.5, 0.0], expanded["MagnitudeCurve"])


if __name__ == "__main__":
    unittest.main()
