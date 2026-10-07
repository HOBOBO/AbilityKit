"""Summon and summon action references."""

from relations import ReferenceSpec


class Rule:
    def nested_relations(self):
        return [ReferenceSpec("summons", "AttrScales[].AttrId", "attr_types", required=True),
                ReferenceSpec("summon_attr_inherits", "Scales[].AttrId", "attr_types", required=True)]

    def references(self):
        return ({
            "summons": {"AttributeTemplateId": "attribute_templates", "ModelId": "models",
                        "SkillIds": "skills", "PassiveSkillIds": "passive_skills",
                        "DefaultComponentTemplateIds": "component_templates", "BrainId": "brains",
                        "InheritAttributeConfigId": "summon_attr_inherits"},
            "spawn_summon_action_templates": {"SummonId": "summons",
                                              "QueryTemplateId": "search_query_templates"},
        }, {("summons", "AttributeTemplateId"),
            ("spawn_summon_action_templates", "SummonId")})
