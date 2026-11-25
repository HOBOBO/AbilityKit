"""One-time guarded migration of explicitly bound character nodes to model references.

Usage: python LubanConfig/Moba/tools/migrate_model_animation_catalog.py --entity 3002 --apply
No Unity asset facts are guessed: migration uses the previously reviewed bindings/facts.
"""
from __future__ import annotations
import argparse
from contextlib import ExitStack
import copy
import io
import json
from pathlib import Path
import sys
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parent))
import character_animation_sync as sync
import openpyxl
from openpyxl.styles import Font, PatternFill


def empty_catalog():
    book = openpyxl.Workbook()
    sheet = book.active
    sheet.title = "animations"
    sheet.append(["##var", "Id", "ModelId", "ActionKey", "AnimatorState", "FrameCount", "FrameRate", "Layer"])
    sheet.append(["##type", "int", "int", "string", "string", "int", "double", "int"])
    sheet.append(["##", "Row key", "Model reference", "Stable key within model", "Full Animator state path",
                  "Duration frame intervals", "Imported frames per second", "Animator layer"])
    sheet.freeze_panes = "B4"
    for cell in sheet[1]:
        cell.font = Font(bold=True, color="FFFFFF")
        cell.fill = PatternFill("solid", fgColor="305478")
    for col, width in {"A": 12, "B": 12, "C": 14, "D": 44, "E": 48, "F": 25, "G": 30, "H": 20}.items():
        sheet.column_dimensions[col].width = width
    stream = io.BytesIO()
    book.save(stream)
    book.close()
    return stream.getvalue()


