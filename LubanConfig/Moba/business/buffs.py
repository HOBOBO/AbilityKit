"""Buff and continuous process references."""


def validate_buff_hud_templates(rows, buffs):
    buff_ids = {row["Id"] for row in buffs}
    for row in rows:
        template_id = row.get("Id", 0)
        if template_id not in buff_ids:
            raise ValueError(f"buff_hud_templates/{template_id}: missing Buff {template_id}")
        if not row.get("Name"):
            raise ValueError(f"buff_hud_templates/{template_id}: Name is required")


class Rule:
    def project(self, context):
        validate_buff_hud_templates(
            context.tables["buff_hud_templates"],
            context.tables["buffs"])

    def references(self):
        return ({
            "buffs": {"ContinuousTagTemplateId": "continuous_tag_templates",
                      "PresentationTemplateId": "presentation_templates", "OnAddEffects": "triggers",
                      "OnRemoveEffects": "triggers", "OnIntervalEffects": "triggers",
                      "TriggerIds": "triggers"},
            "continuous_processes": {"ContinuousTagTemplateId": "continuous_tag_templates",
                                     "IntervalTriggerIds": "triggers", "TriggerIds": "triggers"},
        }, set())
