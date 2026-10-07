"""Projection of Luban enum values into the runtime projectile document."""


class Rule:
    def references(self):
        return ({
            "projectiles": {"OnHitEffectId": "triggers", "OnSpawnTriggerIds": "triggers",
                            "OnHitTriggerIds": "triggers", "OnTickTriggerIds": "triggers",
                            "OnExitTriggerIds": "triggers", "VfxId": "vfx",
                            "OnSpawnVfxId": "vfx", "OnHitVfxId": "vfx", "OnExpireVfxId": "vfx"},
            "aoes": {"ModelId": "models", "VfxId": "vfx", "OnDelayTriggerIds": "triggers",
                     "OnEnterTriggerIds": "triggers", "OnExitTriggerIds": "triggers",
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
