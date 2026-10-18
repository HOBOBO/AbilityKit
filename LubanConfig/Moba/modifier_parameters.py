"""Validate the Excel modifier parameter catalog and generate runtime keys."""

import re


VALUE_KINDS = {1: "ReferenceId", 2: "Integer", 3: "Float"}
RESOLVE_PHASES = {1: "Action", 2: "Launch", 3: "BuffApply", 4: "AreaSpawn",
                  5: "Emit", 6: "ProjectileSpawn", 7: "Hit", 8: "SummonCreate"}
RUNTIME_STAGES = {"Action": "CastPrepare", "Launch": "LauncherCreate",
                  "BuffApply": "BuffApply", "AreaSpawn": "AreaSpawn",
                  "Emit": "Emit", "ProjectileSpawn": "ProjectileSpawn",
                  "Hit": "Hit", "SummonCreate": "SummonCreate"}
LEGACY_KEYS = {
    1: ("Projectile", "LauncherId", 30, 1, 1),
    2: ("Projectile", "ProjectileId", 30, 2, 1),
    3: ("Projectile", "CountPerShot", 30, 3, 2),
    4: ("Projectile", "FanAngleDeg", 30, 4, 3),
    5: ("Projectile", "DurationMs", 30, 5, 2),
    6: ("Skill", "SkillId", 40, 1, 1),
    7: ("Summon", "SummonId", 40, 2, 1),
    8: ("Skill", "ResourceCost", 40, 3, 2),
    9: ("Skill", "CooldownMs", 40, 4, 2),
    10: ("Skill", "CastRange", 40, 5, 3),
    11: ("Buff", "DurationMs", 32, 1, 2),
    12: ("Projectile", "Speed", 30, 6, 3),
    13: ("Aoe", "Radius", 31, 1, 3),
    14: ("Aoe", "DurationMs", 31, 2, 2),
    15: ("Damage", "RequestedBase", 33, 1, 3),
    16: ("Summon", "LifetimeMs", 40, 6, 2),
}
PARAMETER_ID_MIGRATION = {
    1: 30001, 2: 40001, 3: 30002, 4: 30003, 5: 30004,
    6: 10001, 7: 60001, 8: 10002, 9: 10003, 10: 10004,
    11: 20001, 12: 40002, 13: 50001, 14: 50002,
    15: 70001, 16: 60002,
}
EMITTER_LEGACY_IDS = {1, 3, 4, 5}

DOMAIN_NAMES = {1: "Skill", 2: "Projectile", 3: "Summon", 4: "Buff", 5: "Aoe", 6: "Damage", 7: "Emitter"}
# The original 1-16 IDs are retired by the audited domain migration below.
PARAMETER_ID_RANGES = {
    1: (10000, 19999),  # Skill
    4: (20000, 29999),  # Buff
    7: (30000, 39999),  # Emitter / launcher
    2: (40000, 49999),  # Projectile
    5: (50000, 59999),  # Aoe
    3: (60000, 69999),  # Summon
    6: (70000, 79999),  # Damage
}
MODIFIER_TEMPLATE_ID_RANGES = {
    "Attribute": (100001, 199999),
    "Buff": (200001, 299999),
    "Skill": (300001, 399999),
    "Emitter": (400001, 499999),
    "Projectile": (500001, 599999),
    "Aoe": (600001, 699999),
    "Summon": (700001, 799999),
    "Damage": (800001, 899999),
}
FIELD_UNITS = {"None": 0, "Point": 1, "Distance": 2, "Ms": 3, "Count": 4,
               "Ratio": 5, "Degrees": 6, "Id": 7, "Percent": 8}
