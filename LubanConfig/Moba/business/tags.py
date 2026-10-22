"""Compile tag rules and emit the stable runtime tag API."""

import json
import math

from tag_excel import (build_tag_usage_report, compile_tag_tables,
                       finalize_tag_usage_report, generate_flags_code, gate_enum_members,
                       preview_tag_rule, preview_tag_template, validate_flag_abi, validate_gate_abi,
                       validate_tag_abi)


TAG_NAMESPACE = {"GameplayState": 1, "SkillSelection": 2}


def validate_tag_hud_presentations(rows, tag_definitions, vfx_rows=()):
    """Validate the view-channel catalog against published gameplay facts."""
    tags = {row["Id"]: row for row in tag_definitions}
    vfx_ids = {row["Id"] for row in vfx_rows}
    seen = set()
    allowed_channels = 1 | 2 | 4
    for row in rows:
        definition_id = row.get("DefinitionId", 0)
        owner = f"tag_hud_presentations/{definition_id}"
        if definition_id in seen:
            raise ValueError(f"{owner}: duplicate DefinitionId")
        seen.add(definition_id)
        tag = tags.get(definition_id)
        if tag is None or tag.get("Namespace") != TAG_NAMESPACE["GameplayState"] or \
                tag.get("Status") != 0 or tag.get("Kind") != 1:
            raise ValueError(f"{owner}: must reference an active gameplay fact tag")
        name = row.get("DisplayName") or row.get("LocalizationKey")
        if not name:
            raise ValueError(f"{owner}: DisplayName or LocalizationKey is required")
        channel_mask = row.get("ChannelMask", 1)
        if type(channel_mask) is not int or channel_mask <= 0 or channel_mask & ~allowed_channels:
            raise ValueError(f"{owner}: invalid ChannelMask")
        if row.get("HeadTextDurationMs", 0) < 0 or row.get("VfxDurationMs", 0) < 0:
            raise ValueError(f"{owner}: channel durations cannot be negative")
        if channel_mask & 4 and row.get("VfxId", 0) <= 0:
            raise ValueError(f"{owner}: VfxId is required for the Vfx channel")
        if row.get("VfxId", 0) and row["VfxId"] not in vfx_ids:
            raise ValueError(f"{owner}: references missing VFX {row['VfxId']}")
        for component in ("ColorR", "ColorG", "ColorB", "ColorA"):
            value = row.get(component, 1.0)
            if not isinstance(value, (int, float)) or not 0.0 <= value <= 1.0:
                raise ValueError(f"{owner}: {component} must be between 0 and 1")


def stable_string_id(name):
    # Matches StableHashV1.Fnv1a32Utf16NonNegative.
    value = 2166136261
    for character in name:
        value = ((value ^ ord(character)) * 16777619) & 0xFFFFFFFF
    return value & 0x7FFFFFFF


def stable_action_id(name):
    return stable_string_id("action:" + name)


