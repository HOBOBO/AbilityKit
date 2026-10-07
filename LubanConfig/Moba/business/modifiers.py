"""Modifier catalog validation and generated parameter keys."""

import json
import math

from modifier_parameters import (generate_parameter_keys, validate_consumer_contracts,
                                 validate_modifier_parameters, validate_modifier_template_ids)


MODIFIER_FIELDS = (
    "TargetKind", "TargetId", "Op", "Value", "Priority",
    "MagnitudeSourceType", "MagnitudeBaseValue", "MagnitudeCoefficient",
    "MagnitudeDuration", "MagnitudeDecayType", "MagnitudeAttributeTypeId",
    "MagnitudeContextKey", "MagnitudeCurve", "EvaluationPolicy", "AttributeSourceRole",
    "SelectorSlot", "SelectorBaseSkillId", "SelectorEffectiveSkillId",
    "SelectorLauncherTemplateId", "SelectorProjectileTemplateId", "SelectorAreaTemplateId",
    "SelectorSummonTemplateId", "SelectorBuffTemplateId", "CandidateSkillId",
    "CandidateReferenceId", "FormulaConstant", "FormulaLevelCoefficient", "FormulaTerms",
)
PROCESS_MODIFIER_FIELDS = MODIFIER_FIELDS


def project_modifier_bindings(tables, resolve_tags):
    modifiers = {}
    for row in tables["modifier_templates"]:
        identity = row["Id"]
        if identity in modifiers:
            raise ValueError(f"modifier_templates: duplicate ID {identity}")
        modifiers[identity] = row
    for name, kind, fields in (("buffs", 0, MODIFIER_FIELDS),
                               ("continuous_processes", 1, PROCESS_MODIFIER_FIELDS)):
        for row in tables[name]:
            owner = f"{name}/{row['Id']}"
            ids = row.pop("ModifierIds")
            if len(ids) != len(set(ids)):
                raise ValueError(f"{owner}: duplicate modifier ID")
            resolved = []
            for identity in ids:
                modifier = modifiers.get(identity)
                if modifier is None or modifier["Kind"] != kind:
                    raise ValueError(f"{owner}: missing or wrong-kind modifier {identity}")
                value = {field: (modifier.get(field, 0) if field == "SelectorBuffTemplateId" else modifier[field])
                         for field in fields}
                if kind == 0:
                    if value["MagnitudeContextKey"] == "":
                        value["MagnitudeContextKey"] = None
                    if value["MagnitudeCurve"] == []:
                        value["MagnitudeCurve"] = None
                resolved.append(value)
            if ids or "Modifiers" not in row:
                row["Modifiers"] = resolved
            if name == "buffs":
                for source, target in (("TagIds", "TagNames"),
                                       ("DispelBlockedByTagIds", "DispelBlockedByTagNames")):
                    tag_ids = row.pop(source)
                    if tag_ids:
                        row[target] = resolve_tags(owner, tag_ids, "GameplayState",
                                                   allow_category=source == "DispelBlockedByTagIds")
                    elif target not in row:
                        row[target] = None
            else:
                row["Tags"] = resolve_tags(owner, row.pop("TagIds"), "GameplayState")