# CapturePolicy answers *when the field is read at its consumption site*, which is a different
# question from a modifier's EvaluationPolicy (whether a modifier instance's magnitude is frozen).
#   LiveProperty   -> read live on every resolve; an already-running Buff that changes this field
#                     is visible to the next resolve, because nothing caches the computed value.
#   CreateSnapshot -> read once per consuming snapshot; stable inside that snapshot.
#   EventSample    -> sampled when the triggering event fires, so e.g. a launcher change only
#                     affects the next launch and never the projectiles already in flight.
CAPTURE_VALUES = {"CreateSnapshot": 1, "EventSample": 2, "LiveProperty": 3}
AGGREGATION_VALUES = {"Flat": 0, "Percent": 1}
OPERATION_NAMES = ("Add", "Mul", "Override", "PercentAdd")
# Reference IDs carry no numeric range; every other unit describes a numeric quantity.
NUMERIC_UNITS = {1, 2, 3, 4, 5, 6, 8}
# Author-facing schema columns; Category/SubCategory stay locked ModifierKey ABI.
SCHEMA_COLUMNS = ("Unit", "ValueMin", "ValueMax", "CapturePolicy", "Deprecated")


OPERATION_VALUES = {"Add": 0, "Mul": 1, "Override": 2, "PercentAdd": 3}


UNIT_NAMES = {value: name for name, value in FIELD_UNITS.items()}
CAPTURE_NAMES = {value: name for name, value in CAPTURE_VALUES.items()}


def derive_operation_mask(ops):
    """Derive the runtime OpMask bit field from the author-facing Ops list.

    Accepts the enum values Luban emits (a list of ints) or the readable names an
    author may type in the sheet. Returns None when the list cannot be trusted.
    """
    if isinstance(ops, (list, tuple)):
        values = list(ops)
        if not values or len(values) != len(set(values)):
            return None
        if all(type(value) is int for value in values):
            if any(value not in OPERATION_VALUES.values() for value in values):
                return None
            return sum(1 << value for value in values)
        values = [str(value) for value in values]
    else:
        values = [item for item in str(ops).split(",") if item]
    if not values or len(values) != len(set(values)):
        return None
    if any(value not in OPERATION_VALUES for value in values):
        return None
    return sum(1 << OPERATION_VALUES[value] for value in values)


def validate_field_schema(row, identity, kind, require_capture=True):
    """Return a reason string when a field row violates the shared modifier field schema.

    Attributes and modifier parameters share every column except CapturePolicy, which
    only applies to domain parameters resolved at a lifecycle stage.
    """
    unit = row.get("Unit", 0)
    if unit not in set(FIELD_UNITS.values()):
        return f"unknown unit {unit}"
    if kind == 1:
        if unit != FIELD_UNITS["Id"]:
            return "reference fields must use the Id unit"
        if row.get("ValueMin") or row.get("ValueMax"):
            return "reference fields cannot declare a numeric range"
    elif unit not in NUMERIC_UNITS:
        return "numeric fields must declare a dimensional unit"
    # An absent float cell is normalized to 0, so 0 means "unbounded" on both ends.
    lower, upper = row.get("ValueMin") or 0, row.get("ValueMax") or 0
    if upper and lower > upper:
        return "ValueMin must not exceed a nonzero ValueMax"
    if require_capture:
        capture = row.get("CapturePolicy")
        if capture not in set(CAPTURE_VALUES.values()):
            return f"unknown capture policy {capture}"
    ops = derive_operation_mask(row.get("Ops"))
    if ops is None:
        return "Ops must list at least one distinct known operation"
    if kind == 1 and ops != 4:
        return "reference fields only allow the Override operation"
    # Deprecated only applies to modifier parameters; attributes have no such column.
    if "Deprecated" in row and row["Deprecated"] is not False:
        return "Deprecated fields cannot be published"
    return None


def domain_for_parameter_id(identity):
    """Return the domain enum value owning an ID segment, or None when unallocated."""
    for domain_id, (lower, upper) in PARAMETER_ID_RANGES.items():
        if lower <= identity <= upper:
            return domain_id
    return None


def domain_name_for_parameter_id(identity):
    domain_id = domain_for_parameter_id(identity)
    return None if domain_id is None else DOMAIN_NAMES.get(domain_id)