def validate_trigger_tag_references(triggers, tag_definitions, tag_rule_groups=(),
                                    tag_templates=(), continuous_processes=()):
    tags = {row["Id"]: row for row in tag_definitions}
    groups = {row["Id"] for row in tag_rule_groups}
    templates = {row["Id"] for row in tag_templates}
    processes = {row["Id"] for row in continuous_processes}
    remove_buff_id = stable_action_id("remove_buff")
    rule_function_id = stable_string_id("predicate:source_target_tag_rules")
    for trigger in triggers:
        for node in (trigger.get("Predicate") or {}).get("Nodes", []):
            if node.get("Kind") != "Function" or node.get("FunctionId") != rule_function_id:
                continue
            owner = f"trigger/{trigger['TriggerId']}/source_target_tag_rules"
            if node.get("FunctionArity") != 2:
                raise ValueError(f"{owner}: expected two rule group arguments")
            active = 0
            for field in ("Left", "Right"):
                arg = node.get(field) or {}
                value = arg.get("ConstValue", 0) if arg.get("Kind") == "Const" else None
                if type(value) not in (int, float) or not math.isfinite(value) or int(value) != value:
                    raise ValueError(f"{owner}: {field} must be a constant rule group ID")
                if value < 0 or value > 0 and value not in groups:
                    raise ValueError(f"{owner}: {field} references a missing rule group {value}")
                active += value > 0
            if not active:
                raise ValueError(f"{owner}: at least one rule group is required")
        for action in trigger.get("Actions", []):
            args = action.get("Args") or {}
            references = {}
            for field, known in (("continuous_tag_template_id", templates),
                                 ("continuous_process_id", processes)):
                if field not in args:
                    continue
                arg = args[field]
                value = arg.get("ConstValue", 0) if arg.get("Kind") == "Const" else None
                owner = f"trigger/{trigger['TriggerId']}/action/{action.get('ActionId')}/{field}"
                if type(value) not in (int, float) or not math.isfinite(value) or int(value) != value or value < 0:
                    raise ValueError(f"{owner}: expected a nonnegative constant ID")
                if value and int(value) not in known:
                    raise ValueError(f"{owner}: references a missing ID {int(value)}")
                references[field] = int(value)
            if references.get("continuous_tag_template_id") and references.get("continuous_process_id"):
                raise ValueError(f"trigger/{trigger['TriggerId']}/action/{action.get('ActionId')}: "
                                 "continuous process and tag template cannot both be set")
            if action.get("ActionId") != remove_buff_id:
                continue
            owner = f"trigger/{trigger['TriggerId']}/remove_buff"
            if "remove_slow" in args:
                raise ValueError(f"{owner}: remove_slow is retired; use tag_id")
            tag_arg = args.get("tag_id")
            if tag_arg is None:
                continue
            value = tag_arg.get("ConstValue") if tag_arg.get("Kind") == "Const" else None
            if type(value) not in (int, float) or not math.isfinite(value) or int(value) != value:
                raise ValueError(f"{owner}: tag_id must be a constant tag definition ID")
            tag = tags.get(int(value))
            if tag is None or tag["Namespace"] != 1 or tag["Status"] != 0:
                raise ValueError(f"{owner}: tag_id references a missing or inactive gameplay tag {value}")


def project_tag_references(tables):
    tags = {}
    for row in tables["tag_definitions"]:
        identity = row["Id"]
        if identity in tags:
            raise ValueError(f"tag_definitions: duplicate ID {identity}")
        tags[identity] = row
    names = [(row["Namespace"], row["Name"]) for row in tags.values()]
    if len(names) != len(set(names)) or any(not row["Name"] for row in tags.values()):
        raise ValueError("tag_definitions: duplicate or empty name")
    for identity, tag in tags.items():
        if tag["Namespace"] not in TAG_NAMESPACE.values():
            raise ValueError(f"tag_definitions/{identity}: invalid namespace")

    def resolve(owner, ids, namespace, allow_category=False):
        if len(ids or []) != len(set(ids or [])):
            raise ValueError(f"{owner}: duplicate tag ID")
        result = []
        for identity in ids or []:
            if identity not in tags:
                raise ValueError(f"{owner}: missing tag definition {identity}")
            tag = tags[identity]
            if tag["Namespace"] != TAG_NAMESPACE[namespace]:
                raise ValueError(f"{owner}: tag {identity} belongs to another namespace")
            if tag["Status"] != 0 or tag["Kind"] != 1 and not allow_category:
                raise ValueError(f"{owner}: tag {identity} is planned or is a category")
            result.append(tag["Name"])
        return result

    for row in tables["skills"]:
        resolve(f"skills/{row['Id']}", row["Tags"], "SkillSelection")
    for name in ("summons", "gameplays"):
        for row in tables[name]:
            if row["Tags"]:
                raise ValueError(f"{name}/{row['Id']}: unused Tags must be empty")
    return resolve