class Rule:
    def project(self, context):
        project_modifier_bindings(context.tables, context.state["resolve_tags"])

    def validate_graph(self, graph, tables, external):
        for modifier in tables["modifier_templates"]:
            owner = f"modifier_templates/{modifier['Id']}"
            selector_references = {
                "SelectorBaseSkillId": "skills", "SelectorEffectiveSkillId": "skills",
                "SelectorLauncherTemplateId": "projectile_launchers",
                "SelectorProjectileTemplateId": "projectiles", "SelectorAreaTemplateId": "aoes",
                "SelectorSummonTemplateId": "summons", "SelectorBuffTemplateId": "buffs",
            }
            for field in ("SelectorSlot", *selector_references):
                if type(modifier[field]) is not int or modifier[field] < 0:
                    raise ValueError(f"{owner}: {field} must be a nonnegative integer")
            for field, table in selector_references.items():
                graph.check_value(owner, modifier, field, table)
            selected = {field for field in ("SelectorSlot", *selector_references) if modifier[field]}
            kind = modifier["TargetKind"]
            if kind not in (1, 4, 5, 6):
                raise ValueError(f"{owner}: unsupported TargetKind {kind}")
            if kind == 5:
                if selected:
                    raise ValueError(f"{owner}: slot candidate cannot use parameter selectors")
                if modifier["TargetId"] <= 0 or modifier["TargetId"] > 16:
                    raise ValueError(f"{owner}: slot candidate requires a slot in 1..16")
                graph.check_value(owner, modifier, "CandidateSkillId", "skills", True)
            elif modifier["CandidateSkillId"]:
                raise ValueError(f"{owner}: CandidateSkillId requires SkillSlotCandidate target")
            if kind == 6:
                parameter = graph.index["modifier_parameters"].get(modifier["TargetId"])
                reference_tables = {(7, "LauncherId"): "projectile_launchers",
                                    (2, "ProjectileId"): "projectiles", (3, "SummonId"): "summons"}
                target = reference_tables.get((parameter["Domain"], parameter["Key"])) if parameter else None
                if target is None:
                    raise ValueError(f"{owner}: unsupported reference parameter {modifier['TargetId']}")
                graph.check_value(owner, modifier, "CandidateReferenceId", target, True)
                available = {"SelectorSlot", "SelectorBaseSkillId", "SelectorEffectiveSkillId"}
                if target == "projectiles":
                    available.add("SelectorLauncherTemplateId")
                if selected - available:
                    raise ValueError(f"{owner}: reference selector fields are unavailable")
            elif modifier["CandidateReferenceId"]:
                raise ValueError(f"{owner}: CandidateReferenceId requires ParameterReferenceCandidate target")
            if kind == 1:
                if selected:
                    raise ValueError(f"{owner}: attribute modifiers cannot use parameter selectors")
                if not modifier["TargetId"]:
                    raise ValueError(f"{owner}: attribute target requires an attr_types ID")
                graph.check_value(owner, modifier, "TargetId", "attr_types")
            graph.check_value(owner, modifier, "MagnitudeAttributeTypeId", "attr_types")
            if modifier.get("AttributeSourceRole", 0) not in (0, 1, 2):
                raise ValueError(f"{owner}: invalid AttributeSourceRole")
            if modifier.get("MagnitudeSourceType", 0) == 2:
                graph.check_value(owner, modifier, "MagnitudeAttributeTypeId", "attr_types", True)
                if modifier.get("EvaluationPolicy", 0) != 1:
                    raise ValueError(f"{owner}: attribute magnitude source requires OnApplySnapshot")
            elif modifier.get("AttributeSourceRole", 0) != 0:
                raise ValueError(f"{owner}: AttributeSourceRole requires Attribute magnitude source")
            if modifier.get("MagnitudeSourceType", 0) == 3:
                if kind not in (1, 4):
                    raise ValueError(f"{owner}: TimeDecay requires numeric attribute or parameter target")
                if modifier.get("EvaluationPolicy", 0) != 0:
                    raise ValueError(f"{owner}: TimeDecay requires Realtime evaluation")
                if not math.isfinite(modifier.get("MagnitudeDuration", 0)) or modifier["MagnitudeDuration"] <= 0:
                    raise ValueError(f"{owner}: TimeDecay duration must be positive seconds")
                curve = modifier.get("MagnitudeCurve") or []
                if curve and (len(curve) < 2 or len(curve) > 64 or
                              any(not math.isfinite(value) or value < 0 for value in curve)):
                    raise ValueError(f"{owner}: TimeDecay curve requires 2-64 finite nonnegative samples")
            if modifier.get("MagnitudeSourceType", 0) == 6:
                if modifier.get("EvaluationPolicy", 0) != 1:
                    raise ValueError(f"{owner}: formula requires OnApplySnapshot")
                terms = modifier.get("FormulaTerms") or []
                if len(terms) > 8:
                    raise ValueError(f"{owner}: formula exceeds 8 attribute terms")
                if not all(math.isfinite(modifier[field]) for field in
                           ("FormulaConstant", "FormulaLevelCoefficient")):
                    raise ValueError(f"{owner}: formula contains a non-finite constant")
                for index, term in enumerate(terms):
                    graph.check_value(f"{owner}/FormulaTerms/{index}", term,
                                      "AttributeTypeId", "attr_types", True)
                    if term["ActorRole"] not in (0, 1, 2) or not math.isfinite(term["Coefficient"]):
                        raise ValueError(f"{owner}/FormulaTerms/{index}: invalid role or coefficient")
            elif modifier.get("FormulaTerms") or modifier.get("FormulaConstant") or modifier.get("FormulaLevelCoefficient"):
                raise ValueError(f"{owner}: formula fields require LinearFormula source")
            if kind == 4:
                graph.check_value(owner, modifier, "TargetId", "modifier_parameters", True)
                parameter = graph.index["modifier_parameters"][modifier["TargetId"]]
                if parameter["ValueKind"] == 1:
                    raise ValueError(f"{owner}: reference IDs must use a typed candidate")
                stage_fields = {
                    1: {"SelectorSlot", "SelectorBaseSkillId", "SelectorEffectiveSkillId"},
                    2: {"SelectorSlot", "SelectorBaseSkillId", "SelectorEffectiveSkillId", "SelectorLauncherTemplateId", "SelectorProjectileTemplateId"},
                    5: {"SelectorSlot", "SelectorBaseSkillId", "SelectorEffectiveSkillId", "SelectorLauncherTemplateId", "SelectorProjectileTemplateId"},
                    6: {"SelectorSlot", "SelectorBaseSkillId", "SelectorEffectiveSkillId", "SelectorLauncherTemplateId", "SelectorProjectileTemplateId"},
                    7: {"SelectorSlot", "SelectorBaseSkillId", "SelectorEffectiveSkillId", "SelectorProjectileTemplateId"},
                    8: {"SelectorSlot", "SelectorBaseSkillId", "SelectorEffectiveSkillId", "SelectorSummonTemplateId"},
                    4: {"SelectorSlot", "SelectorBaseSkillId", "SelectorEffectiveSkillId", "SelectorProjectileTemplateId", "SelectorAreaTemplateId"},
                    3: {"SelectorSlot", "SelectorBaseSkillId", "SelectorEffectiveSkillId", "SelectorProjectileTemplateId", "SelectorBuffTemplateId"},
                }
                if parameter["Domain"] == 1 and parameter["Key"] == "SkillId":
                    raise ValueError(f"{owner}: SkillId is resolved by slot candidates, not numeric modifiers")
                unavailable = selected - stage_fields.get(parameter["ResolveAt"], set())
                if unavailable:
                    raise ValueError(f"{owner}: selector fields {sorted(unavailable)} "
                                     f"are unavailable at parameter {modifier['TargetId']} consumption")
                if type(modifier["Op"]) is not int or not 0 <= modifier["Op"] <= 3:
                    raise ValueError(f"{owner}: invalid modifier operation")
                if not parameter["OpMask"] & (1 << modifier["Op"]):
                    raise ValueError(f"{owner}: operation is not allowed for parameter {modifier['TargetId']}")

    def normalize(self, table, row):
        for modifier in row.get("Modifiers") or []:
            if modifier.get("MagnitudeContextKey") == "":
                modifier["MagnitudeContextKey"] = None
            if modifier.get("MagnitudeCurve") == []:
                modifier["MagnitudeCurve"] = None
        return row

    def validate(self, context):
        published = context.published_roots[0] / "moba/modifier_parameters.json"
        validate_modifier_parameters(context.tables["modifier_parameters"],
                                     json.loads(published.read_text(encoding="utf-8-sig"))
                                     if published.exists() else None)
        validate_consumer_contracts(context.tables["modifier_parameters"])
        validate_modifier_template_ids(context.tables["modifier_templates"],
                                       context.tables["modifier_parameters"])

    def generate(self, context):
        return {"MobaSkillParamModifierKeys.g.cs":
                generate_parameter_keys(context.tables["modifier_parameters"])}
