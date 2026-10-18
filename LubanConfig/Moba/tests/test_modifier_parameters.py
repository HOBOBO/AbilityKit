"""Contract tests for the published modifier parameter identifiers."""

import copy
import json
import unittest
from pathlib import Path

from modifier_parameters import (DOMAIN_NAMES, derive_operation_mask,
                                 domain_name_for_parameter_id, EMITTER_LEGACY_IDS, LEGACY_KEYS,
                                 CONSUMER_CONTRACTS, PARAMETER_ID_MIGRATION, PARAMETER_ID_RANGES, generate_parameter_keys,
                                 validate_consumer_contracts, validate_modifier_parameters,
                                 validate_modifier_template_ids)
from business.actors import validate_attribute_types
from business.modifiers import (Rule as ModifierRule, bind_template_definitions,
                                project_modifier_bindings, validate_conflict_group_keys,
                                validate_modifier_definitions)
from relations import RelationGraph


CATALOG = (Path(__file__).resolve().parents[3] /
           "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/luban/moba/modifier_parameters.json")
TEMPLATES = CATALOG.with_name("modifier_templates.json")
DEFINITIONS = CATALOG.with_name("modifier_definitions.json")


class ModifierParameterCatalogTests(unittest.TestCase):
    def setUp(self):
        self.rows = json.loads(CATALOG.read_text(encoding="utf-8"))
        self.templates = json.loads(TEMPLATES.read_text(encoding="utf-8"))
        self.definitions = json.loads(DEFINITIONS.read_text(encoding="utf-8"))

    def bind(self, template):
        """Return the published mount row merged with its definition's semantics."""
        return bind_template_definitions([template], self.definitions)[0]

    def definition_for(self, template_id):
        """Return a definition-shaped row matching a published template, for graph tests."""
        template = next(row for row in self.templates if row["Id"] == template_id)
        return {
            "Id": template["DefinitionId"], "Key": template["DefinitionKey"],
            "MountPoint": template["MountPoint"],
            "Target": {"Kind": template["TargetKind"], "Id": template["TargetId"]},
            "Value": {"Op": template["Op"], "Value": template["Value"],
                      "Priority": template["Priority"]},
            "Magnitude": {"SourceType": template["MagnitudeSourceType"],
                          "BaseValue": template["MagnitudeBaseValue"],
                          "Coefficient": template["MagnitudeCoefficient"],
                          "DurationSec": template["MagnitudeDuration"],
                          "DecayType": template["MagnitudeDecayType"],
                          "AttributeTypeId": template["MagnitudeAttributeTypeId"]},
            "Curve": template["MagnitudeCurve"], "ContextKey": template["MagnitudeContextKey"],
            "EvaluationPolicy": template["EvaluationPolicy"],
            "AttributeSourceRole": template["AttributeSourceRole"],
            "Candidate": {"SkillId": template["CandidateSkillId"],
                          "ReferenceId": template["CandidateReferenceId"]},
            "FormulaConstant": template["FormulaConstant"],
            "FormulaLevelCoefficient": template["FormulaLevelCoefficient"],
            "FormulaTerms": template["FormulaTerms"],
            "StackingPolicy": template["StackingPolicy"], "MaxStacks": template["MaxStacks"],
            "ConflictGroup": template["ConflictGroup"], "ConditionSetId": 0, "Deprecated": False,
        }

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

    def test_capture_policy_must_match_consumer_timing(self):
        rows = copy.deepcopy(self.rows)
        rows[1]["CapturePolicy"] = 2
        with self.assertRaisesRegex(ValueError, "does not match consumer timing"):
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
            candidate = copy.deepcopy(next(r for r in rows if r["ValueKind"] != 1))
            candidate.update(Id=upper, Key=f"NewKey{domain}", Category=100 + domain, SubCategory=1,
                             Ops=[0], Unit=1, ValueMin=0, ValueMax=0)
            rows.append(candidate)
            validate_modifier_parameters(rows)
        # An ID between two segments belongs to the lower segment, so it stays legal.
        rows = copy.deepcopy(self.rows)
        candidate = copy.deepcopy(next(r for r in rows if r["ValueKind"] != 1))
        candidate.update(Id=19999, Key="SkillTop", Category=119, SubCategory=9,
                         Ops=[0], Unit=1, ValueMin=0, ValueMax=0)
        rows.append(candidate)
        validate_modifier_parameters(rows)
        for stale in (17, 9999):
            rows = copy.deepcopy(self.rows)
            rows.append(dict(next(r for r in rows if r["ValueKind"] != 1), Id=stale, Key=f"Stale{stale}", Category=120, SubCategory=9,
                             Ops=[0], Unit=1, ValueMin=0, ValueMax=0))
            with self.assertRaisesRegex(ValueError, "unallocated ID segment"):
                validate_modifier_parameters(rows)

    def test_reference_id_cannot_use_arithmetic_or_trigger_action(self):
        rows = copy.deepcopy(self.rows)
        reference = rows[0]
        reference["Ops"] = [0, 1, 3]
        with self.assertRaisesRegex(ValueError, "reference fields only allow Override"):
            validate_modifier_parameters(rows)
        reference["Ops"] = [2]
        reference["TriggerWritable"] = True
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
            # Legacy IDs 1-16 no longer fall in an allocated segment; the domain they
            # belonged to is only recoverable from the audited migration table.
            self.assertEqual((expected[0], row["Key"], row["Category"],
                              row["SubCategory"], row["ValueKind"]), expected)
        validate_modifier_parameters(self.rows, published=previous)
        previous[0]["Key"] = "Changed"
        with self.assertRaisesRegex(ValueError, "published parameter ABI"):
            validate_modifier_parameters(self.rows, published=previous)

    def test_modifier_definition_ids_follow_target_domains(self):
        templates = self.templates
        validate_modifier_template_ids(templates, self.rows)
        wrong = copy.deepcopy(templates)
        wrong[0]["Id"] = 200001
        with self.assertRaisesRegex(ValueError, "ID must be in"):
            validate_modifier_template_ids(wrong, self.rows)

    def test_definition_catalog_is_validated(self):
        domains = {row["Id"]: domain_name_for_parameter_id(row["Id"]) for row in self.rows}
        validate_modifier_definitions(self.definitions, domains)
        broken = copy.deepcopy(self.definitions)
        broken[0]["Key"] = "lowercase key"
        with self.assertRaisesRegex(ValueError, "invalid or duplicate Key"):
            validate_modifier_definitions(broken, domains)
        broken = copy.deepcopy(self.definitions)
        broken[0]["ConditionSetId"] = 7
        with self.assertRaisesRegex(ValueError, "ConditionSetId is reserved"):
            validate_modifier_definitions(broken, domains)
        broken = copy.deepcopy(self.definitions)
        broken[0]["MaxStacks"] = 3
        with self.assertRaisesRegex(ValueError, "MaxStacks is only meaningful"):
            validate_modifier_definitions(broken, domains)
        broken = copy.deepcopy(self.definitions)
        broken[0]["MountPoint"] = 6
        with self.assertRaisesRegex(ValueError, "does not match target domain"):
            validate_modifier_definitions(broken, domains)

        broken = copy.deepcopy(self.definitions)
        emitter = next(row for row in broken if 3000001 <= row["Id"] <= 3999999)
        emitter["Target"] = {"Kind": 4, "Id": 30003}
        emitter["Value"]["Value"] = 361
        parameter_schemas = {row["Id"]: row for row in self.rows}
        with self.assertRaisesRegex(ValueError, "outside parameter 30003 range"):
            validate_modifier_definitions(broken, domains, parameter_schemas)

    def test_templates_bind_definitions_by_id(self):
        bound = bind_template_definitions(self.templates, self.definitions)
        self.assertEqual(len(self.templates), len(bound))
        for template in bound:
            self.assertIn("TargetKind", template)
            self.assertIn("DefinitionKey", template)
        broken = copy.deepcopy(self.templates)
        broken[0]["DefinitionId"] = 999999999
        with self.assertRaisesRegex(ValueError, "missing modifier definition"):
            bind_template_definitions(broken, self.definitions)

    def test_evaluation_policy_is_derived_from_magnitude_source(self):
        for template in bind_template_definitions(self.templates, self.definitions):
            source = template["MagnitudeSourceType"]
            expected = 1 if source in (2, 6) else 0
            self.assertEqual(expected, template["EvaluationPolicy"],
                             msg=f"definition {template['DefinitionKey']} source {source}")

    def test_priority_comes_from_the_mount_row(self):
        bound = {row["Id"]: row for row in bind_template_definitions(self.templates, self.definitions)}
        for template in self.templates:
            self.assertEqual(template["Priority"], bound[template["Id"]]["Priority"])

    def test_conflict_group_must_not_straddle_one_modifier_key(self):
        validate_conflict_group_keys(self.definitions)
        broken = copy.deepcopy(self.definitions)
        target = broken[0]["Target"]
        for row in broken:
            if row["Target"] == target and row["Id"] != broken[0]["Id"]:
                row["ConflictGroup"] = 3
                break
        else:
            broken.append(dict(broken[0], Id=9999991, ConflictGroup=3))
        with self.assertRaisesRegex(ValueError, "always compete"):
            validate_conflict_group_keys(broken)

    def test_base_skill_selector_is_authorable_at_projectile_spawn(self):
        template = self.bind(copy.deepcopy(self.templates[0]))
        skill = json.loads(CATALOG.with_name("skills.json").read_text(encoding="utf-8"))[0]
        projectile = json.loads(CATALOG.with_name("projectiles.json").read_text(encoding="utf-8"))[0]
        template["TargetKind"], template["TargetId"] = 4, 40002
        template["Selectors"] = (f"BaseSkillId|{skill['Id']};"
                                  f"ProjectileTemplateId|{projectile['Id']}")
        tables = {"modifier_templates": [template], "modifier_definitions": [], "modifier_parameters": self.rows,
                  "skills": [skill], "projectiles": [projectile], "buffs": [{"Id": 101}],
                  "attr_types": json.loads(CATALOG.with_name("attr_types.json").read_text(encoding="utf-8"))}
        ModifierRule().validate_graph(RelationGraph(tables), tables, ())
        # Bound rows are already flattened, so selectors live in their projected columns.
        template["SelectorBuffTemplateId"] = 101
        with self.assertRaisesRegex(ValueError, "unavailable"):
            ModifierRule().validate_graph(RelationGraph(tables), tables, ())

    def test_projectile_lineage_selector_is_authorable_for_area_and_buff(self):
        template = self.bind(copy.deepcopy(self.templates[0]))
        projectile = json.loads(CATALOG.with_name("projectiles.json").read_text(encoding="utf-8"))[0]
        area = json.loads(CATALOG.with_name("aoes.json").read_text(encoding="utf-8"))[0]
        buff = json.loads(CATALOG.with_name("buffs.json").read_text(encoding="utf-8"))[0]
        tables = {"modifier_templates": [template], "modifier_definitions": [], "modifier_parameters": self.rows,
                  "projectiles": [projectile], "aoes": [area], "buffs": [buff],
                  "attr_types": json.loads(CATALOG.with_name("attr_types.json").read_text(encoding="utf-8"))}
        template["TargetKind"], template["TargetId"] = 4, 50001
        template["Selectors"] = (f"ProjectileTemplateId|{projectile['Id']};"
                                  f"AreaTemplateId|{area['Id']}")
        ModifierRule().validate_graph(RelationGraph(tables), tables, ())
        template["TargetKind"], template["TargetId"] = 4, 20001
        template["Selectors"] = (f"ProjectileTemplateId|{projectile['Id']};"
                                  f"BuffTemplateId|{buff['Id']}")
        ModifierRule().validate_graph(RelationGraph(tables), tables, ())

    def test_continuous_process_keeps_modifier_ids_without_baking_modifiers(self):
        template = self.bind(copy.deepcopy(self.templates[19]))
        template.update(Kind=1)
        template["MagnitudeSourceType"] = 3
        template["MagnitudeDuration"] = 2.0
        template["MagnitudeCurve"] = [1.0, 0.5, 0.0]
        tables = {"modifier_templates": [template], "buffs": [],
                  "continuous_processes": [{"Id": 1, "ModifierIds": [template["Id"]],
                                            "TagIds": []}]}
        project_modifier_bindings(tables, lambda *args, **kwargs: [])
        process = tables["continuous_processes"][0]
        self.assertEqual([template["Id"]], process["ModifierIds"])
        self.assertNotIn("Modifiers", process)

    def test_parameter_schema_rejects_incompatible_declarations(self):
        rows = copy.deepcopy(self.rows)
        reference = next(row for row in rows if row["ValueKind"] == 1)
        reference["Unit"] = 2
        with self.assertRaisesRegex(ValueError, "reference fields must use the Id unit"):
            validate_modifier_parameters(rows)
        rows = copy.deepcopy(self.rows)
        next(row for row in rows if row["ValueKind"] == 1)["ValueMin"] = 1
        with self.assertRaisesRegex(ValueError, "reference fields cannot declare a numeric range"):
            validate_modifier_parameters(rows)
        numeric = lambda rows: next(row for row in rows if row["ValueKind"] != 1)
        rows = copy.deepcopy(self.rows)
        numeric(rows)["Unit"] = 0
        with self.assertRaisesRegex(ValueError, "numeric fields must declare a dimensional unit"):
            validate_modifier_parameters(rows)
        rows = copy.deepcopy(self.rows)
        numeric(rows)["CapturePolicy"] = 3
        # LiveProperty is publishable: the runtime resolves every consumption on demand and caches
        # no computed value, so an already-running Buff observes the change on its next read.
        validate_modifier_parameters(rows)
        rows = copy.deepcopy(self.rows)
        rows[0]["Deprecated"] = True
        with self.assertRaisesRegex(ValueError, "Deprecated fields cannot be published"):
            validate_modifier_parameters(rows)

    def test_parameter_schema_accepts_a_bounded_numeric_range(self):
        rows = copy.deepcopy(self.rows)
        target = next(row for row in rows if row["ValueKind"] != 1)
        target["ValueMin"], target["ValueMax"] = 10, 20
        validate_modifier_parameters(rows)
        target["ValueMin"], target["ValueMax"] = 30, 20
        with self.assertRaisesRegex(ValueError, "ValueMin must not exceed"):
            validate_modifier_parameters(rows)

    def test_unbounded_range_is_expressed_by_zero(self):
        rows = copy.deepcopy(self.rows)
        for row in rows:
            row["ValueMin"] = 0
            row["ValueMax"] = 0
        validate_modifier_parameters(rows)

    def test_operation_mask_is_derived_from_the_ops_list(self):
        self.assertEqual(15, derive_operation_mask([0, 1, 2, 3]))
        self.assertEqual(15, derive_operation_mask("Add,Mul,Override,PercentAdd"))
        self.assertEqual(4, derive_operation_mask([2]))
        self.assertIsNone(derive_operation_mask([]))
        self.assertIsNone(derive_operation_mask([0, 0]))
        self.assertIsNone(derive_operation_mask([9]))
        self.assertIsNone(derive_operation_mask(["Nope"]))

    def test_attribute_catalog_shares_the_modifier_field_schema(self):
        rows = json.loads(CATALOG.with_name("attr_types.json").read_text(encoding="utf-8"))
        validate_attribute_types(rows)
        broken = copy.deepcopy(rows)
        broken[0]["Unit"] = 0
        with self.assertRaisesRegex(ValueError, "dimensional unit"):
            validate_attribute_types(broken)
        broken = copy.deepcopy(rows)
        broken[1]["Ops"] = []
        with self.assertRaisesRegex(ValueError, "at least one distinct known operation"):
            validate_attribute_types(broken)

if __name__ == "__main__":
    unittest.main()