def validate_modifier_parameters(rows, published=None):
    """Validate the published parameter catalog.

    The domain is implied by the parameter ID segment, so rows only carry the
    author-facing columns plus the locked ModifierKey ABI.
    """
    ids, names, packed = set(), set(), set()
    for row in rows:
        identity = row["Id"]
        if type(identity) is not int or identity <= 0 or identity in ids:
            raise ValueError(f"modifier_parameters: duplicate or invalid ID {identity}")
        domain_id = domain_for_parameter_id(identity)
        domain = DOMAIN_NAMES.get(domain_id)
        name = row["Key"]
        category, subcategory = row["Category"], row["SubCategory"]
        kind = row["ValueKind"]
        if domain is None or not isinstance(name, str) or not re.fullmatch(r"[A-Z][A-Za-z0-9]*", name):
            raise ValueError(f"modifier_parameters/{identity}: unallocated ID segment or invalid key")
        if ((domain, name) in names or type(category) is not int or
                type(subcategory) is not int or not 1 <= category <= 255 or
                not 1 <= subcategory <= 255):
            raise ValueError(f"modifier_parameters/{identity}: duplicate name or invalid key bits")
        if (category, subcategory) in packed:
            raise ValueError(f"modifier_parameters/{identity}: duplicate ModifierKey")
        if kind not in VALUE_KINDS or row["ResolveAt"] not in RESOLVE_PHASES:
            raise ValueError(f"modifier_parameters/{identity}: invalid type or resolution phase")
        derived = derive_operation_mask(row.get("Ops"))
        if derived is None or not 1 <= derived <= 15:
            raise ValueError(f"modifier_parameters/{identity}: invalid operation list")
        if kind == 1 and derived != 4:
            raise ValueError(f"modifier_parameters/{identity}: reference fields only allow Override")
        if type(row["TriggerWritable"]) is not bool or kind == 1 and row["TriggerWritable"]:
            raise ValueError(f"modifier_parameters/{identity}: invalid trigger permission")
        reason = validate_field_schema(row, identity, kind)
        if reason:
            raise ValueError(f"modifier_parameters/{identity}: {reason}")
        ids.add(identity)
        names.add((domain, name))
        packed.add((category, subcategory))
    for old_id, new_id in PARAMETER_ID_MIGRATION.items():
        expected = LEGACY_KEYS[old_id]
        expected_domain = "Emitter" if old_id in EMITTER_LEGACY_IDS else expected[0]
        row = next((item for item in rows if item["Id"] == new_id), None)
        actual = None if row is None else (domain_name_for_parameter_id(new_id), row["Key"],
                                           row["Category"], row["SubCategory"], row["ValueKind"])
        if actual != (expected_domain, *expected[1:]):
            raise ValueError(f"modifier_parameters/{new_id}: migrated key ABI cannot change")
    for previous in published or ():
        stable = ("Key", "Category", "SubCategory", "ValueKind")
        old_id = previous["Id"]
        if old_id in PARAMETER_ID_MIGRATION:
            # Legacy IDs sit outside every allocated segment, so their original domain
            # comes from the audited migration table rather than the ID.
            expected = LEGACY_KEYS[old_id]
            original = (expected[0], previous["Key"], previous["Category"],
                        previous["SubCategory"], previous["ValueKind"])
            if original != expected:
                raise ValueError(f"modifier_parameters/{old_id}: published parameter ABI cannot change")
            continue
        row = next((item for item in rows if item["Id"] == old_id), None)
        if row is None or any(row[field] != previous[field] for field in stable):
            raise ValueError(f"modifier_parameters/{previous['Id']}: published parameter ABI cannot change")


