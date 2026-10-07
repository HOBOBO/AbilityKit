import unittest

from tools.modifier_impact_report import build_report


class ModifierImpactReportTests(unittest.TestCase):
    def test_shared_and_unused_templates_are_reported(self):
        report = build_report(
            [{"Id": 100001, "TargetKind": "Attribute", "TargetId": 17},
             {"Id": 500001, "TargetKind": "SkillParameter", "TargetId": 40002}],
            [{"Id": 1, "ModifierIds": [100001]},
             {"Id": 2, "ModifierIds": [100001]}],
            [])
        self.assertEqual([100001], report["sharedIds"])
        self.assertEqual([500001], report["unusedIds"])
        self.assertEqual(2, len(report["templates"][0]["usedBy"]))

    def test_missing_template_fails(self):
        with self.assertRaisesRegex(ValueError, "missing modifier template 7"):
            build_report([], [{"Id": 1, "ModifierIds": [7]}], [])


if __name__ == "__main__":
    unittest.main()
