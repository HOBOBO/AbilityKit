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


def validate_hero_level_skill_growth_parameters(rows):
    seen = set()
    for row in rows:
        owner = f"hero_level_skill_growth_parameters/{row['Id']}"
        skill_id = row.get("SkillId")
        champion_level = row.get("ChampionLevel")
        if not isinstance(skill_id, int) or isinstance(skill_id, bool) or skill_id <= 0:
            raise ValueError(f"{owner}: SkillId must be a positive integer")
        if not isinstance(champion_level, int) or isinstance(champion_level, bool) or champion_level <= 0:
            raise ValueError(f"{owner}: ChampionLevel must be a positive integer")
        key = (skill_id, champion_level)
        if key in seen:
            raise ValueError(f"{owner}: duplicate SkillId + ChampionLevel {key}")
        seen.add(key)


def validate_character_skill_preview_vfx(characters, skills, vfx_entries):
    skills_by_id = {row["Id"]: row for row in skills}
    vfx_ids = {row["Id"] for row in vfx_entries}
    for character in characters:
        for skill_id in character.get("SkillIds") or []:
            skill = skills_by_id.get(skill_id)
            if skill is None:
                raise ValueError(f"characters/{character['Id']}: missing skill {skill_id}")
            vfx_id = skill.get("PreviewVfxId", 0)
            if not isinstance(vfx_id, int) or isinstance(vfx_id, bool) or vfx_id <= 0:
                raise ValueError(f"skills/{skill_id}: PreviewVfxId must reference a VFX for character use")
            if vfx_id not in vfx_ids:
                raise ValueError(f"skills/{skill_id}: PreviewVfxId references missing vfx {vfx_id}")


class Rule:
    def project(self, context):
        validate_skill_level_tables(context.tables["skill_level_tables"])
        validate_hero_level_skill_growth_parameters(context.tables["hero_level_skill_growth_parameters"])
        validate_character_skill_preview_vfx(
            context.tables["characters"], context.tables["skills"], context.tables["vfx"])

    def validate_graph(self, graph, tables, external):
        def check_phase(phase, owner):
            if phase.get("RulePlan") is not None:
                # RulePlan historically referenced the legacy rules namespace.
                # Trigger Authoring aggregate triggers are now also valid here;
                # both are resolved through the same runtime trigger database.
                graph.check_any_value(owner + "/RulePlan", phase["RulePlan"],
                                      "TriggerIds", ("rules", "triggers"))
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