CONSUMER_CONTRACTS = {
    10001: ("Skill", "SkillId", "Action", "MobaSkillSlotCandidateService", "DerivedView"),
    10002: ("Skill", "ResourceCost", "Action", "SkillCastPreparationService", "CreateSnapshot"),
    10003: ("Skill", "CooldownMs", "Action", "SkillCastPreparationService", "CreateSnapshot"),
    10004: ("Skill", "CastRange", "Action", "SkillCastPreparationService", "CreateSnapshot"),
    20001: ("Buff", "DurationMs", "BuffApply", "BuffApplyFlow", "CreateSnapshot"),
    30001: ("Emitter", "LauncherId", "Launch", "ShootProjectilePlanActionModule", "CreateSnapshot"),
    30002: ("Emitter", "CountPerShot", "Emit", "MobaModifierProjectileSpawnPatternProvider", "EventSample"),
    30003: ("Emitter", "FanAngleDeg", "Emit", "MobaModifierProjectileSpawnPatternProvider", "EventSample"),
    30004: ("Emitter", "DurationMs", "Launch", "ShootProjectilePlanActionModule", "CreateSnapshot"),
    30005: ("Emitter", "IntervalMs", "Launch", "MobaProjectileService", "CreateSnapshot"),
    40001: ("Projectile", "ProjectileId", "Launch", "ShootProjectilePlanActionModule", "CreateSnapshot"),
    40002: ("Projectile", "Speed", "ProjectileSpawn", "MobaProjectileService,MobaModifierProjectileSpawnPatternProvider", "CreateSnapshot"),
    40003: ("Projectile", "LifetimeMs", "ProjectileSpawn", "MobaProjectileService,MobaModifierProjectileSpawnPatternProvider", "CreateSnapshot"),
    40004: ("Projectile", "PierceHits", "ProjectileSpawn", "MobaProjectileService,MobaModifierProjectileSpawnPatternProvider", "CreateSnapshot"),
    50001: ("Aoe", "Radius", "AreaSpawn", "SpawnAreaPlanActionModule", "CreateSnapshot"),
    50002: ("Aoe", "DurationMs", "AreaSpawn", "SpawnAreaPlanActionModule", "CreateSnapshot"),
    50003: ("Aoe", "IntervalMs", "AreaSpawn", "SpawnAreaPlanActionModule", "CreateSnapshot"),
    60001: ("Summon", "SummonId", "SummonCreate", "SpawnSummonPlanActionModule", "CreateSnapshot"),
    60002: ("Summon", "LifetimeMs", "SummonCreate", "MobaSummonService", "CreateSnapshot"),
    60003: ("Summon", "InheritRatioScale", "SummonCreate", "MobaSummonService", "CreateSnapshot"),
    70001: ("Damage", "RequestedBase", "Hit", "GiveDamagePlanActionModule", "EventSample"),
}


