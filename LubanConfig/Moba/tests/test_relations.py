"""Typed relationship checks for table and editor-owned IDs."""

import unittest

from relations import ReferenceSpec, RelationGraph, relation_report


class RelationGraphTests(unittest.TestCase):
    def test_nested_list_reference_and_reverse_edge(self):
        graph = RelationGraph({
            "battle_start": [{"RowId": 1, "Players": [{"HeroId": 10}, {"HeroId": 11}]}],
            "characters": [{"Id": 10}, {"Id": 11}],
        })
        edges = graph.validate([ReferenceSpec("battle_start", "Players[].HeroId", "characters", True)])
        self.assertEqual([10, 11], [edge["targetId"] for edge in edges])
        self.assertEqual("battle_start/1/Players/0", edges[0]["source"])
        report = relation_report(edges)
        self.assertEqual("battle_start/1/Players/0", report["dependents"][0]["usedBy"][0]["source"])

    def test_missing_nested_reference_reports_path(self):
        graph = RelationGraph({"summons": [{"Id": 3, "AttrScales": [{"AttrId": 99}]}],
                               "attr_types": [{"Id": 1}]})
        with self.assertRaisesRegex(ValueError, "summons/3/AttrScales/0: AttrId references missing"):
            graph.validate([ReferenceSpec("summons", "AttrScales[].AttrId", "attr_types", True)])

    def test_optional_zero_and_non_integer_are_distinct(self):
        graph = RelationGraph({"skills": [{"Id": 1, "CastFlowId": 0}],
                               "skill_flows": [{"Id": 2}]})
        graph.validate([ReferenceSpec("skills", "CastFlowId", "skill_flows")])
        graph.rows["skills"][0]["CastFlowId"] = True
        with self.assertRaisesRegex(ValueError, "requires an integer"):
            graph.validate([ReferenceSpec("skills", "CastFlowId", "skill_flows")])
        graph.rows["skills"][0]["CastFlowId"] = 0
        with self.assertRaisesRegex(ValueError, "requires a skill_flows ID"):
            graph.validate([ReferenceSpec("skills", "CastFlowId", "skill_flows", True)])

    def test_any_value_records_the_resolved_namespace(self):
        graph = RelationGraph({"rules": [{"id": 30}],
                               "triggers": [{"TriggerId": 20}]})
        graph.check_any_value("flow/phase/RulePlan", {"TriggerIds": [20, 30]},
                              "TriggerIds", ("rules", "triggers"))
        self.assertEqual(["triggers", "rules"], [edge["target"] for edge in graph.edges])
        with self.assertRaisesRegex(ValueError, "references missing one of rules or triggers IDs 99"):
            graph.check_any_value("flow/phase/RulePlan", {"TriggerIds": [99]},
                                  "TriggerIds", ("rules", "triggers"))

    def test_any_value_rejects_ambiguous_ids(self):
        graph = RelationGraph({"rules": [{"id": 20}],
                               "triggers": [{"TriggerId": 20}]})
        with self.assertRaisesRegex(ValueError, "references ambiguous rules or triggers ID 20"):
            graph.check_any_value("flow/phase/RulePlan", {"TriggerIds": [20]},
                                  "TriggerIds", ("rules", "triggers"))


if __name__ == "__main__":
    unittest.main()
