"""Presentation and motion references."""


def validate_presentation_templates(rows):
    for row in rows:
        row_id = row.get("Id", "?")
        for field, allowed in (
            ("OffsetSpace", {0, 1}),
            ("ParentScaleMode", {0, 1}),
            ("OrderingMode", {0, 1, 2}),
            ("StackPolicy", {0, 1, 2}),
            ("StopPolicy", {0, 1}),
            ("TargetLossPolicy", {0, 1, 2, 3}),
            ("CompletionPolicy", {0, 1, 2}),
        ):
            value = row.get(field, 0)
            if type(value) is not int or value not in allowed:
                raise ValueError(
                    f"presentation_templates/{row_id}: {field} must be one of {sorted(allowed)}, got {value!r}")
        stop_grace_ms = row.get("StopGraceMs", 0)
        if type(stop_grace_ms) is not int or stop_grace_ms < 0:
            raise ValueError(
                f"presentation_templates/{row_id}: StopGraceMs must be a non-negative integer, got {stop_grace_ms!r}")


class Rule:
    def references(self):
        return ({
            "presentation_templates": {"AssetId": "vfx"},
            "motion_groups": {"SuppressedGroupIds": "motion_groups"},
        }, set())

    def validate(self, context):
        validate_presentation_templates(context.tables["presentation_templates"])