def migrate(entity_code, root=sync.ROOT, apply=False):
    schema_path = Path("LubanConfig/Moba/Production/Datas/__tables__.xlsx")
    complex_path = Path("LubanConfig/Moba/Production/complex_columns.json")
    keys_path = Path("LubanConfig/Moba/Production/table_keys.json")
    owned = (sync.MODEL_BOOK, sync.ANIMATION_BOOK, sync.BINDINGS, schema_path, complex_path, keys_path)
    with ExitStack() as locks:
        for relative in sorted(owned):
            path = root / relative
            locks.enter_context(sync.writer_lock(path))
            if path.with_name("~$" + path.name).exists():
                raise ValueError(f"Workbook is open in Excel: {path}")
        facts = sync.snapshot(root)
        expected = {root / p: sync.digest(root / p) for p in owned}
        actors = {a["Id"]: a for a in facts["actors"]}
        if entity_code not in actors:
            raise ValueError("Selected actor is not configured")
        model_id = actors[entity_code]["ModelId"]
        bindings = copy.deepcopy(facts["bindings"])
        by_target = {sync.target_key(b["target"]): b for b in bindings["bindings"]}
        groups, targets = {}, {}
        existing = {(r["ModelId"], r["ActionKey"]): r for r in facts["modelAnimations"]}
        for action in facts["actions"]:
            target = action["target"]
            if target["entityCode"] != entity_code:
                continue
            binding = by_target.get(sync.target_key(target))
            if binding is None:
                raise ValueError("Migrate only after reviewing and binding every selected play node")
            animation = action["animation"]
            sync.validate_facts(animation)
            key = animation["animatorState"]
            pair = (model_id, key)
            candidate = {"animation": animation, "source": binding["source"]}
            if pair in groups and groups[pair] != candidate:
                raise ValueError("Shared model action facts or resource identities disagree")
            groups[pair] = candidate
            targets[sync.target_key(target)] = pair
        if not targets:
            return {"migratedNodes": 0, "modelActions": 0, "changedFiles": []}
        book, _ = sync.read_book(root / sync.MODEL_BOOK)
        replacements = {}
        migrated = 0
        try:
            for sheet in book:
                headers = [c.value for c in sheet[1]]
                if "variants" not in headers:
                    continue
                for number, row in sync.table(sheet):
                    if row.get("RowId") is None:
                        continue
                    variants = json.loads(row.get("variants") or "[]")
                    dirty = False
                    for variant in variants:
                        if variant["entityCode"] != entity_code:
                            continue
                        for node_ids, node in sync.play_nodes(variant["action"]):
                            if "animationKey" in node:
                                continue
                            target = dict(catalogRowId=row["RowId"], entityCode=entity_code, path=variant["path"],
                                          skillId=variant.get("skillId", 0), actionId=variant["actionId"], nodeIds=node_ids)
                            pair = targets[sync.target_key(target)]
                            for field in sync.FIELDS:
                                node.pop(field, None)
                            node.update(modelId=pair[0], animationKey=pair[1])
                            migrated += 1
                            dirty = True
                    if dirty:
                        replacements[sheet.title, f"{openpyxl.utils.get_column_letter(headers.index('variants')+1)}{number}"] = \
                            json.dumps(variants, ensure_ascii=False, separators=(",", ":"))
        finally:
            book.close()
        changes = {root / sync.MODEL_BOOK: sync.patch_cells((root / sync.MODEL_BOOK).read_bytes(), replacements)}
        catalog_content = (root / sync.ANIMATION_BOOK).read_bytes() if (root / sync.ANIMATION_BOOK).exists() else empty_catalog()
        appended = []
        next_id = max((r["Id"] for r in facts["modelAnimations"]), default=0) + 1
        for pair, candidate in groups.items():
            animation = candidate["animation"]
            row = existing.get(pair)
            if row:
                if [row[f] for f in ("AnimatorState", "FrameCount", "FrameRate", "Layer")] != [animation[f] for f in sync.FIELDS]:
                    raise ValueError("Existing model animation facts disagree")
            else:
                appended.append([None, next_id, *pair, *(animation[f] for f in sync.FIELDS)])
                next_id += 1
        if appended:
            changes[root / sync.ANIMATION_BOOK] = sync.patch_table(catalog_content, "animations", {}, appended)
        bindings["bindings"] = [b for b in bindings["bindings"] if sync.target_key(b["target"]) not in targets]
        for pair, candidate in groups.items():
            target = {"modelId": pair[0], "animationKey": pair[1]}
            binding = {"target": target, "source": candidate["source"]}
            previous = next((b for b in bindings["bindings"] if sync.target_key(b["target"]) == sync.target_key(target)), None)
            if previous and previous != binding:
                raise ValueError("Existing model binding disagrees")
            if not previous:
                bindings["bindings"].append(binding)
        bindings["bindings"].sort(key=lambda b: json.dumps(b["target"], sort_keys=True))
        changes[root / sync.BINDINGS] = sync.json_bytes(bindings)
        book, _ = sync.read_book(root / schema_path)
        try:
            sheet = book.active
            if not any(row.get("output") == "model_animations" for _, row in sync.table(sheet)):
                changes[root / schema_path] = sync.patch_table((root / schema_path).read_bytes(), sheet.title, {}, [
                    [None, "ModelAnimations", "DRModelAnimations", "true", "animations@model-animations.xlsx", "Id", None,
                     "c,s,e", "Independent model animation facts", None, "model_animations"]])
        finally:
            book.close()
        for relative, value in ((complex_path, {}), (keys_path, "Id")):
            data = json.loads((root / relative).read_bytes())
            if "model_animations" not in data:
                data["model_animations"] = value
                changes[root / relative] = sync.json_bytes(data)
        report = {"migratedNodes": migrated, "modelActions": len(groups),
                  "changedFiles": [str(p.relative_to(root)) for p in changes],
                  "before": {str(p.relative_to(root)): h for p, h in expected.items()}}
        if apply:
            sync.check_fingerprints(root, facts["fingerprints"])
            sync.replace_files(changes, expected)
        return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--entity", type=int, required=True)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    print(json.dumps(migrate(args.entity, apply=args.apply), indent=2, ensure_ascii=False))
