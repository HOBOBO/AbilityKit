"""Projection of Luban enum values into the runtime projectile document."""


def validate_collision_rules(rows):
    fields = {"Role", "MainType", "UnitSubType", "Relation", "Response"}
    ranges = {"Role": (1, 3), "MainType": (0, 5), "UnitSubType": (0, 7),
              "Relation": (0, 3), "Response": (0, 2)}
    for row in rows:
        rules = row.get("CollisionRules")
        if rules is None:
            rules = []
        owner = f"projectiles/{row['Id']}/CollisionRules"
        if not isinstance(rules, list) or len(rules) > 256:
            raise ValueError(f"{owner}: expected an array with at most 256 rules")
        for index, rule in enumerate(rules):
            path = f"{owner}/{index}"
            if not isinstance(rule, dict) or not {"Role", "Response"} <= rule.keys() or rule.keys() - fields:
                raise ValueError(f"{path}: requires Role/Response and only known classification fields")
            for name, (low, high) in ranges.items():
                value = rule.get(name, 0)
                if type(value) is not int or not low <= value <= high:
                    raise ValueError(f"{path}/{name}: invalid enum value {value!r}")
            if rule["Role"] == 3 and (rule["Response"] == 1 or
                    any(rule.get(name, 0) for name in ("MainType", "UnitSubType", "Relation"))):
                raise ValueError(f"{path}: StaticWorld only supports Ignore/Block without Actor classification")
            if rule.get("MainType", 0) not in (0, 1) and rule.get("UnitSubType", 0):
                raise ValueError(f"{path}: UnitSubType requires Unit or any MainType")


class Rule:
    def project(self, context):
        validate_collision_rules(context.tables["projectiles"])
        for row in context.tables["projectiles"]:
            response = row.get("MaxDistanceResponse", 0)
            if type(response) is not int or response not in (0, 1):
                raise ValueError(f"projectiles/{row['Id']}: invalid MaxDistanceResponse")
            if row.get("OnArrivalTriggerIds") and response != 1:
                raise ValueError(f"projectiles/{row['Id']}: arrival triggers require SuspendAtDestination")

    def references(self):
        return ({
            "projectiles": {"OnHitEffectId": "triggers", "OnSpawnTriggerIds": "triggers",
                            "OnHitTriggerIds": "triggers", "OnTickTriggerIds": "triggers",
                            "OnExitTriggerIds": "triggers", "OnArrivalTriggerIds": "triggers", "VfxId": "vfx",
                            "OnSpawnVfxId": "vfx", "OnHitVfxId": "vfx", "OnExpireVfxId": "vfx"},
            "aoes": {"ModelId": "models", "VfxId": "vfx", "OnDelayTriggerIds": "triggers",
                     "OnEnterTriggerIds": "triggers", "OnExitTriggerIds": "triggers",
                     "OnCrossTriggerIds": "triggers",
                     "OnIntervalTriggerIds": "triggers"},
            "emitters": {"TemplateId": "presentation_templates"},
        }, {("emitters", "TemplateId")})

    def normalize(self, table, row):
        for collision in row.get("CollisionObjects") or []:
            for field, names in (("ShapeType", ("Box", "Sphere", "Capsule")),
                                 ("ProjectileResponse", ("Default", "Block", "Ignore", "HitOnly", "Pierce"))):
                if isinstance(collision[field], int):
                    collision[field] = names[collision[field]]
        return row
