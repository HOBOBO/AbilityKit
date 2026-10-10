"""Validate the Excel modifier parameter catalog and generate runtime keys."""

import re


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

# A catalog entry is publishable only after its domain adapter has a named consumer.
# Values are (domain, key, phase, consumer); SkillId is retired in favor of slot candidates.
CONSUMER_CONTRACTS = {
    10001: ("Skill", "SkillId", "Action", "MobaSkillSlotCandidateService"),
    10002: ("Skill", "ResourceCost", "Action", "SkillCastPreparationService"),
    10003: ("Skill", "CooldownMs", "Action", "SkillCastPreparationService"),
    10004: ("Skill", "CastRange", "Action", "SkillCastPreparationService"),
    20001: ("Buff", "DurationMs", "BuffApply", "BuffApplyFlow"),
    30001: ("Emitter", "LauncherId", "Launch", "ShootProjectilePlanActionModule"),
    30002: ("Emitter", "CountPerShot", "Emit", "MobaModifierProjectileSpawnPatternProvider"),
    30003: ("Emitter", "FanAngleDeg", "Emit", "MobaModifierProjectileSpawnPatternProvider"),
    30004: ("Emitter", "DurationMs", "Launch", "ShootProjectilePlanActionModule"),
    30005: ("Emitter", "IntervalMs", "Launch", "MobaProjectileService"),
    40001: ("Projectile", "ProjectileId", "Launch", "ShootProjectilePlanActionModule"),
    40002: ("Projectile", "Speed", "ProjectileSpawn", "MobaProjectileService,MobaModifierProjectileSpawnPatternProvider"),
    40003: ("Projectile", "LifetimeMs", "ProjectileSpawn", "MobaProjectileService,MobaModifierProjectileSpawnPatternProvider"),
    40004: ("Projectile", "PierceHits", "ProjectileSpawn", "MobaProjectileService,MobaModifierProjectileSpawnPatternProvider"),
    50001: ("Aoe", "Radius", "AreaSpawn", "SpawnAreaPlanActionModule"),
    50002: ("Aoe", "DurationMs", "AreaSpawn", "SpawnAreaPlanActionModule"),
    50003: ("Aoe", "IntervalMs", "AreaSpawn", "SpawnAreaPlanActionModule"),
    60001: ("Summon", "SummonId", "SummonCreate", "SpawnSummonPlanActionModule"),
    60002: ("Summon", "LifetimeMs", "SummonCreate", "MobaSummonService"),
    60003: ("Summon", "InheritRatioScale", "SummonCreate", "MobaSummonService"),
    70001: ("Damage", "RequestedBase", "Hit", "GiveDamagePlanActionModule"),
}


def validate_modifier_parameters(rows, published=None):
    ids, names, packed = set(), set(), set()
    for row in rows:
        identity = row["Id"]
        domain = DOMAIN_NAMES.get(row["Domain"])
        name = row["Key"]
        category, subcategory = row["Category"], row["SubCategory"]
        kind, mask = row["ValueKind"], row["OpMask"]
        if type(identity) is not int or identity <= 0 or identity in ids:
            raise ValueError(f"modifier_parameters: duplicate or invalid ID {identity}")
        if domain is None or not isinstance(name, str) or not re.fullmatch(r"[A-Z][A-Za-z0-9]*", name):
            raise ValueError(f"modifier_parameters/{identity}: invalid domain or key")
        lower, upper = PARAMETER_ID_RANGES[row["Domain"]]
        if not lower <= identity <= upper:
            raise ValueError(f"modifier_parameters/{identity}: ID outside {domain} allocation {lower}-{upper}")
        if ((domain, name) in names or type(category) is not int or
                type(subcategory) is not int or not 1 <= category <= 255 or
                not 1 <= subcategory <= 255):
            raise ValueError(f"modifier_parameters/{identity}: duplicate name or invalid key bits")
        if (category, subcategory) in packed:
            raise ValueError(f"modifier_parameters/{identity}: duplicate ModifierKey")
        if kind not in VALUE_KINDS or row["ResolveAt"] not in RESOLVE_PHASES:
            raise ValueError(f"modifier_parameters/{identity}: invalid type or resolution phase")
        if type(mask) is not int or not 1 <= mask <= 15 or kind == 1 and mask != 4:
            raise ValueError(f"modifier_parameters/{identity}: invalid operation mask")
        if type(row["TriggerWritable"]) is not bool or kind == 1 and row["TriggerWritable"]:
            raise ValueError(f"modifier_parameters/{identity}: invalid trigger permission")
        ids.add(identity)
        names.add((domain, name))
        packed.add((category, subcategory))
    for old_id, new_id in PARAMETER_ID_MIGRATION.items():
        expected = LEGACY_KEYS[old_id]
        expected_domain = "Emitter" if old_id in EMITTER_LEGACY_IDS else expected[0]
        row = next((item for item in rows if item["Id"] == new_id), None)
        actual = None if row is None else (DOMAIN_NAMES.get(row["Domain"]), row["Key"], row["Category"],
                                           row["SubCategory"], row["ValueKind"])
        if actual != (expected_domain, *expected[1:]):
            raise ValueError(f"modifier_parameters/{new_id}: migrated key ABI cannot change")
    for previous in published or ():
        stable = ("Domain", "Key", "Category", "SubCategory", "ValueKind")
        old_id = previous["Id"]
        if old_id in PARAMETER_ID_MIGRATION:
            expected = LEGACY_KEYS[old_id]
            original = (DOMAIN_NAMES.get(previous["Domain"]), previous["Key"],
                        previous["Category"], previous["SubCategory"], previous["ValueKind"])
            if original != expected:
                raise ValueError(f"modifier_parameters/{old_id}: published parameter ABI cannot change")
            continue
        row = next((item for item in rows if item["Id"] == old_id), None)
        if row is None or any(row[field] != previous[field] for field in stable):
            raise ValueError(f"modifier_parameters/{previous['Id']}: published parameter ABI cannot change")


