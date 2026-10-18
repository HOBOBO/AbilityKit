"""Modifier catalog validation, definition binding and generated parameter keys."""

import json
import math
import re

from modifier_parameters import (derive_operation_mask, domain_name_for_parameter_id,
                                 generate_parameter_keys, validate_consumer_contracts,
                                 validate_modifier_parameters, validate_modifier_template_ids)


# modifier_definitions owns the semantic half of a modifier; modifier_templates only records
# which lifecycle mounts it. Templates merge their definition back in at export time so the
# published JSON keeps the historical flat shape while authoring stays split.
DEFINITION_ID_RANGES = {
    "Attribute": (1000001, 1999999), "Skill": (2000001, 2999999),
    "Emitter": (3000001, 3999999), "Projectile": (4000001, 4999999),
    "Aoe": (5000001, 5999999), "Summon": (6000001, 6999999),
    "Damage": (7000001, 7999999), "Buff": (8000001, 8999999),
}
MOUNT_BY_TARGET_KIND = {1: "Actor", 5: "Skill"}
MOUNT_BY_DOMAIN = {"Skill": "Skill", "Buff": "Buff", "Emitter": "Emitter",
                   "Projectile": "Projectile", "Aoe": "Aoe", "Summon": "Summon",
                   "Damage": "Damage"}
STACKING_POLICIES = {0, 1, 2}
MAGNITUDE_SOURCE_TYPES = {0, 1, 2, 3, 4, 5, 6}
ATTRIBUTE_SOURCE_ROLES = {0, 1, 2}
TARGET_KINDS = {1, 4, 5, 6}
CONFLICT_GROUPS = {0, 1, 2, 3}
MOUNT_POINTS = {0: None, 1: "Actor", 2: "Skill", 3: "Summon", 4: "Emitter",
                5: "Projectile", 6: "Aoe", 7: "Buff", 8: "Damage"}

# Arbitration contract, verified rather than assumed.
#
# The runtime never reads ConflictGroup. Arbitration happens inside AbilityKit.Modifiers, and
# modifiers only ever compete with modifiers sharing the same ModifierKey: MobaSkillParamModifier-
# Service indexes by ModifierKey and the attribute path groups by AttributeId. Two modifiers with
# different keys therefore never meet in one SortByPriority pass and cannot override each other.
#
# ConflictGroup is the authoring-side statement of that invariant -- "these effects write the same
# key, so Priority decides the winner". The exporter validates it against ModifierKey so a
# mislabelled group fails at export time rather than producing a silently wrong ordering.
CONFLICT_GROUP_MOUNTS = {
    1: ("Actor", "Buff"),
    2: ("Skill",),
    3: ("Emitter", "Projectile", "Aoe", "Summon", "Damage"),
}

# EvaluationPolicy is a derived conclusion, never an authored column. It answers one question:
# is this modifier instance's magnitude frozen at apply time? The answer follows from the
# magnitude source, because a magnitude that reads another attribute cannot be frozen without
# changing what the modifier means mid-flight.
#   Attribute / LinearFormula -> OnApplySnapshot: sampled once, then replayed from the capture
#                               store so Buff refresh, rollback and re-projection stay identical.
#   Fixed / Scalable / TimeDecay / Pipeline / ContextFloat -> Realtime: recomputed from the
#                               continuous instance's own state, which is already deterministic.
#
# This is deliberately NOT derived from modifier_parameters.CapturePolicy. That column answers a
# different question -- when the field is read at its ResolveAt consumption site -- and the
# runtime reaches it through the generated field schema, not through this flag. Collapsing the
# two would silently freeze attribute and formula magnitudes that are meant to stay live.
EVALUATION_POLICIES = {0, 1}
SNAPSHOT_MAGNITUDE_SOURCES = {2, 6}  # Attribute, LinearFormula


# The authored modifier bean is structured; consumers still see the flat DTO shape, so the
# projection flattens Target/Modifier/Magnitude/Selectors/Candidate back into named fields.
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

