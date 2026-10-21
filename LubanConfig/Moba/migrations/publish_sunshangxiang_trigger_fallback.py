"""Publish the 1007 trigger slice when the Unity editor is unavailable.

The normal owner of this step is TriggerAuthoringMobaMigration.PublishRuntimeBatch.
This helper only derives the checked-in aggregate from the existing published
1006 plans so the Luban graph can be validated in a headless checkout.
"""

from __future__ import annotations

import copy
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
TARGETS = [
    ROOT / "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/ability/ability_trigger_plans.json",
    ROOT / "src/AbilityKit.Demo.Moba.Console/Configs/luban/ability/ability_trigger_plans.json",
]


def remap(value):
    if isinstance(value, dict):
        return {key: remap(item) for key, item in value.items()}
    if isinstance(value, list):
        return [remap(item) for item in value]
    if isinstance(value, float) and value.is_integer():
        value = int(value)
    if isinstance(value, int):
        text = str(value)
        for source, target in (("1006", "1007"), ("1003", "1007"), ("1004", "1007"),
                               ("3006", "3007"), ("3004", "3007"),
                               ("3106", "3107"), ("3104", "3107"),
                               ("4006", "4007"), ("4004", "4007")):
            if text.startswith(source):
                return int(target + text[len(source):])
        return value
    if isinstance(value, str):
        for source, target in (("1006", "1007"), ("1003", "1007"), ("1004", "1007"),
                               ("3006", "3007"), ("3004", "3007"),
                               ("3106", "3107"), ("3104", "3107"),
                               ("4006", "4007"), ("4004", "4007")):
            value = value.replace(source, target)
        return value
    return value


def main():
    for path in TARGETS:
        document = json.loads(path.read_text(encoding="utf-8-sig"))
        triggers = document["Triggers"]
        triggers[:] = [trigger for trigger in triggers if not str(trigger["TriggerId"]).startswith("1007")]
        source_ids = [
            (10070000, 10060000), (10070001, 10060001),
            (10070002, 10060002), (10070003, 10060003),
            (10070011, 10060011), (10070101, 10030101),
            (10070111, 10030111), (10070201, 10040201),
            (10070211, 10040211), (10070212, 10040212),
            (10070301, 10060301), (10070311, 10060311),
            (10070313, 10060313),
        ]
        by_id = {trigger["TriggerId"]: trigger for trigger in triggers}
        for target_id, source_id in source_ids:
            clone = remap(copy.deepcopy(by_id[source_id]))
            clone["TriggerId"] = target_id
            if target_id == 10070101:
                # The source module owns the roll motion. Keep the headless
                # fallback aligned with that source until Unity publishes it.
                dash = remap(copy.deepcopy(by_id[10030102]["Actions"][0]))
                clone["Actions"].insert(0, dash)
            if target_id == 10070002:
                # 10060002 is the Ying Zheng counter trigger, not the 1007
                # passive's cooldown reset. Use the registered reset action
                # shape so headless validation exercises the authored effect.
                reset = copy.deepcopy(by_id[10010201]["Actions"][0])
                reset["ActionId"] = 343447158
                reset["Arity"] = 2
                reset["Args"] = {
                    "skill_id": {"Kind": "Const", "ConstValue": 10070101.0, "Scale": 1.0},
                    "skill_slot": {"Kind": "Const", "ConstValue": 1.0, "Scale": 1.0},
                }
                clone["Actions"] = [reset]
            if target_id == 10070211:
                for action in clone.get("Actions", []):
                    if action.get("Args", {}).get("area_id", {}).get("ConstValue"):
                        action["Args"]["area_id"]["ConstValue"] = 40070202.0
            if target_id == 10070212:
                for action in clone.get("Actions", []):
                    args = action.get("Args", {})
                    if action.get("ActionId") == 176148626:
                        args["damage_type"]["ConstValue"] = 1.0
                        args["damage_value"]["ConstValue"] = 260.0
                        args["source_attack_ratio"]["ConstValue"] = 0.6
                    if action.get("ActionId") == 1241142882:
                        args["buff_ids"]["ConstValue"] = 10070201.0
            if target_id == 10070313:
                for action in clone.get("Actions", []):
                    args = action.get("Args", {})
                    if action.get("ActionId") == 176148626:
                        args["damage_type"]["ConstValue"] = 1.0
                        args["damage_value"]["ConstValue"] = 360.0
                        args["source_attack_ratio"]["ConstValue"] = 1.0
            triggers.append(clone)
        path.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("published headless 1007 trigger aggregate")


if __name__ == "__main__":
    main()
