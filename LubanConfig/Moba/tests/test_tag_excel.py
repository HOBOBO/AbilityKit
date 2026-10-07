import unittest

from tag_excel import TAG_TABLES, build_tag_usage_report, compile_tag_tables, gate_enum_members, generate_flags_code, preview_tag_rule, validate_flag_abi, validate_gate_abi, validate_tag_definitions, validate_tag_abi


class TagWorkbookTests(unittest.TestCase):
    def setUp(self):
        self.tables = {name: [] for name in TAG_TABLES}
        self.tables["tag_definitions"] = [
            {"Id": 1000, "Name": "State", "DisplayName": "状态", "Namespace": 1, "ParentId": 0, "Kind": 0, "Status": 0},
            {"Id": 1100, "Name": "State.Ready", "DisplayName": "就绪", "Namespace": 1, "ParentId": 1000, "Kind": 1, "Status": 0},
        ]
        self.tables["tag_rule_groups"] = [{"Id": 2001, "Name": "ready", "RootNodeId": 200101}]
        self.tables["tag_rule_nodes"] = [self.node(200101, 2001, 0, 1, tag=1100)]
        self.tables["tag_result_flags"] = [{"Id": 1, "Key": "CannotMove", "BitIndex": 4}]
        self.tables["tag_result_mappings"] = [{"Id": 1, "FlagId": 1, "RuleGroupId": 2001}]
        self.tables["tag_behavior_gates"] = [{"Id": 5, "Name": "移动", "BlockedFlagIds": [1]}]

    @staticmethod
    def node(identity, group, parent, operator, tag=0, ref=0, order=0):
        return {"Id": identity, "GroupId": group, "ParentId": parent, "Order": order,
                "Operator": operator, "TagId": tag, "Exact": False, "RefGroupId": ref}

    def test_rule_reference_expands_and_continuous_template_uses_tag_ids(self):
        self.tables["tag_rule_groups"].append({"Id": 2002, "Name": "not ready", "RootNodeId": 200201})
        self.tables["tag_rule_nodes"] += [self.node(200201, 2002, 0, 4), self.node(200202, 2002, 200201, 5, ref=2001)]
        self.tables["continuous_tag_templates"] = [{"Id": 7, "Name": "test",
            "ActivationRequiredTagIds": [], "ActivationBlockedTagIds": [],
            "ApplicationTagIds": [1100], "RemovalRequiredTagIds": [],
            "RemovalBlockedTagIds": [], "OngoingRequiredTagIds": [],
            "OngoingBlockedTagIds": [],
            "ActivationRuleGroupId": 2002, "RemovalRuleGroupId": 0,
            "OngoingRuleGroupId": 0, "ExecutionBlockedFlagIds": [1]}]
        row = compile_tag_tables(self.tables)["continuous_tag_templates"][0]
        self.assertEqual(["State.Ready"], row["ApplicationTags"])
        self.assertEqual("Not", row["ActivationCondition"]["Operator"])
        self.assertEqual("State.Ready", row["ActivationCondition"]["Children"][0]["Tag"])
        self.assertEqual(1 << 4, row["ExecutionBlockedResultMask"])
        self.assertIn("CannotMove = 1UL << 4", generate_flags_code(self.tables["tag_result_flags"]))

    def test_execution_gate_rejects_unknown_and_duplicate_flags(self):
        self.test_rule_reference_expands_and_continuous_template_uses_tag_ids()
        template = self.tables["continuous_tag_templates"][0]
        for ids in ([999], [1, 1]):
            template["ExecutionBlockedFlagIds"] = ids
            with self.assertRaisesRegex(ValueError, "ExecutionBlockedFlagIds"):
                compile_tag_tables(self.tables)

    def test_behavior_gate_references_and_report(self):
        rules = {}
        self.tables["buffs"] = [{"Id": 9, "ContinuousTagTemplateId": 7}]
        self.tables["continuous_tag_templates"] = [{"Id": 7, "Name": "grant",
            "ApplicationTagIds": [1100], "ActivationRequiredTagIds": [], "ActivationBlockedTagIds": [],
            "RemovalRequiredTagIds": [], "RemovalBlockedTagIds": [],
            "OngoingRequiredTagIds": [], "OngoingBlockedTagIds": [],
            "ActivationRuleGroupId": 0, "RemovalRuleGroupId": 0, "OngoingRuleGroupId": 0}]
        compile_tag_tables(self.tables, rules)
        report = build_tag_usage_report(self.tables, rules, ["State.Ready"])
        self.assertEqual(["CannotMove"], report["activeFlags"])
        self.assertTrue(report["behaviorGates"][0]["blocked"])
        self.assertEqual("Move", report["behaviorGates"][0]["key"])
        self.assertEqual([{"table": "buffs", "id": 9, "field": "ContinuousTagTemplateId", "templateId": 7}], report["templateConsumers"])
        self.assertEqual("Move", gate_enum_members()[5])
        self.tables["tag_behavior_gates"][0]["Id"] = 999
        with self.assertRaisesRegex(ValueError, "ID missing from Luban enum"):
            compile_tag_tables(self.tables)
        self.tables["tag_behavior_gates"][0]["Id"] = 5
        for blocked in ([999], [1, 1], []):
            self.tables["tag_behavior_gates"][0]["BlockedFlagIds"] = blocked
            with self.assertRaisesRegex(ValueError, "BlockedFlagIds"):
                compile_tag_tables(self.tables)

    def test_duplicate_result_mapping_pair_is_rejected(self):
        self.tables["tag_result_mappings"].append({"Id": 2, "FlagId": 1, "RuleGroupId": 2001})
        with self.assertRaisesRegex(ValueError, "duplicate flag and rule group pair"):
            compile_tag_tables(self.tables)

    def test_skill_execution_gate_compiles_and_validates_flags(self):
        self.tables["skills"] = [{"Id": 7, "ExecutionBlockedFlagIds": [1]}]
        skill = compile_tag_tables(self.tables)["skills"][0]
        self.assertEqual(1 << 4, skill["ExecutionBlockedResultMask"])
        self.assertNotIn("ExecutionBlockedFlagIds", skill)
        for ids in ([999], [1, 1]):
            self.tables["skills"][0]["ExecutionBlockedFlagIds"] = ids
            with self.assertRaisesRegex(ValueError, "skills/7/ExecutionBlockedFlagIds"):
                compile_tag_tables(self.tables)

    def test_unknown_tag_rejected(self):
        self.tables["tag_rule_nodes"][0]["TagId"] = 999
        with self.assertRaisesRegex(ValueError, "invalid gameplay tag"):
            compile_tag_tables(self.tables)

    def test_skill_selection_tag_rejected(self):
        self.tables["tag_definitions"] += [
            {"Id": 90, "Name": "Skill", "DisplayName": "技能", "Namespace": 2, "ParentId": 0, "Kind": 0, "Status": 0},
            {"Id": 2000, "Name": "Skill.Role", "DisplayName": "技能用途", "Namespace": 2, "ParentId": 90, "Kind": 0, "Status": 0},
            {"Id": 2001, "Name": "Skill.Role.Control", "DisplayName": "控制技能", "Namespace": 2, "ParentId": 2000, "Kind": 1, "Status": 0},
        ]
        self.tables["tag_rule_nodes"][0]["TagId"] = 2001
        with self.assertRaisesRegex(ValueError, "invalid gameplay tag"):
            compile_tag_tables(self.tables)

    def test_tag_parent_and_name_are_checked(self):
        self.tables["tag_definitions"][1]["ParentId"] = 0
        with self.assertRaisesRegex(ValueError, "ParentId must match"):
            compile_tag_tables(self.tables)
        self.tables["tag_definitions"][1]["ParentId"] = 1000
        self.tables["tag_definitions"][1]["Name"] = "就绪"
        with self.assertRaisesRegex(ValueError, "invalid gameplay tag ID or name"):
            compile_tag_tables(self.tables)

    def test_retired_tag_id_cannot_be_reused(self):
        self.tables["tag_definitions"][1]["Id"] = 1003
        with self.assertRaisesRegex(ValueError, "invalid gameplay tag ID or name"):
            validate_tag_definitions(self.tables["tag_definitions"])

    def test_new_gameplay_ids_follow_state_and_debuff_ranges(self):
        self.tables["tag_definitions"][1].update(Id=1200, Name="State.Ready")
        validate_tag_definitions(self.tables["tag_definitions"])
        self.tables["tag_definitions"][1]["Id"] = 1500
        with self.assertRaisesRegex(ValueError, "invalid gameplay tag ID or name"):
            validate_tag_definitions(self.tables["tag_definitions"])

    def test_published_gameplay_id_cannot_be_renamed(self):
        self.tables["tag_definitions"][1].update(Id=1001, Name="State.Ready")
        with self.assertRaisesRegex(ValueError, "invalid gameplay tag ID or name"):
            validate_tag_definitions(self.tables["tag_definitions"])

    def test_skill_tag_is_optional_and_uses_semantic_id_range(self):
        self.tables["tag_definitions"] += [
            {"Id": 90, "Name": "Skill", "DisplayName": "技能", "Namespace": 2, "ParentId": 0, "Kind": 0, "Status": 0},
            {"Id": 2000, "Name": "Skill.Role", "DisplayName": "技能用途", "Namespace": 2, "ParentId": 90, "Kind": 0, "Status": 0},
        ]
        validate_tag_definitions(self.tables["tag_definitions"])
        self.tables["tag_definitions"][-1]["Id"] = 101
        with self.assertRaisesRegex(ValueError, "invalid skill tag ID or name"):
            validate_tag_definitions(self.tables["tag_definitions"])

    def test_legacy_numeric_namespace_is_rejected(self):
        self.tables["tag_definitions"].append(
            {"Id": 1, "Name": "1", "DisplayName": "旧数字", "Namespace": 0, "ParentId": 0, "Kind": 1, "Status": 0})
        with self.assertRaisesRegex(ValueError, "invalid Namespace"):
            validate_tag_definitions(self.tables["tag_definitions"])

    def test_planned_fact_cannot_enter_an_active_rule(self):
        self.tables["tag_definitions"][1]["Status"] = 1
        with self.assertRaisesRegex(ValueError, "invalid gameplay tag leaf"):
            compile_tag_tables(self.tables)

    def test_category_can_be_queried_but_not_granted(self):
        self.tables["tag_rule_nodes"][0]["TagId"] = 1000
        self.tables["continuous_tag_templates"] = [{"Id": 7, "Name": "test",
            "ActivationRequiredTagIds": [], "ActivationBlockedTagIds": [],
            "ApplicationTagIds": [1000], "RemovalRequiredTagIds": [],
            "RemovalBlockedTagIds": [], "OngoingRequiredTagIds": [],
            "OngoingBlockedTagIds": [],
            "ActivationRuleGroupId": 0, "RemovalRuleGroupId": 0,
            "OngoingRuleGroupId": 0}]
        with self.assertRaisesRegex(ValueError, "invalid tag IDs"):
            compile_tag_tables(self.tables)
        self.tables["continuous_tag_templates"][0]["ApplicationTagIds"] = []
        self.assertEqual("test", compile_tag_tables(self.tables)["continuous_tag_templates"][0]["Name"])

    def test_template_cannot_depend_on_its_own_granted_tag(self):
        self.tables["continuous_tag_templates"] = [{"Id": 7, "Name": "test",
            "ActivationRequiredTagIds": [], "ActivationBlockedTagIds": [],
            "ApplicationTagIds": [1100], "RemovalRequiredTagIds": [],
            "RemovalBlockedTagIds": [], "OngoingRequiredTagIds": [1000],
            "OngoingBlockedTagIds": [],
            "ActivationRuleGroupId": 0, "RemovalRuleGroupId": 0,
            "OngoingRuleGroupId": 0, "ExecutionBlockedFlagIds": []}]
        with self.assertRaisesRegex(ValueError, "references its own granted tag"):
            compile_tag_tables(self.tables)
        self.tables["continuous_tag_templates"][0]["OngoingRequiredTagIds"] = []
        self.tables["tag_rule_nodes"][0]["TagId"] = 1000
        self.tables["continuous_tag_templates"][0]["RemovalRuleGroupId"] = 2001
        with self.assertRaisesRegex(ValueError, "references its own granted tag"):
            compile_tag_tables(self.tables)

    def test_live_fact_requires_an_active_category_parent(self):
        self.tables["tag_definitions"].extend([
            {"Id": 1200, "Name": "State.Group", "DisplayName": "规划分类", "Namespace": 1,
             "ParentId": 1000, "Kind": 0, "Status": 1},
            {"Id": 1201, "Name": "State.Group.Leaf", "DisplayName": "已接入事实", "Namespace": 1,
             "ParentId": 1200, "Kind": 1, "Status": 0},
        ])
        with self.assertRaisesRegex(ValueError, "parent must be an active category"):
            validate_tag_definitions(self.tables["tag_definitions"])

    def test_cyclic_group_reference_rejected(self):
        self.tables["tag_rule_nodes"][0].update(Operator=5, TagId=0, RefGroupId=2001)
        with self.assertRaisesRegex(ValueError, "cyclic group"):
            compile_tag_tables(self.tables)

    def test_orphan_parent_cycle_rejected(self):
        self.tables["tag_rule_nodes"] += [self.node(200102, 2001, 200103, 2), self.node(200103, 2001, 200102, 2)]
        with self.assertRaisesRegex(ValueError, "orphan or cyclic"):
            compile_tag_tables(self.tables)

    def test_duplicate_sibling_order_rejected(self):
        self.tables["tag_rule_nodes"] = [self.node(200101, 2001, 0, 3), self.node(200102, 2001, 200101, 1, tag=1100),
                                        self.node(200103, 2001, 200101, 1, tag=1100)]
        with self.assertRaisesRegex(ValueError, "duplicate sibling"):
            compile_tag_tables(self.tables)

    def test_not_requires_one_child(self):
        self.tables["tag_rule_nodes"][0].update(Operator=4, TagId=0)
        with self.assertRaisesRegex(ValueError, "invalid compound"):
            compile_tag_tables(self.tables)

    def test_rule_and_node_ids_follow_reserved_blocks(self):
        self.tables["tag_rule_groups"][0]["Id"] = 1
        with self.assertRaisesRegex(ValueError, "invalid ID range"):
            compile_tag_tables(self.tables)
        self.tables["tag_rule_groups"][0]["Id"] = 2001
        self.tables["tag_rule_nodes"][0]["Id"] = 1
        with self.assertRaisesRegex(ValueError, "ID must use group block"):
            compile_tag_tables(self.tables)

    def test_duplicate_flag_bit_rejected(self):
        self.tables["tag_result_flags"].append({"Id": 2, "Key": "CannotCast", "BitIndex": 4})
        with self.assertRaisesRegex(ValueError, "duplicate BitIndex"):
            compile_tag_tables(self.tables)

    def test_missing_flag_mapping_rejected(self):
        self.tables["tag_result_mappings"] = []
        with self.assertRaisesRegex(ValueError, "every flag needs"):
            compile_tag_tables(self.tables)

    def test_flag_key_must_generate_a_valid_non_reserved_member(self):
        for key in ("None", "class", "not valid", "9Flag"):
            with self.subTest(key=key):
                self.tables["tag_result_flags"][0]["Key"] = key
                with self.assertRaisesRegex(ValueError, "invalid or duplicate Key"):
                    compile_tag_tables(self.tables)

    def test_bit_63_is_supported(self):
        self.tables["tag_result_flags"][0]["BitIndex"] = 63
        compile_tag_tables(self.tables)
        self.assertIn("1UL << 63", generate_flags_code(self.tables["tag_result_flags"]))

    def test_rule_preview_uses_compiled_tree_and_hierarchy(self):
        rules = {}
        compile_tag_tables(self.tables, rules)
        self.assertFalse(preview_tag_rule(rules[2001], set()))
        self.assertTrue(preview_tag_rule(rules[2001], {"State.Ready"}))
        self.assertTrue(preview_tag_rule(rules[2001], {"State.Ready.Special"}))
        self.assertFalse(preview_tag_rule(rules[2001], {"State"}))

    def test_published_bit_cannot_be_reassigned_or_removed(self):
        with self.assertRaisesRegex(ValueError, "cannot be removed or reassigned"):
            validate_flag_abi(self.tables["tag_result_flags"], [{"Key": "CannotMove", "BitIndex": 3}])
        with self.assertRaisesRegex(ValueError, "cannot be removed or reassigned"):
            validate_flag_abi([], self.tables["tag_result_flags"])

    def test_published_behavior_gate_id_cannot_be_removed(self):
        published = self.tables["tag_behavior_gates"]
        with self.assertRaisesRegex(ValueError, "published ID cannot be removed"):
            validate_gate_abi([], published)
        changed = [dict(row) for row in published]
        changed[0]["Id"] += 100
        with self.assertRaisesRegex(ValueError, "published ID cannot be removed"):
            validate_gate_abi(changed, published)

    def test_published_tag_identity_is_frozen(self):
        published = [dict(row) for row in self.tables["tag_definitions"]]
        for field, value in (("Name", "State.Changed"), ("Namespace", 2), ("Kind", 0)):
            changed = [dict(row) for row in published]
            changed[1][field] = value
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, "published ID"):
                validate_tag_abi(changed, published)
        with self.assertRaisesRegex(ValueError, "published ID"):
            validate_tag_abi(published[:1], published)
        changed = [dict(row) for row in published]
        changed[1]["Status"] = 1
        with self.assertRaisesRegex(ValueError, "cannot become planned"):
            validate_tag_abi(changed, published)

    def test_template_usage_reports_pipeline_consumer(self):
        self.tables["skills"] = [{"Id": 9, "PipelineContinuousTagTemplateId": 7}]
        rules = {}
        compile_tag_tables(self.tables, rules)
        report = build_tag_usage_report(self.tables, rules)
        self.assertEqual([{"table": "skills", "id": 9,
                           "field": "PipelineContinuousTagTemplateId", "templateId": 7}],
                         report["templateConsumers"])

    def test_negative_ongoing_dependency_cycle_is_rejected(self):
        self.tables["tag_definitions"].append(
            {"Id": 1101, "Name": "State.Busy", "DisplayName": "忙碌", "Namespace": 1,
             "ParentId": 1000, "Kind": 1, "Status": 0})
        def template(identity, granted, required=(), blocked=()):
            return {"Id": identity, "Name": str(identity), "ApplicationTagIds": [granted],
                    "ActivationRequiredTagIds": [], "ActivationBlockedTagIds": [],
                    "RemovalRequiredTagIds": [], "RemovalBlockedTagIds": [],
                    "OngoingRequiredTagIds": list(required), "OngoingBlockedTagIds": list(blocked),
                    "ActivationRuleGroupId": 0, "RemovalRuleGroupId": 0,
                    "OngoingRuleGroupId": 0, "ExecutionBlockedFlagIds": []}
        self.tables["continuous_tag_templates"] = [template(7, 1100, required=[1101]),
                                                     template(8, 1101, blocked=[1100])]
        with self.assertRaisesRegex(ValueError, "negative ongoing dependency cycle"):
            compile_tag_tables(self.tables)
        self.tables["continuous_tag_templates"][1]["OngoingBlockedTagIds"] = []
        self.tables["continuous_tag_templates"][1]["OngoingRequiredTagIds"] = [1100]
        rules = {}
        compile_tag_tables(self.tables, rules)
        report = build_tag_usage_report(self.tables, rules)
        self.assertEqual(2, len(report["templateDependencies"]))
        self.assertEqual([{"templateIds": [7, 8], "hasExternalProvider": False}],
                         report["positiveDependencyCycles"])

    def test_direct_grant_cannot_sustain_its_own_template(self):
        self.tables["continuous_tag_templates"] = [{"Id": 7, "Name": "dependent",
            "ApplicationTagIds": [], "ActivationRequiredTagIds": [], "ActivationBlockedTagIds": [],
            "RemovalRequiredTagIds": [], "RemovalBlockedTagIds": [],
            "OngoingRequiredTagIds": [1100], "OngoingBlockedTagIds": [],
            "ActivationRuleGroupId": 0, "RemovalRuleGroupId": 0,
            "OngoingRuleGroupId": 0, "ExecutionBlockedFlagIds": []}]
        self.tables["buffs"] = [{"Id": 9, "TagNames": ["State.Ready"],
                                  "ContinuousTagTemplateId": 7}]
        with self.assertRaisesRegex(ValueError, "depends on its own direct grant"):
            compile_tag_tables(self.tables)

    def test_impact_report_filters_authorable_facts_and_traces_gates(self):
        self.tables["tag_definitions"].append(
            {"Id": 1101, "Name": "State.Future", "DisplayName": "未来", "Namespace": 1,
             "ParentId": 1000, "Kind": 1, "Status": 1})
        self.tables["buffs"] = [{"Id": 9, "TagNames": ["State.Ready"],
                                  "ContinuousTagTemplateId": 0}]
        rules = {}
        compile_tag_tables(self.tables, rules)
        report = build_tag_usage_report(self.tables, rules)
        self.assertEqual(["State.Ready"], report["authorableGameplayFacts"])
        impact = next(row for row in report["tagImpact"] if row["name"] == "State.Ready")
        self.assertEqual([{"table": "buffs", "id": 9}], impact["directGrantCarriers"])
        self.assertEqual([2001], impact["ruleGroupIds"])
        self.assertEqual(["CannotMove"], impact["resultFlags"])
        self.assertEqual(["Move"], impact["behaviorGates"])


if __name__ == "__main__":
    unittest.main()