SELECTOR_FIELD_COLUMNS = {
    "Slot": "SelectorSlot",
    "BaseSkillId": "SelectorBaseSkillId",
    "EffectiveSkillId": "SelectorEffectiveSkillId",
    "LauncherTemplateId": "SelectorLauncherTemplateId",
    "ProjectileTemplateId": "SelectorProjectileTemplateId",
    "AreaTemplateId": "SelectorAreaTemplateId",
    "SummonTemplateId": "SelectorSummonTemplateId",
    "BuffTemplateId": "SelectorBuffTemplateId",
}
SELECTOR_COLUMNS = tuple(SELECTOR_FIELD_COLUMNS.values())


def definition_domain(row, parameter_domains):
    """Return the domain that owns a definition, derived from its target."""
    target = row.get("Target") or {}
    kind, identity = target.get("Kind", 0), target.get("Id", 0)
    if kind == 1:
        return "Attribute"
    if kind == 5:
        return "Skill"
    return parameter_domains.get(identity)


def expected_mount(domain, target_kind):
    return MOUNT_BY_TARGET_KIND.get(target_kind) or MOUNT_BY_DOMAIN.get(domain)


def derive_evaluation_policy(magnitude):
    """Return the runtime evaluation policy implied by a definition's magnitude source.

    Magnitudes that read other state must be sampled once so that later changes cannot rewrite an
    in-flight modifier; magnitudes derived from the continuous instance itself stay realtime.
    """
    source = (magnitude or {}).get("SourceType", 0)
    if source not in MAGNITUDE_SOURCE_TYPES:
        raise ValueError(f"unknown magnitude source type {source}")
    return 1 if source in SNAPSHOT_MAGNITUDE_SOURCES else 0


def validate_evaluation_contract(modifier, owner):
    """Validate the combinations consumed by the runtime projection contract."""
    source = modifier.get("MagnitudeSourceType", 0)
    # EvaluationPolicy is derived during publication. Raw workbook rows may carry the
    # default zero value, so validate the derived policy rather than treating that default
    # as an authored override.
    policy = derive_evaluation_policy({"SourceType": source})
    role = modifier.get("AttributeSourceRole", 0)
    if policy not in EVALUATION_POLICIES:
        raise ValueError(f"{owner}: invalid EvaluationPolicy {policy}")
    if source == 2:
        if role not in (0, 1, 2):
            raise ValueError(f"{owner}: invalid AttributeSourceRole {role}")
        if policy == 0 and role != 0:
            raise ValueError(f"{owner}: Realtime attribute magnitude requires Owner source role")
    elif role != 0:
        raise ValueError(f"{owner}: AttributeSourceRole requires Attribute magnitude source")
    if source == 6 and policy == 0:
        raise ValueError(f"{owner}: LinearFormula magnitude requires OnApplySnapshot")


def flatten_definition(definition):
    """Project a modifier definition onto the flat DTO shape templates expose."""
    flat = flatten_modifier({
        "Target": definition.get("Target"),
        "Modifier": definition.get("Value"),
        "Magnitude": definition.get("Magnitude"),
        "Curve": definition.get("Curve"),
        "ContextKey": definition.get("ContextKey"),
        "AttributeSourceRole": definition.get("AttributeSourceRole", 0),
        "Candidate": definition.get("Candidate"),
        "FormulaConstant": definition.get("FormulaConstant", 0),
        "FormulaLevelCoefficient": definition.get("FormulaLevelCoefficient", 0),
        "FormulaTerms": definition.get("FormulaTerms"),
        "Selectors": None,
    })
    return flat