def validate_consumer_contracts(rows):
    for row in rows:
        identity = row["Id"]
        contract = CONSUMER_CONTRACTS.get(identity)
        actual = (DOMAIN_NAMES.get(row["Domain"]), row["Key"], RESOLVE_PHASES.get(row["ResolveAt"]))
        if contract is None or actual != contract[:3]:
            raise ValueError(f"modifier_parameters/{identity}: no matching consumer contract for {actual}")
        consumers = contract[3].split(",")
        if len(consumers) != len(set(consumers)) or any(
                re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", name) is None for name in consumers):
            raise ValueError(f"modifier_parameters/{identity}: invalid consumer type names")


def validate_modifier_template_ids(rows, parameters):
    parameter_domains = {row["Id"]: DOMAIN_NAMES[row["Domain"]] for row in parameters}
    seen = set()
    for row in rows:
        identity = row["Id"]
        target_kind = row["TargetKind"]
        if target_kind == 1:
            domain = "Attribute"
        elif target_kind == 5:
            domain = "Skill"
        elif target_kind in (4, 6):
            domain = parameter_domains.get(row["TargetId"])
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
        "using AbilityKit.Modifiers;", "", "namespace AbilityKit.Demo.Moba.Services", "{",
        "    public static class MobaSkillParamModifierKeys", "    {",
    ]
    for domain_id, domain in DOMAIN_NAMES.items():
        members = [row for row in rows if row["Domain"] == domain_id]
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
        lines.append(f"                case {row['Id']}: key = {DOMAIN_NAMES[row['Domain']]}.{row['Key']}; return true;")
    lines.extend(("                default: key = ModifierKey.None; return false;", "            }", "        }", "",
                  "        public static bool TryGetParameterId(ModifierKey key, out int parameterId)",
                  "        {"))
    for row in sorted(rows, key=lambda item: item["Id"]):
        lines.append(f"            if (key.Equals({DOMAIN_NAMES[row['Domain']]}.{row['Key']})) {{ parameterId = {row['Id']}; return true; }}")
    lines.extend(("            parameterId = 0; return false;", "        }", "",
                  "        public static bool TryGetResolveStage(ModifierKey key, out MobaParameterStage stage)",
                  "        {"))
    for row in sorted(rows, key=lambda item: item["Id"]):
        stage = RUNTIME_STAGES[RESOLVE_PHASES[row["ResolveAt"]]]
        lines.append(f"            if (key.Equals({DOMAIN_NAMES[row['Domain']]}.{row['Key']})) {{ stage = MobaParameterStage.{stage}; return true; }}")
    lines.extend(("            stage = MobaParameterStage.None; return false;", "        }", "",
                  "        public static bool TryGetConsumerTypeName(int parameterId, out string typeName)",
                  "        {", "            switch (parameterId)", "            {"))
    for row in sorted(rows, key=lambda item: item["Id"]):
        consumer = CONSUMER_CONTRACTS[row["Id"]][3]
        lines.append(f'                case {row["Id"]}: typeName = "{consumer}"; return true;')
    lines.extend(("                default: typeName = null; return false;", "            }", "        }", "",
                  "        public static bool IsTriggerWritable(int parameterId)", "        {",
                  "            switch (parameterId)", "            {"))
    for row in sorted(rows, key=lambda item: item["Id"]):
        if row["TriggerWritable"]:
            lines.append(f"                case {row['Id']}: return true;")
    lines.extend(("                default: return false;", "            }", "        }", "",
                  "        public static bool SupportsOperation(int parameterId, int op)", "        {",
                  "            if (op < 0 || op > 3) return false;", "            switch (parameterId)", "            {"))
    for row in sorted(rows, key=lambda item: item["Id"]):
        lines.append(f"                case {row['Id']}: return ({row['OpMask']} & (1 << op)) != 0;")
    lines.extend(("                default: return false;", "            }", "        }", "    }", "}", ""))
    return "\n".join(lines)