def validate_consumer_contracts(rows):
    for row in rows:
        identity = row["Id"]
        contract = CONSUMER_CONTRACTS.get(identity)
        actual = (domain_name_for_parameter_id(identity), row["Key"],
                  RESOLVE_PHASES.get(row["ResolveAt"]))
        if contract is None or actual != contract[:3]:
            raise ValueError(f"modifier_parameters/{identity}: no matching consumer contract for {actual}")
        consumers = contract[3].split(",")
        if len(consumers) != len(set(consumers)) or any(
                re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", name) is None for name in consumers):
            raise ValueError(f"modifier_parameters/{identity}: invalid consumer type names")
        timing = contract[4]
        if timing not in ("CreateSnapshot", "EventSample", "LiveProperty", "DerivedView"):
            raise ValueError(f"modifier_parameters/{identity}: invalid consumer timing {timing}")
        if timing != "DerivedView" and timing != CAPTURE_NAMES[row["CapturePolicy"]]:
            raise ValueError(f"modifier_parameters/{identity}: CapturePolicy {CAPTURE_NAMES[row['CapturePolicy']]} "
                             f"does not match consumer timing {timing}")


def validate_modifier_template_ids(rows, parameters):
    parameter_domains = {row["Id"]: domain_name_for_parameter_id(row["Id"])
                         for row in parameters}
    seen = set()
    for row in rows:
        identity = row["Id"]
        # Templates are mount-only in the workbook and flattened by the export projection, so
        # accept either the structured bean or the published flat target fields.
        target = row.get("Target") or {}
        target_kind = row.get("TargetKind", target.get("Kind", 0))
        target_id = row.get("TargetId", target.get("Id", 0))
        if target_kind == 1:
            domain = "Attribute"
        elif target_kind == 5:
            domain = "Skill"
        elif target_kind in (4, 6):
            domain = parameter_domains.get(target_id)
        else:
            domain = None
        if type(identity) is not int or identity in seen or domain not in MODIFIER_TEMPLATE_ID_RANGES:
            raise ValueError(f"modifier_templates/{identity}: invalid ID or target domain")
        lower, upper = MODIFIER_TEMPLATE_ID_RANGES[domain]
        if not lower <= identity <= upper:
            raise ValueError(f"modifier_templates/{identity}: {domain} ID must be in {lower}-{upper}")
        seen.add(identity)


def generate_parameter_keys(rows):
    lines = [
        "// <auto-generated>MOBA modifier parameter keys from J-效果表.xlsx.</auto-generated>",
        "using AbilityKit.Modifiers;",
        "using moba_luban;",
        "", "namespace AbilityKit.Demo.Moba.Services", "{",
        "    public readonly struct ModifierFieldSchema", "    {",
        "        public ModifierFieldSchema(ModifierFieldUnit unit, float min, float max,",
        "            ModifierParameterCapturePolicy capture, int operations, string domain, string displayName)",
        "        {",
        "            Unit = unit; Min = min; Max = max; Capture = capture;",
        "            Operations = operations; Domain = domain; DisplayName = displayName;",
        "        }", "",
        "        public ModifierFieldUnit Unit { get; }",
        "        public float Min { get; }",
        "        public float Max { get; }",
        "        public ModifierParameterCapturePolicy Capture { get; }",
        "        public int Operations { get; }",
        "        public string Domain { get; }",
        "        public string DisplayName { get; }", "",
        "        public bool HasUpperBound => Max > 0f;",
        "        public bool IsMutable(int operation) => (Operations & (1 << operation)) != 0;",
        "        public bool IsInRange(float value) => value >= Min && (!HasUpperBound || value <= Max);",
        "    }", "",
        "    public static class MobaSkillParamModifierKeys", "    {",
    ]
    for domain_id, domain in DOMAIN_NAMES.items():
        members = [row for row in rows if domain_for_parameter_id(row["Id"]) == domain_id]
        if not members:
            continue
        lines.extend((f"        public static class {domain}", "        {"))
        for row in sorted(members, key=lambda item: item["Id"]):
            lines.append(f"            public static readonly ModifierKey {row['Key']} = ModifierKey.Create({row['Category']}, {row['SubCategory']});")
        lines.extend(("", "            public static class Ids", "            {"))
        for row in sorted(members, key=lambda item: item["Id"]):
            lines.append(f"                public const int {row['Key']} = {row['Id']};")
        lines.append("            }")
        lines.append("        }")
    lines.extend(("", "        public static bool TryResolve(int parameterId, out ModifierKey key)",
                  "        {", "            switch (parameterId)", "            {"))
    for row in sorted(rows, key=lambda item: item["Id"]):
        lines.append(f"                case {row['Id']}: key = {domain_name_for_parameter_id(row['Id'])}.{row['Key']}; return true;")
    lines.extend(("                default: key = ModifierKey.None; return false;", "            }", "        }", "",
                  "        public static bool TryGetParameterId(ModifierKey key, out int parameterId)",
                  "        {"))
    for row in sorted(rows, key=lambda item: item["Id"]):
        lines.append(f"            if (key.Equals({domain_name_for_parameter_id(row['Id'])}.{row['Key']})) {{ parameterId = {row['Id']}; return true; }}")
    lines.extend(("            parameterId = 0; return false;", "        }", "",
                  "        public static bool TryGetResolveStage(ModifierKey key, out MobaParameterStage stage)",
                  "        {"))
    for row in sorted(rows, key=lambda item: item["Id"]):
        stage = RUNTIME_STAGES[RESOLVE_PHASES[row["ResolveAt"]]]
        lines.append(f"            if (key.Equals({domain_name_for_parameter_id(row['Id'])}.{row['Key']})) {{ stage = MobaParameterStage.{stage}; return true; }}")
    lines.extend(("            stage = MobaParameterStage.None; return false;", "        }", "",
                  "        public static bool TryGetConsumerTypeName(int parameterId, out string typeName)",
                  "        {", "            switch (parameterId)", "            {"))
    for row in sorted(rows, key=lambda item: item["Id"]):
        consumer = CONSUMER_CONTRACTS[row["Id"]][3]
        lines.append(f'                case {row["Id"]}: typeName = "{consumer}"; return true;')
    lines.extend(("                default: typeName = null; return false;", "            }", "        }", "",
                  "        public static bool TryGetConsumerSampleTiming(int parameterId, out MobaModifierSampleTiming timing)",
                  "        {", "            switch (parameterId)", "            {"))
    for row in sorted(rows, key=lambda item: item["Id"]):
        timing = CONSUMER_CONTRACTS[row["Id"]][4]
        lines.append(f"                case {row['Id']}: timing = MobaModifierSampleTiming.{timing}; return true;")
    lines.extend(("                default: timing = default; return false;", "            }", "        }", "",
                  "        public static bool TryGetFieldSchema(int parameterId, out ModifierFieldSchema schema)",
                  "        {", "            switch (parameterId)", "            {"))
    for row in sorted(rows, key=lambda item: item["Id"]):
        domain = domain_name_for_parameter_id(row["Id"])
        lower = row.get("ValueMin") or 0
        upper = row.get("ValueMax") or 0
        unit = UNIT_NAMES[row["Unit"]]
        capture = CAPTURE_NAMES[row["CapturePolicy"]]
        mask = derive_operation_mask(row["Ops"])
        label = row["DisplayName"].replace(chr(34), "")
        lines.append(f'                case {row["Id"]}: schema = new ModifierFieldSchema(ModifierFieldUnit.{unit}, {lower}f, {upper}f, ModifierParameterCapturePolicy.{capture}, {mask}, "{domain}", "{label}"); return true;')
    lines.extend(("                default: schema = default; return false;", "            }", "        }", "",
                  "        public static bool TryValidateValue(int parameterId, float value, out string error)",
                  "        {", "            if (!TryGetFieldSchema(parameterId, out var schema))",
                  "            { error = \"parameter id is not registered\"; return false; }",
                  "            if (float.IsNaN(value) || float.IsInfinity(value))",
                  "            { error = \"value must be finite\"; return false; }",
                  "            if (!schema.IsInRange(value))",
                  "            { error = $\"value {value} is outside [{schema.Min}, {schema.Max}]\"; return false; }",
                  "            error = null; return true;", "        }", ""))
    lines.extend(("        public static bool IsTriggerWritable(int parameterId)", "        {",
                  "            switch (parameterId)", "            {"))
    for row in sorted(rows, key=lambda item: item["Id"]):
        if row["TriggerWritable"]:
            lines.append(f"                case {row['Id']}: return true;")
    lines.extend(("                default: return false;", "            }", "        }", "",
                  "        public static bool SupportsOperation(int parameterId, int op)", "        {",
                  "            if (op < 0 || op > 3) return false;", "            switch (parameterId)", "            {"))
    for row in sorted(rows, key=lambda item: item["Id"]):
        mask = derive_operation_mask(row["Ops"])
        lines.append(f"                case {row['Id']}: return ({mask} & (1 << op)) != 0;")
    lines.extend(("                default: return false;", "            }", "        }", "    }", "}", ""))
    return "\n".join(lines)