def validate_modifier_definitions(rows, parameter_domains, parameter_schemas=None):
    """Validate the semantic catalog: IDs, keys, mount points, stacking and conditions."""
    ids, keys = set(), set()
    for row in rows:
        identity = row["Id"]
        owner = f"modifier_definitions/{identity}"
        key = row["Key"]
        if type(identity) is not int or identity <= 0 or identity in ids:
            raise ValueError(f"{owner}: duplicate or invalid ID")
        if not isinstance(key, str) or re.fullmatch(r"[A-Z][A-Za-z0-9_]*", key) is None or key in keys:
            raise ValueError(f"{owner}: invalid or duplicate Key {key!r}")
        if row.get("Deprecated") is not False:
            raise ValueError(f"{owner}: deprecated definitions cannot be published")
        if row.get("ConditionSetId") not in (0, None):
            raise ValueError(f"{owner}: ConditionSetId is reserved and must stay empty")
        target = row.get("Target") or {}
        target_kind = target.get("Kind", 0)
        if target_kind not in TARGET_KINDS:
            raise ValueError(f"{owner}: unsupported TargetKind {target_kind}")
        domain = definition_domain(row, parameter_domains)
        if domain not in DEFINITION_ID_RANGES:
            raise ValueError(f"{owner}: target does not resolve to a known modifier domain")
        lower, upper = DEFINITION_ID_RANGES[domain]
        if not lower <= identity <= upper:
            raise ValueError(f"{owner}: {domain} definition ID must be in {lower}-{upper}")
        mount = row.get("MountPoint", 0)
        if mount not in MOUNT_POINTS or expected_mount(domain, target_kind) != MOUNT_POINTS.get(mount):
            raise ValueError(f"{owner}: MountPoint {mount} does not match target domain {domain}")
        value = row.get("Value") or {}
        if type(value.get("Op")) is not int or not 0 <= value["Op"] <= 3:
            raise ValueError(f"{owner}: invalid operation")
        if not math.isfinite(value.get("Value", 0)):
            raise ValueError(f"{owner}: non-finite operation value")
        target_id = target.get("Id", 0)
        schema = (parameter_schemas or {}).get(target_id) if target_kind in (4, 6) else None
        if schema is not None and not schema_value_in_range(schema, value.get("Value", 0)):
            raise ValueError(f"{owner}: value {value.get('Value', 0)} is outside parameter "
                             f"{target_id} range [{schema.get('ValueMin', 0)}, {schema.get('ValueMax', 0)}]")
        magnitude = row.get("Magnitude") or {}
        if magnitude.get("SourceType", 0) not in MAGNITUDE_SOURCE_TYPES:
            raise ValueError(f"{owner}: unknown magnitude source type")
        if row.get("AttributeSourceRole", 0) not in ATTRIBUTE_SOURCE_ROLES:
            raise ValueError(f"{owner}: invalid attribute source role")
        stacking = row.get("StackingPolicy", 0)
        max_stacks = row.get("MaxStacks", 0)
        if stacking not in STACKING_POLICIES or type(max_stacks) is not int or max_stacks < 0:
            raise ValueError(f"{owner}: invalid stacking policy or max stacks")
        if stacking != 2 and max_stacks:
            raise ValueError(f"{owner}: MaxStacks is only meaningful for the Max stacking policy")
        if row.get("ConflictGroup", 0) not in CONFLICT_GROUPS:
            raise ValueError(f"{owner}: invalid conflict group")
        group = row.get("ConflictGroup", 0)
        mount_name = MOUNT_POINTS.get(mount)
        if group and mount_name not in CONFLICT_GROUP_MOUNTS[group]:
            raise ValueError(f"{owner}: conflict group {group} does not cover mount {mount_name}")
        ids.add(identity)
        keys.add(key)
    return ids


def schema_value_in_range(schema, value):
    if not math.isfinite(value):
        return False
    lower = schema.get("ValueMin") or 0
    upper = schema.get("ValueMax") or 0
    return value >= lower and (not upper or value <= upper)


