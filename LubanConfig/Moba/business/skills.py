"""Skill and passive skill references."""

import re


def validate_skill_level_tables(rows):
    for table in rows:
        owner = f"skill_level_tables/{table['Id']}"
        names = table.get("ParamNames") or []
        levels = table.get("Levels") or []
        if not levels:
            raise ValueError(f"{owner}: Levels must contain at least one level")
        if any(not isinstance(name, str) or not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", name)
               for name in names):
            raise ValueError(f"{owner}: ParamNames must be nonempty identifiers")
        if len(names) != len(set(names)):
            raise ValueError(f"{owner}: ParamNames contains duplicates")
        for index, level in enumerate(levels, 1):
            values = level.get("Params") or []
            if len(values) != len(names):
                raise ValueError(f"{owner}/Levels/{index}: Params has {len(values)} values, "
                                 f"but ParamNames has {len(names)} names")


class Rule:
    def project(self, context):
        validate_skill_level_tables(context.tables["skill_level_tables"])

    def validate_graph(self, graph, tables, external):
        def check_phase(phase, owner):
            if phase.get("RulePlan") is not None:
                graph.check_value(owner + "/RulePlan", phase["RulePlan"], "TriggerIds", "rules")
            if phase.get("Window") is not None:
                for field in ("OpenTriggerIds", "TickTriggerIds", "CloseTriggerIds"):
                    graph.check_value(owner + "/Window", phase["Window"], field, "triggers")
            if phase.get("Timeline") is not None:
                for index, event in enumerate(phase["Timeline"].get("Events") or []):
                    graph.check_value(f"{owner}/Timeline/Events/{index}", event, "EffectId", "triggers", True)
            for index, child in enumerate(phase.get("Children") or []):
                check_phase(child, f"{owner}/Children/{index}")
            repeat = phase.get("Repeat") or {}
            if repeat.get("Phase") is not None:
                check_phase(repeat["Phase"], owner + "/Repeat/Phase")

        for row in external["skill_flows"]:
            owner = f"skill_flows/{row['Id']}"
            graph.check_value(owner, row, "PipelineContinuousTagTemplateId", "continuous_tag_templates")
            for index, phase in enumerate(row.get("Phases") or []):
                check_phase(phase, f"{owner}/Phases/{index}")

    def references(self):
        return ({
            "skills": {"LevelTableId": "skill_level_tables", "SkillButtonTemplateId": "skill_button_templates",
                       "RequiredTargetQueryId": "search_query_templates", "PreCastFlowId": "skill_flows",
                       "CastFlowId": "skill_flows"},
            "passive_skills": {"ContinuousProcessIds": "continuous_processes", "TriggerIds": "triggers"},
        }, {("skills", "CastFlowId")})
