"""Buff and continuous process references."""


class Rule:
    def references(self):
        return ({
            "buffs": {"ContinuousTagTemplateId": "continuous_tag_templates",
                      "PresentationTemplateId": "presentation_templates", "OnAddEffects": "triggers",
                      "OnRemoveEffects": "triggers", "OnIntervalEffects": "triggers",
                      "TriggerIds": "triggers"},
            "continuous_processes": {"ContinuousTagTemplateId": "continuous_tag_templates",
                                     "IntervalTriggerIds": "triggers", "TriggerIds": "triggers"},
        }, set())
