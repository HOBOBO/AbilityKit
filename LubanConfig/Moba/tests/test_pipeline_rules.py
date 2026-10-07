"""The business rule contract is deterministic and rejects ambiguous output."""

import json
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

from pipeline_rules import ExportContext, RulePipeline


class RulePipelineTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.stage = Path(self.temp.name)
        (self.stage / "code").mkdir()
        self.manifest = self.stage / "rules.json"

    def pipeline(self, names, modules):
        self.manifest.write_text(json.dumps({"rules": names}), encoding="utf-8")
        with patch("pipeline_rules.importlib.import_module", side_effect=lambda name: modules[name]):
            return RulePipeline(self.manifest)

    def test_rules_run_in_manifest_order_and_normalize_rows(self):
        calls = []

        class First:
            def normalize(self, table, row):
                calls.append(("first", table))
                row["Value"] += 1
                return row

            def transform(self, context):
                calls.append(("transform", "first"))

        class Second:
            def transform(self, context):
                calls.append(("transform", "second"))

        pipeline = self.pipeline(["business.first", "business.second"],
                                 {"business.first": SimpleNamespace(Rule=First),
                                  "business.second": SimpleNamespace(Rule=Second)})
        self.assertEqual({"Value": 2}, pipeline.normalize("sample", {"Value": 1}))
        pipeline.run("transform", ExportContext({}, self.stage, ()))
        self.assertEqual([("first", "sample"), ("transform", "first"),
                          ("transform", "second")], calls)

    def test_duplicate_rule_or_generated_file_is_rejected(self):
        class Generator:
            def generate(self, context):
                return {"Shared.g.cs": "content"}

        modules = {name: SimpleNamespace(Rule=Generator)
                   for name in ("business.first", "business.second")}
        with self.assertRaisesRegex(ValueError, "duplicate business rule"):
            self.pipeline(["business.first", "business.first"], modules)
        pipeline = self.pipeline(["business.first", "business.second"], modules)
        with self.assertRaisesRegex(ValueError, "duplicate generated code artifact"):
            pipeline.run("generate", ExportContext({}, self.stage, ()))
        self.assertFalse((self.stage / "code/Shared.g.cs").exists())

    def test_luban_code_cannot_be_overwritten(self):
        class Generator:
            def generate(self, context):
                return {"Owned.g.cs": "business"}

        path = self.stage / "code/Owned.g.cs"
        path.write_text("luban", encoding="utf-8")
        pipeline = self.pipeline(["business.first"],
                                 {"business.first": SimpleNamespace(Rule=Generator)})
        with self.assertRaisesRegex(ValueError, "conflicts with Luban output"):
            pipeline.run("generate", ExportContext({}, self.stage, ()))
        self.assertEqual("luban", path.read_text(encoding="utf-8"))

    def test_reference_ownership_is_checked(self):
        class First:
            def references(self):
                return {"skills": {"CastFlowId": "skill_flows"}}, {("skills", "CastFlowId")}

        class Second:
            def references(self):
                return {"skills": {"CastFlowId": "skill_flows"}}, set()

        modules = {"business.first": SimpleNamespace(Rule=First),
                   "business.second": SimpleNamespace(Rule=Second)}
        pipeline = self.pipeline(["business.first"], modules)
        self.assertEqual(({"skills": {"CastFlowId": "skill_flows"}},
                          {("skills", "CastFlowId")}), pipeline.reference_specs())
        pipeline = self.pipeline(["business.first", "business.second"], modules)
        with self.assertRaisesRegex(ValueError, "duplicate reference owner"):
            pipeline.reference_specs()

    def test_dependencies_order_rules_and_reject_cycles(self):
        calls = []

        class First:
            def project(self, context):
                calls.append("first")

        class Second:
            def project(self, context):
                calls.append("second")

        modules = {"business.first": SimpleNamespace(Rule=First),
                   "business.second": SimpleNamespace(Rule=Second)}
        ordered = [{"module": "business.second", "after": ["business.first"]}, "business.first"]
        pipeline = self.pipeline(ordered, modules)
        pipeline.run("project", ExportContext({}, self.stage, ()))
        self.assertEqual(["first", "second"], calls)
        cyclic = [{"module": "business.first", "after": ["business.second"]},
                  {"module": "business.second", "after": ["business.first"]}]
        with self.assertRaisesRegex(ValueError, "cyclic business rule dependency"):
            self.pipeline(cyclic, modules)


if __name__ == "__main__":
    unittest.main()
