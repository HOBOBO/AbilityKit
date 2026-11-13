#!/usr/bin/env python3
"""Read-only contract checks for the Jhin MOBA integration.

This intentionally checks authoring sources and published view resources without
rewriting any generated file. Structural mismatches fail; known presentation
gaps are warnings so the report can be used before the runtime work starts.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SKILL_XLSX = ROOT / "LubanConfig/Moba/Production/Datas/J-技能表.xlsx"
HFSM = ROOT / "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaStateMachineAuthoring/character_hfsm.json"
FLOW = ROOT / "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/Moba/SkillFlowCO.asset"
ACTIONS = ROOT / "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/luban/moba/character_view_actions.json"
TRIGGER_DIR = ROOT / "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaTriggerAuthoring/Sources"
MODIFIERS = ROOT / "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/luban/moba/modifier_definitions.json"

SKILL_IDS = {30010101, 30010201, 30010301, 30010401, 30010501, 30010502, 30010601, 30010602, 30010603, 30010604}
PASSIVE_ID = 30010000
REQUIRED_STATES = {"attack.slot.1", "attack.slot.2", "attack.slot.3", "attack.slot.4", "attack.slot.5", "r.aiming", "r.shooting", "r.final_shot"}
REQUIRED_HFSM_CONDITIONS = {"parameter.10.eq.0", "parameter.10.eq.1", "parameter.10.eq.2", "parameter.10.eq.3", "parameter.10.eq.4", "parameter.10.eq.5", "parameter.11.eq.0", "parameter.11.eq.1", "parameter.11.eq.2", "parameter.11.eq.3"}
REQUIRED_TRIGGERS = {30010001, 30010002, 30010003, 30010004, 30010006, 30010014}
R_TRIGGERS = {32010404, 32010405}
EXPECTED_ACTIONS = {
    "attack.slot.1": "AutoAttack_1",
    "attack.slot.2": "AutoAttack_2",
    "attack.slot.3": "AutoAttack_3",
    "attack.slot.4": "AutoAttack_4",
    "attack.slot.5": "AutoAttack_Reload",
    "r.aiming": "Spell_R_Idle",
    "r.shooting": "Spell_R_Shoot",
    "r.final_shot": "Spell_R_ShootLast",
}


class Report:
    def __init__(self) -> None:
        self.errors: list[str] = []
        self.warnings: list[str] = []

    def error(self, message: str) -> None:
        self.errors.append(message)
        print(f"ERROR: {message}")

    def warning(self, message: str) -> None:
        self.warnings.append(message)
        print(f"WARNING: {message}")

    def ok(self, message: str) -> None:
        print(f"OK: {message}")


def load_json(path: Path, report: Report):
    if not path.exists():
        report.error(f"missing file: {path.relative_to(ROOT)}")
        return None
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception as exc:
        report.error(f"invalid JSON {path.relative_to(ROOT)}: {exc}")
        return None


def check_excel(report: Report) -> None:
    if not SKILL_XLSX.exists():
        report.error("missing authoritative Jhin skill workbook")
        return
    try:
        import openpyxl
        workbook = openpyxl.load_workbook(SKILL_XLSX, data_only=True, read_only=True)
        skill_rows = list(workbook["技能"].values)
        passive_rows = list(workbook["被动技能"].values)
    except Exception as exc:
        report.error(f"cannot read Excel authority: {exc}")
        return
    found = {int(row[1]) for row in skill_rows[3:] if row and len(row) > 1 and row[1] is not None}
    missing = sorted(SKILL_IDS - found)
    if missing:
        report.error(f"skill workbook missing IDs: {missing}")
    else:
        report.ok(f"skill workbook contains {len(SKILL_IDS)} Jhin active/attack IDs")
    passive_found = {int(row[1]) for row in passive_rows[3:] if row and len(row) > 1 and row[1] is not None}
    if PASSIVE_ID not in passive_found:
        report.error(f"skill workbook missing passive ID {PASSIVE_ID}")
    else:
        report.ok(f"skill workbook contains passive {PASSIVE_ID}")


def check_hfsm(report: Report) -> None:
    data = load_json(HFSM, report)
    if not data:
        return
    machines = {machine.get("id"): machine for machine in data.get("machines", [])}
    action = machines.get("action")
    if action is None:
        report.error("HFSM is missing action machine")
        return
    states = {state.get("id") for state in action.get("states", [])}
    missing_states = sorted(REQUIRED_STATES - states)
    if missing_states:
        report.error(f"HFSM action machine missing states: {missing_states}")
    else:
        report.ok("HFSM contains attack slots 1..5 and R aiming/shooting/final states")
    conditions = {transition.get("conditionKey") for transition in action.get("transitions", [])}
    missing_conditions = sorted(REQUIRED_HFSM_CONDITIONS - conditions)
    if missing_conditions:
        report.error(f"HFSM missing parameter conditions: {missing_conditions}")
    else:
        report.ok("HFSM contains parameter 10 and 11 condition coverage")


def check_flows(report: Report) -> None:
    if not FLOW.exists():
        report.error("missing SkillFlowCO authority")
        return
    text = FLOW.read_text(encoding="utf-8-sig")
    found = {int(value) for value in re.findall(r"^\s*- Id: (\d+)", text, flags=re.MULTILINE)}
    missing = sorted(SKILL_IDS - found)
    if missing:
        report.error(f"SkillFlowCO missing flow IDs: {missing}")
    else:
        report.ok(f"SkillFlowCO contains {len(SKILL_IDS)} Jhin flow IDs")


def collect_triggers(report: Report) -> set[int]:
    found: set[int] = set()
    for path in sorted(TRIGGER_DIR.glob("*.trigger.json")):
        data = load_json(path, report)
        if not data:
            continue
        for trigger in data.get("module", {}).get("triggers", []):
            if isinstance(trigger.get("id"), int):
                found.add(trigger["id"])
    return found


def check_triggers(report: Report) -> None:
    found = collect_triggers(report)
    missing = sorted(REQUIRED_TRIGGERS - found)
    if missing:
        report.error(f"Trigger Authoring missing Jhin trigger IDs: {missing}")
    else:
        report.ok("Trigger Authoring contains passive counter/reload entry triggers")
    referenced: set[int] = set()
    for path in sorted(TRIGGER_DIR.glob("*.trigger.json")):
        text = path.read_text(encoding="utf-8-sig")
        for identifier in R_TRIGGERS:
            if str(identifier) in text:
                referenced.add(identifier)
    missing_r = sorted(R_TRIGGERS - referenced)
    if missing_r:
        report.warning(f"R phase trigger references not found in current source set: {missing_r}")
    else:
        report.ok("Trigger Authoring contains R phase trigger references")


def check_actions(report: Report) -> None:
    data = load_json(ACTIONS, report)
    if not data:
        return
    base_paths = {item.get("path"): item for row in data for item in row.get("states", [])}
    missing = sorted(
        path for path in EXPECTED_ACTIONS
        if f"life/alive/action/{path}" not in base_paths
    )
    if missing:
        report.error(f"character_view_actions missing state paths: {missing}")
    else:
        report.ok("character_view_actions contains all Jhin HFSM action paths")
    variant_actions: dict[str, set[str]] = {path: set() for path in EXPECTED_ACTIONS}
    for row in data:
        for variant in row.get("variants", []):
            if variant.get("entityCode") != 3001:
                continue
            path = variant.get("path")
            if path and path.startswith("life/alive/action/"):
                state = path.rsplit("/", 1)[-1]
                if state in variant_actions:
                    action = variant.get("action", {})
                    action_items = action.get("actions", [])
                    if action.get("type") == "play":
                        action_items = [action]
                    for item in action_items:
                        if item.get("type") == "play" and item.get("animatorState"):
                            variant_actions[state].add(item["animatorState"])
    for state, expected in EXPECTED_ACTIONS.items():
        if expected not in variant_actions[state]:
            report.warning(f"entityCode 3001 action {state} does not map to {expected}; found {sorted(variant_actions[state])}")
    if all(expected in variant_actions[state] for state, expected in EXPECTED_ACTIONS.items()):
        report.ok("entityCode 3001 maps all required states to expected model actions")


def check_modifier_domains(report: Report) -> None:
    data = load_json(MODIFIERS, report)
    if not data:
        return
    by_id = {item.get("Id"): item for item in data}
    for identifier in (8000002, 8000005, 8000006):
        item = by_id.get(identifier)
        if not item:
            report.error(f"missing RPhase modifier definition {identifier}")
        elif item.get("Target", {}).get("Kind") != 2 or item.get("Target", {}).get("Id") != 11:
            report.error(f"modifier {identifier} is not an HFSM RPhase contribution")
    for identifier in (2000003, 2000004, 2000005, 2000006, 2000007):
        item = by_id.get(identifier)
        if not item:
            report.error(f"missing skill-slot modifier definition {identifier}")
        elif item.get("Target", {}).get("Kind") == 2 and item.get("Target", {}).get("Id") == 10:
            report.error(f"modifier {identifier} incorrectly aliases HFSM AttackSlot parameter 10")
    report.ok("HFSM parameter and skill-slot modifier domains remain distinct")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, help="repository root override")
    args = parser.parse_args()
    if args.root:
        global ROOT, SKILL_XLSX, HFSM, FLOW, ACTIONS, TRIGGER_DIR, MODIFIERS
        ROOT = args.root.resolve()
        SKILL_XLSX = ROOT / "LubanConfig/Moba/Production/Datas/J-技能表.xlsx"
        HFSM = ROOT / "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaStateMachineAuthoring/character_hfsm.json"
        FLOW = ROOT / "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/Moba/SkillFlowCO.asset"
        ACTIONS = ROOT / "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/luban/moba/character_view_actions.json"
        TRIGGER_DIR = ROOT / "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaTriggerAuthoring/Sources"
        MODIFIERS = ROOT / "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/luban/moba/modifier_definitions.json"
    report = Report()
    check_excel(report)
    check_hfsm(report)
    check_flows(report)
    check_triggers(report)
    check_actions(report)
    check_modifier_domains(report)
    print(f"SUMMARY: {len(report.errors)} error(s), {len(report.warnings)} warning(s)")
    return 1 if report.errors else 0


if __name__ == "__main__":
    sys.exit(main())
