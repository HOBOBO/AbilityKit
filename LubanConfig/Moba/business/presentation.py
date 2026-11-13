"""Presentation and motion references."""

import math


def validate_model_animations(tables):
    models = {row["Id"] for row in tables.get("models", [])}
    animations, ids = {}, set()
    for row in tables.get("model_animations", []):
        key = (row.get("ModelId"), row.get("ActionKey"))
        if type(row.get("Id")) is not int or row["Id"] <= 0 or row["Id"] in ids or \
                type(key[0]) is not int or key[0] not in models or not isinstance(key[1], str) or \
                not key[1].strip() or key[1] != key[1].strip() or key in animations or \
                not isinstance(row.get("AnimatorState"), str) or not row["AnimatorState"].strip() or \
                type(row.get("FrameCount")) is not int or not 0 < row["FrameCount"] <= 2147483647 or \
                type(row.get("FrameRate")) not in (int, float) or not math.isfinite(row["FrameRate"]) or row["FrameRate"] <= 0 or \
                type(row.get("Layer")) is not int or row["Layer"] < 0:
            raise ValueError(f"Invalid / duplicate model animation: {key}")
        ids.add(row["Id"])
        animations[key] = row

    def visit(node):
        if not isinstance(node, dict):
            raise ValueError("Invalid character action node")
        if "animationKey" in node or "modelId" in node:
            key = (node.get("modelId"), node.get("animationKey"))
            if node.get("type") != "play" or type(key[0]) is not int or not isinstance(key[1], str) or \
                    key not in animations or any(f in node for f in ("animatorState", "frameCount", "frameRate", "layer")):
                raise ValueError(f"Missing / mixed model animation reference: {key}")
        for child in node.get("actions", []):
            visit(child)

    for row in tables.get("character_view_actions", []):
        for entry in (*row.get("states", []), *row.get("variants", [])):
            visit(entry["action"])


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
        validate_model_animations(context.tables)
