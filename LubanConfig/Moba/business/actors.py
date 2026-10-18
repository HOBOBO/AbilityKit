"""Character and attribute template references."""

from modifier_parameters import validate_field_schema


ATTRIBUTE_KEYS = (
    "HP", "MAX_HP", "EXTRA_HP", "PHYSICS_ATTACK", "MAGIC_ATTACK",
    "EXTRA_PHYSICS_ATTACK", "EXTRA_MAGIC_ATTACK", "PHYSICS_DEFENSE",
    "MAGIC_DEFENSE", "MANA", "MAX_MANA", "CRITICAL_R", "ATTACK_SPEED_R",
    "COOLDOWN_REDUCE_R", "PHYSICS_PENETRATION_R", "MAGIC_PENETRATION_R",
    "MOVE_SPEED", "PHYSICS_BLOODSUCKING_R", "MAGIC_BLOODSUCKING_R",
    "ATTACK_RANGE", "PER_SECOND_BLOOD_R", "PER_SECOND_MANA_R", "RESILIENCE_R",
)


def validate_attribute_types(rows):
    """Validate the attribute catalog against the runtime enum and the shared field schema."""
    definitions = {row["Id"]: row for row in rows}
    if len(definitions) != len(rows):
        raise ValueError("attr_types: duplicate ID")
    if set(definitions) != set(range(1, len(ATTRIBUTE_KEYS) + 1)):
        raise ValueError("attr_types: IDs must match BattleAttributeType 1-23")
    for identity, key in enumerate(ATTRIBUTE_KEYS, 1):
        row = definitions[identity]
        if row["Key"] != key:
            raise ValueError(f"attr_types/{identity}: Key must match BattleAttributeType.{key}")
        if row["ValueKind"] not in (0, 1):
            raise ValueError(f"attr_types/{identity}: invalid ValueKind")
        reason = validate_field_schema(row, identity, kind=2, require_capture=False)
        if reason:
            raise ValueError(f"attr_types/{identity}: {reason}")


def project_loadouts(tables):
    def unique_rows(name):
        indexed = {}
        for row in tables[name]:
            identity = row["Id"]
            if identity in indexed:
                raise ValueError(f"{name}: duplicate ID {identity}")
            indexed[identity] = row
        return indexed

    characters = unique_rows("characters")
    attributes = unique_rows("attribute_templates")
    character_loadouts = {}
    for character in characters.values():
        template_id = character["AttributeTemplateId"]
        loadout = (character["SkillIds"], character["PassiveSkillIds"])
        if template_id in character_loadouts and character_loadouts[template_id] != loadout:
            raise ValueError(f"attribute_templates/{template_id}: characters disagree on skill loadout")
        character_loadouts[template_id] = loadout
    for identity, attribute in attributes.items():
        active, passive = character_loadouts.get(identity, ([], []))
        attribute["ActiveSkills"] = active
        attribute["PassiveSkills"] = passive


class Rule:
    def project(self, context):
        validate_attribute_types(context.tables["attr_types"])
        project_loadouts(context.tables)

    def references(self):
        return ({
            "characters": {"AttributeTemplateId": "attribute_templates", "ModelId": "models",
                           "SkillIds": "skills", "PassiveSkillIds": "passive_skills"},
            "attribute_templates": {"BasicAttackSkillId": "skills", "ActiveSkills": "skills",
                                    "PassiveSkills": "passive_skills"},
        }, {("characters", "AttributeTemplateId"),
            ("attribute_templates", "BasicAttackSkillId")})