class Rule:
    def project(self, context):
        context.state["resolve_tags"] = project_tag_references(context.tables)
        if "tag_hud_presentations" in context.tables:
            validate_tag_hud_presentations(
                context.tables["tag_hud_presentations"],
                context.tables["tag_definitions"],
                context.tables.get("vfx", []))

    def validate_graph(self, graph, tables, external):
        validate_trigger_tag_references(external["triggers"], tables.get("tag_definitions", []),
                                        tables.get("tag_rule_groups", []),
                                        tables.get("continuous_tag_templates", []),
                                        tables.get("continuous_processes", []))
        report_path = external.get("tag_report_path")
        if report_path is not None and report_path.exists():
            report = json.loads(report_path.read_text(encoding="utf-8"))
            for flow in external["skill_flows"]:
                template_id = flow.get("PipelineContinuousTagTemplateId", 0)
                if template_id:
                    report["templateConsumers"].append({"table": "skill_flows", "id": flow["Id"],
                                                        "field": "PipelineContinuousTagTemplateId",
                                                        "templateId": template_id})
            for trigger in external["triggers"]:
                for node in (trigger.get("Predicate") or {}).get("Nodes", []):
                    if node.get("Kind") != "Function" or node.get("FunctionId") != stable_string_id("predicate:source_target_tag_rules"):
                        continue
                    for field, actor in (("Left", "source"), ("Right", "target")):
                        group_id = (node.get(field) or {}).get("ConstValue", 0)
                        if group_id:
                            report.setdefault("ruleConsumers", []).append({"table": "triggers",
                                                                            "id": trigger["TriggerId"],
                                                                            "actor": actor,
                                                                            "ruleGroupId": int(group_id)})
                for action in trigger.get("Actions", []):
                    arg = (action.get("Args") or {}).get("continuous_tag_template_id")
                    if arg and arg.get("Kind") == "Const" and arg.get("ConstValue", 0):
                        report["templateConsumers"].append({"table": "triggers", "id": trigger["TriggerId"],
                                                            "actionId": action.get("ActionId"),
                                                            "field": "continuous_tag_template_id",
                                                            "templateId": int(arg["ConstValue"])})
            report_path.write_text(json.dumps(finalize_tag_usage_report(report), ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    def transform(self, context):
        source = dict(context.tables)
        compiled = {}
        context.tables.update(compile_tag_tables(context.tables, compiled))
        context.state["compiled_tag_rules"] = compiled
        sample = set(filter(None, (tag.strip() for tag in context.preview_tags.split(","))))
        known = {row["Name"] for row in source["tag_definitions"] if row["Namespace"] == 1}
        unknown = sample - known
        if unknown:
            raise ValueError(f"unknown sample gameplay tags: {sorted(unknown)}")
        report = build_tag_usage_report(source, compiled, sample)
        (context.stage / "tag-usage-report.json").write_text(
            json.dumps(finalize_tag_usage_report(report), ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        if context.preview_template is not None:
            template = next((row for row in context.tables["continuous_tag_templates"]
                             if row["Id"] == context.preview_template), None)
            if template is None:
                raise ValueError(f"missing continuous tag template: {context.preview_template}")
            print(json.dumps(preview_tag_template(template, sample), ensure_ascii=False, indent=2))
        if context.preview_rule is None:
            return
        if context.preview_rule not in compiled:
            raise ValueError(f"missing tag rule group: {context.preview_rule}")
        print(json.dumps({"ruleGroupId": context.preview_rule, "tags": sorted(sample),
                          "tree": compiled[context.preview_rule],
                          "matches": preview_tag_rule(compiled[context.preview_rule], sample)},
                         ensure_ascii=False, indent=2))

    def validate(self, context):
        configured = {row["Id"] for row in context.tables["tag_behavior_gates"]}
        if configured != set(gate_enum_members()):
            raise ValueError("tag_behavior_gates: every Luban gate enum member needs a row")
        for root in context.published_roots:
            for name, validator in (("tag_definitions", validate_tag_abi),
                                    ("tag_result_flags", validate_flag_abi),
                                    ("tag_behavior_gates", validate_gate_abi)):
                path = root / "moba" / f"{name}.json"
                if path.exists():
                    validator(context.tables[name], json.loads(path.read_text(encoding="utf-8-sig")))

    def generate(self, context):
        return {
            "MobaTagResultFlags.g.cs": generate_flags_code(context.tables["tag_result_flags"]),
        }