def validate_conflict_group_keys(definitions):
    """Check that a conflict group describes one coherent set of modifier keys.

    ConflictGroup answers "may these effects compete for the same slot?" -- Attribute groups many
    different AttributeIds together on purpose, because they all roll up into one actor-owned
    Category. Priority itself is only compared between modifiers that share a ModifierKey, so the
    invariant worth enforcing is the opposite one: a single ModifierKey must never straddle two
    conflict groups, because those modifiers do compete and the group would be meaningless.
    """
    key_owner = {}
    for row in definitions:
        identity = row["Id"]
        group = row.get("ConflictGroup", 0)
        target = row.get("Target") or {}
        kind, target_id = target.get("Kind", 0), target.get("Id", 0)
        # Attribute targets key on AttributeId; every other kind keys on the resolved parameter.
        key = (kind, target_id)
        owner = f"modifier_definitions/{identity}"
        previous = key_owner.setdefault(key, (group, identity))
        if previous[0] != group:
            raise ValueError(
                f"{owner}: modifier key {key} is shared with definition {previous[1]} "
                f"but sits in conflict group {group} instead of {previous[0]}; "
                "modifiers on the same key always compete")


def bind_template_definitions(templates, definitions):
    """Return templates with their definition's semantics merged back in.

    DefinitionId is authoritative. Priority comes from the mount row and EvaluationPolicy is
    derived from the magnitude source, so neither arbitration nor timing is authored twice.
    """
    catalog = {}
    for definition in definitions:
        flat = flatten_definition(definition)
        flat["EvaluationPolicy"] = derive_evaluation_policy(definition.get("Magnitude"))
        flat["DefinitionId"] = definition["Id"]
        flat["DefinitionKey"] = definition["Key"]
        flat["MountPoint"] = definition.get("MountPoint", 0)
        flat["StackingPolicy"] = definition.get("StackingPolicy", 0)
        flat["MaxStacks"] = definition.get("MaxStacks", 0)
        flat["ConflictGroup"] = definition.get("ConflictGroup", 0)
        catalog[definition["Id"]] = flat
    bound = []
    for template in templates:
        identity = template["Id"]
        definition_id = template.get("DefinitionId", 0)
        owner = f"modifier_templates/{identity}"
        if definition_id not in catalog:
            raise ValueError(f"{owner}: missing modifier definition {definition_id}")
        merged = dict(catalog[definition_id])
        merged["Id"] = identity
        merged["Kind"] = template["Kind"]
        merged["Priority"] = template.get("Priority", 0) or 0
        for column in SELECTOR_COLUMNS:
            merged[column] = 0
        for entry in parse_selectors(template.get("Selectors")):
            merged[SELECTOR_FIELD_COLUMNS[entry["Field"]]] = entry["Id"]
        bound.append(merged)
    return bound


def parse_selectors(text):
    """Parse the authored `Field|Id;Field|Id` selector list into selector entries.

    Luban cannot express a list of beans whose own separator differs from the outer
    separator without mangling multi-entry cells, so the authored column stays a plain
    string and both projections parse it the same way.
    """
    entries = []
    for token in (text or "").split(";"):
        token = token.strip()
        if not token:
            continue
        field, _, value = token.partition("|")
        field = field.strip()
        if field not in SELECTOR_FIELD_COLUMNS:
            raise ValueError(f"unknown modifier selector field {field!r}")
        entries.append({"Field": field, "Id": int(value or 0)})
    return entries


