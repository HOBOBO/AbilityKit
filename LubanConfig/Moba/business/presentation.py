"""Presentation and motion references."""


class Rule:
    def references(self):
        return ({
            "presentation_templates": {"AssetId": "vfx"},
            "motion_groups": {"SuppressedGroupIds": "motion_groups"},
        }, set())
