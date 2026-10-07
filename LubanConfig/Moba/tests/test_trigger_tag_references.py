import json
import tempfile
import unittest
from pathlib import Path

from business.tags import Rule, stable_action_id, stable_string_id, validate_trigger_tag_references


class TriggerTagReferenceTests(unittest.TestCase):
    def setUp(self):
        self.tags = [{"Id": 1006, "Namespace": 1, "Status": 0},
                     {"Id": 1200, "Namespace": 1, "Status": 1}]
        self.action = {"ActionId": stable_action_id("remove_buff"), "Args": {
            "tag_id": {"Kind": "Const", "ConstValue": 1006.0}}}
        self.triggers = [{"TriggerId": 1, "Actions": [self.action]}]

    def test_active_tag_reference_passes(self):
        validate_trigger_tag_references(self.triggers, self.tags)

    def test_missing_planned_and_dynamic_tag_references_fail(self):
        for arg in ({"Kind": "Const", "ConstValue": 999.0},
                    {"Kind": "Const", "ConstValue": 1200.0},
                    {"Kind": "Variable", "ConstValue": 1006.0}):
            with self.subTest(arg=arg), self.assertRaisesRegex(ValueError, "tag_id"):
                self.action["Args"]["tag_id"] = arg
                validate_trigger_tag_references(self.triggers, self.tags)

    def test_legacy_remove_slow_fails(self):
        self.action["Args"] = {"remove_slow": {"Kind": "Const", "ConstValue": 1.0}}
        with self.assertRaisesRegex(ValueError, "retired"):
            validate_trigger_tag_references(self.triggers, self.tags)

    def test_source_target_rule_groups_require_existing_constant_ids(self):
        node = {"Kind": "Function", "FunctionId": stable_string_id("predicate:source_target_tag_rules"),
                "FunctionArity": 2, "Left": {"Kind": "Const", "ConstValue": 2001},
                "Right": {"Kind": "Const", "ConstValue": 0}}
        self.triggers[0]["Predicate"] = {"Nodes": [node]}
        validate_trigger_tag_references(self.triggers, self.tags, [{"Id": 2001}])
        for field, value in (("Left", {"Kind": "Const", "ConstValue": 999}),
                             ("Left", {"Kind": "Variable", "ConstValue": 2001}),
                             ("Right", {"Kind": "Const", "ConstValue": -1})):
            with self.subTest(field=field, value=value), self.assertRaisesRegex(ValueError, "rule group"):
                previous = node[field]
                node[field] = value
                try:
                    validate_trigger_tag_references(self.triggers, self.tags, [{"Id": 2001}])
                finally:
                    node[field] = previous

    def test_usage_report_includes_pipeline_and_trigger_template_consumers(self):
        self.triggers[0]["Actions"].append({"ActionId": 123, "Args": {
            "continuous_tag_template_id": {"Kind": "Const", "ConstValue": 7}}})
        self.triggers[0]["Predicate"] = {"Nodes": [{
            "Kind": "Function", "FunctionId": stable_string_id("predicate:source_target_tag_rules"),
            "FunctionArity": 2, "Left": {"Kind": "Const", "ConstValue": 2001},
            "Right": {"Kind": "Const", "ConstValue": 0}}]}
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "tag-usage-report.json"
            path.write_text(json.dumps({"templateConsumers": []}), encoding="utf-8")
            Rule().validate_graph(None,
                                  {"tag_definitions": self.tags, "tag_rule_groups": [{"Id": 2001}],
                                   "continuous_tag_templates": [{"Id": 7}]},
                                  {"triggers": self.triggers,
                                   "skill_flows": [{"Id": 9, "PipelineContinuousTagTemplateId": 7}],
                                   "tag_report_path": path})
            consumers = json.loads(path.read_text(encoding="utf-8"))["templateConsumers"]
            self.assertEqual(["skill_flows", "triggers"], [row["table"] for row in consumers])
            report = json.loads(path.read_text(encoding="utf-8"))
            self.assertEqual([{"table": "triggers", "id": 1, "actor": "source", "ruleGroupId": 2001}],
                             report["ruleConsumers"])


if __name__ == "__main__":
    unittest.main()