def flatten_modifier(modifier):
    """Project one structured modifier bean into the flat DTO shape consumers expect."""
    target = modifier.get("Target") or {}
    value = modifier.get("Modifier") or {}
    magnitude = modifier.get("Magnitude") or {}
    candidate = modifier.get("Candidate") or {}
    flat = {
        "TargetKind": target.get("Kind", 0),
        "TargetId": target.get("Id", 0),
        "Op": value.get("Op", 0),
        "Value": value.get("Value", 0),
        # Priority is authored on the mount row; the definition's Value bean no longer carries it.
        "Priority": 0,
        "MagnitudeSourceType": magnitude.get("SourceType", 0),
        "MagnitudeBaseValue": magnitude.get("BaseValue", 0),
        "MagnitudeCoefficient": magnitude.get("Coefficient", 0),
        "MagnitudeDuration": magnitude.get("DurationSec", 0),
        "MagnitudeDecayType": magnitude.get("DecayType", 0),
        "MagnitudeAttributeTypeId": magnitude.get("AttributeTypeId", 0),
        "MagnitudeContextKey": modifier.get("ContextKey"),
        "MagnitudeCurve": modifier.get("Curve") or [],
        "EvaluationPolicy": modifier.get("EvaluationPolicy", 0),
        "AttributeSourceRole": modifier.get("AttributeSourceRole", 0),
        "CandidateSkillId": candidate.get("SkillId", 0),
        "CandidateReferenceId": candidate.get("ReferenceId", 0),
        "FormulaConstant": modifier.get("FormulaConstant", 0),
        "FormulaLevelCoefficient": modifier.get("FormulaLevelCoefficient", 0),
        "FormulaTerms": modifier.get("FormulaTerms") or [],
    }
    for column in SELECTOR_COLUMNS:
        flat[column] = 0
    for entry in parse_selectors(modifier.get("Selectors")):
        column = SELECTOR_FIELD_COLUMNS.get(entry.get("Field"))
        if column:
            flat[column] = entry.get("Id", 0)
    return flat


def project_modifier_bindings(tables, resolve_tags):
    templates = {}
    for row in tables["modifier_templates"]:
        identity = row["Id"]
        if identity in templates:
            raise ValueError(f"modifier_templates: duplicate ID {identity}")
        templates[identity] = row["Kind"]
    for name, kind in (("buffs", 0), ("continuous_processes", 1)):
        for row in tables[name]:
            owner = f"{name}/{row['Id']}"
            # ModifierIds stay in the published row; Buff/ContinuousProcess loaders resolve the
            # structured definitions on demand instead of carrying a pre-baked Modifiers array.
            ids = row["ModifierIds"]
            if len(ids) != len(set(ids)):
                raise ValueError(f"{owner}: duplicate modifier ID")
            for identity in ids:
                template_kind = templates[identity]
                if template_kind != kind:
                    raise ValueError(f"{owner}: wrong-kind modifier {identity}")
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
        tables = context.tables
        parameter_domains = {row["Id"]: domain_name_for_parameter_id(row["Id"])
                             for row in tables["modifier_parameters"]}
        parameter_schemas = {row["Id"]: row for row in tables["modifier_parameters"]}
        validate_modifier_definitions(tables["modifier_definitions"], parameter_domains, parameter_schemas)
        bound = bind_template_definitions(tables["modifier_templates"],
                                          tables["modifier_definitions"])
        # Publishing the merged row keeps the runtime ABI untouched: templates are mount-only in
        # the workbook, but consumers still see the historical flat modifier shape.
        by_id = {row["Id"]: row for row in bound}
        templates = tables["modifier_templates"]
        templates[:] = [by_id[row["Id"]] for row in templates]
        project_modifier_bindings(tables, context.state["resolve_tags"])

    def validate_graph(self, graph, tables, external):
        for template in tables["modifier_templates"]:
            # Validate the same flattened shape consumers receive.
            # Projected rows are already flat; raw workbook rows still carry the structured bean.
            modifier = dict(template) if "TargetKind" in template else flatten_modifier(template)
            modifier["Id"] = template["Id"]
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
            elif modifier.get("AttributeSourceRole", 0) != 0:
                raise ValueError(f"{owner}: AttributeSourceRole requires Attribute magnitude source")
            validate_evaluation_contract(modifier, owner)
            if modifier.get("MagnitudeSourceType", 0) == 3:
                if kind not in (1, 4):
                    raise ValueError(f"{owner}: TimeDecay requires numeric attribute or parameter target")
                if not math.isfinite(modifier.get("MagnitudeDuration", 0)) or modifier["MagnitudeDuration"] <= 0:
                    raise ValueError(f"{owner}: TimeDecay duration must be positive seconds")
                curve = modifier.get("MagnitudeCurve") or []
                if curve and (len(curve) < 2 or len(curve) > 64 or
                              any(not math.isfinite(value) or value < 0 for value in curve)):
                    raise ValueError(f"{owner}: TimeDecay curve requires 2-64 finite nonnegative samples")
            if modifier.get("MagnitudeSourceType", 0) == 6:
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
                if domain_name_for_parameter_id(parameter["Id"]) == "Skill" and parameter["Key"] == "SkillId":
                    raise ValueError(f"{owner}: SkillId is resolved by slot candidates, not numeric modifiers")
                unavailable = selected - stage_fields.get(parameter["ResolveAt"], set())
                if unavailable:
                    raise ValueError(f"{owner}: selector fields {sorted(unavailable)} "
                                     f"are unavailable at parameter {modifier['TargetId']} consumption")
                if type(modifier["Op"]) is not int or not 0 <= modifier["Op"] <= 3:
                    raise ValueError(f"{owner}: invalid modifier operation")
                if not derive_operation_mask(parameter["Ops"]) & (1 << modifier["Op"]):
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
        validate_conflict_group_keys(context.tables["modifier_definitions"])

    def references(self):
        return ({"modifier_templates": {"DefinitionId": "modifier_definitions"}}, ())

    def generate(self, context):
        generated = {"MobaSkillParamModifierKeys.g.cs":
                    generate_parameter_keys(context.tables["modifier_parameters"])}
        generated["MobaModifierDefinitions.g.cs"] = generate_modifier_definitions(
            context.tables["modifier_definitions"], context.tables["modifier_templates"])
        return generated


def generate_modifier_definitions(definitions, templates):
    """Emit the definition catalog: stable keys plus a mount -> definition index.

    The runtime resolves semantic data by DefinitionId from modifier_definitions and only reads
    mount scoping from modifier_templates, so equivalent effects are authored exactly once.
    """
    used = {row["DefinitionId"]: 0 for row in templates}
    for row in templates:
        used[row["DefinitionId"]] += 1
    lines = [
        "// <auto-generated>MOBA modifier definitions from J-效果表.xlsx.</auto-generated>",
        "using System.Collections.Generic;",
        "", "namespace AbilityKit.Demo.Moba.Services", "{",
        "    /// <summary>One modifier definition plus every mount that references it.</summary>",
        "    public readonly struct ModifierDefinitionEntry", "    {",
        "        public readonly int Id;",
        "        public readonly string Key;",
        "        public readonly int MountPoint;",
        "        public readonly int StackingPolicy;",
        "        public readonly int MaxStacks;",
        "        public readonly int ConflictGroup;",
        "        public readonly int MountCount;",
        "", "        public ModifierDefinitionEntry(int id, string key, int mountPoint,",
        "            int stackingPolicy, int maxStacks, int conflictGroup, int mountCount)",
        "        {",
        "            Id = id; Key = key; MountPoint = mountPoint;",
        "            StackingPolicy = stackingPolicy; MaxStacks = maxStacks;",
        "            ConflictGroup = conflictGroup; MountCount = mountCount;",
        "        }", "    }", "",
        "    public static class MobaModifierDefinitions", "    {",
        "        /// <summary>Definition entries indexed by DefinitionId; no runtime lookup scans.</summary>",
        "        public static readonly IReadOnlyDictionary<int, ModifierDefinitionEntry> ById =",
        "            new Dictionary<int, ModifierDefinitionEntry>", "        {",
    ]
    for row in sorted(definitions, key=lambda item: item["Id"]):
        lines.append(
            f"            {{ {row['Id']}, new ModifierDefinitionEntry({row['Id']}, "
            f"\"{row['Key']}\", {row['MountPoint']}, {row['StackingPolicy']}, "
            f"{row['MaxStacks']}, {row['ConflictGroup']}, {used.get(row['Id'], 0)}) }},")
    lines += ["        };", "    }", "}"]
    return "\n".join(lines) + "\n"
