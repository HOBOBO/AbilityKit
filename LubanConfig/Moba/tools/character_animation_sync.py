"""Authoritative character-animation snapshot and guarded, cell-local synchronization.

Unity supplies imported asset facts. This module never infers Unity animation
lengths from YAML and never edits generated resources or gameplay timings.
"""
from __future__ import annotations

import argparse
from contextlib import ExitStack, contextmanager
import hashlib
import io
import json
import math
import os
from pathlib import Path
import tempfile
import xml.etree.ElementTree as ET
import zipfile

import openpyxl

ROOT = Path(__file__).resolve().parents[3]
MODEL_BOOK = Path("LubanConfig/Moba/Production/Datas/M-模型表.xlsx")
ACTOR_BOOK = Path("LubanConfig/Moba/Production/Datas/J-角色表.xlsx")
ANIMATION_BOOK = Path("LubanConfig/Moba/Production/Datas/model-animations.xlsx")
BINDINGS = Path("Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/"
                "MobaAnimationAuthoring/character_animation_bindings.json")
FIELDS = ("animatorState", "frameCount", "frameRate", "layer")
XML = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
REL = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest() if path.exists() else "missing"


def json_bytes(value) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def read_book(path: Path):
    # Parse immutable bytes so the captured hash identifies exactly what was read.
    content = path.read_bytes()
    return openpyxl.load_workbook(io.BytesIO(content), read_only=True), hashlib.sha256(content).hexdigest()


def table(sheet):
    rows = sheet.iter_rows(values_only=True)
    headers = next(rows)
    for number, values in enumerate(rows, 2):
        if number >= 4:
            yield number, dict(zip(headers, values))


def play_nodes(action, ancestors=()):
    if not isinstance(action, dict) or not action.get("id"):
        raise ValueError("Every action node requires a stable ID")
    ids = (*ancestors, action["id"])
    if action.get("type") == "play":
        yield list(ids), action
    children = action.get("actions", [])
    child_ids = [child.get("id") for child in children]
    if len(child_ids) != len(set(child_ids)):
        raise ValueError(f"Duplicate sibling action IDs: {ids}")
    for child in children:
        yield from play_nodes(child, ids)


def target_key(target):
    if "modelId" in target:
        return ("model", target["modelId"], target["animationKey"])
    return (target["catalogRowId"], target["entityCode"], target["path"],
            target.get("skillId", 0), target["actionId"], tuple(target["nodeIds"]))


def enumerate_actions(book):
    found = set()
    for sheet in book:
        headers = [cell.value for cell in sheet[1]]
        if "variants" not in headers:
            continue
        for row_number, row in table(sheet):
            if row.get("RowId") is None:
                continue
            variants = json.loads(row.get("variants") or "[]")
            for variant in variants:
                for node_ids, node in play_nodes(variant["action"]):
                    if "animationKey" in node:
                        continue  # Model facts have their own independent export target.
                    target = dict(catalogRowId=row["RowId"], entityCode=variant["entityCode"],
                                  path=variant["path"], skillId=variant.get("skillId", 0),
                                  actionId=variant["actionId"], nodeIds=node_ids)
                    key = target_key(target)
                    if key in found:
                        raise ValueError(f"Duplicate animation target: {key}")
                    found.add(key)
                    yield target, node, sheet.title, f"{openpyxl.utils.get_column_letter(headers.index('variants') + 1)}{row_number}"


def snapshot(root: Path = ROOT, model_only=False):
    book, book_hash = read_book(root / MODEL_BOOK)
    actors_book, actors_hash = read_book(root / ACTOR_BOOK) if not model_only else (None, None)
    try:
        models = []
        for sheet in book:
            if "PrefabPath" in [c.value for c in sheet[1]]:
                models.extend({key: row[key] for key in ("Id", "Name", "PrefabPath")}
                              for _, row in table(sheet) if row.get("Id") is not None)
        actors = []
        for sheet in actors_book or ():
            if "ModelId" in [c.value for c in sheet[1]]:
                actors.extend({key: row[key] for key in ("Id", "ModelId", "Name")}
                              for _, row in table(sheet) if row.get("Id") is not None)
        actions = [{"target": target, "animation": {k: node[k] for k in FIELDS if k in node},
                    "loop": node.get("loop", False)}
                   for target, node, _, _ in (enumerate_actions(book) if not model_only else ())]
        binding_path = root / BINDINGS
        binding_content = binding_path.read_bytes() if binding_path.exists() else None
        animation_path = root / ANIMATION_BOOK
        animations = []
        if animation_path.exists():
            animation_book, animation_hash = read_book(animation_path)
            try:
                for sheet in animation_book:
                    animations.extend(row for _, row in table(sheet) if row.get("Id") is not None)
            finally:
                animation_book.close()
        else:
            animation_hash = "missing"
        fingerprints = {str(MODEL_BOOK): book_hash, str(ANIMATION_BOOK): animation_hash}
        if not model_only:
            fingerprints[str(ACTOR_BOOK)] = actors_hash
        return dict(schemaVersion=1, actions=actions, models=models, actors=actors, modelAnimations=animations,
                    fingerprints=fingerprints,
                    bindingHash=hashlib.sha256(binding_content).hexdigest() if binding_content is not None else "missing",
                    bindings=json.loads(binding_content) if binding_content is not None else
                    {"schemaVersion": 1, "bindings": []})
    finally:
        book.close()
        if actors_book is not None:
            actors_book.close()


@contextmanager
def writer_lock(path: Path):
    lock = path.with_name(path.name + ".animation-sync.lock")
    lock.parent.mkdir(parents=True, exist_ok=True)
    try:
        fd = os.open(lock, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
    except FileExistsError as exc:
        raise ValueError(f"Animation sync writer is active, or a stale lock needs review: {lock}") from exc
    try:
        with os.fdopen(fd, "w") as stream:
            stream.write(str(os.getpid()))
        yield
    finally:
        lock.unlink(missing_ok=True)


def patch_cells(content: bytes, replacements: dict[tuple[str, str], str]) -> bytes:
    """Replace inline text cells only; preserve every other ZIP member verbatim."""
    output = io.BytesIO()
    with zipfile.ZipFile(io.BytesIO(content)) as source, zipfile.ZipFile(output, "w") as dest:
        workbook = ET.fromstring(source.read("xl/workbook.xml"))
        rels = ET.fromstring(source.read("xl/_rels/workbook.xml.rels"))
        targets = {rel.attrib["Id"]: rel.attrib["Target"] for rel in rels}
        sheets = {}
        for sheet in workbook.find(f"{{{XML}}}sheets"):
            target = targets[sheet.attrib[f"{{{REL}}}id"]]
            sheets[sheet.attrib["name"]] = target.lstrip("/") if target.startswith("/") else "xl/" + target
        edits = {}
        for (sheet, address), value in replacements.items():
            edits.setdefault(sheets[sheet], {})[address] = value
        for member in source.infolist():
            data = source.read(member.filename)
            if member.filename in edits:
                for _, ns in ET.iterparse(io.BytesIO(data), events=("start-ns",)):
                    ET.register_namespace(ns[0], ns[1])
                document = ET.fromstring(data)
                cells = {cell.attrib["r"]: cell for cell in document.iter(f"{{{XML}}}c")}
                for address, value in edits[member.filename].items():
                    cell = cells[address]
                    for child in list(cell):
                        if child.tag in (f"{{{XML}}}v", f"{{{XML}}}is", f"{{{XML}}}f"):
                            cell.remove(child)
                    cell.set("t", "inlineStr")
                    inline = ET.SubElement(cell, f"{{{XML}}}is")
                    ET.SubElement(inline, f"{{{XML}}}t").text = value
                data = ET.tostring(document, encoding="utf-8", xml_declaration=True)
            dest.writestr(member, data)
    return output.getvalue()


def check_fingerprints(root, expected):
    for relative, expected_hash in expected.items():
        path = Path(relative)
        path = path if path.is_absolute() else root / path
        if digest(path) != expected_hash:
            raise ValueError(f"Source changed since scan; scan again: {relative}")


def replace_files(changes: dict[Path, bytes], expected: dict[Path, str]):
    staged, originals, committed = {}, {}, []
    try:
        for path, content in changes.items():
            path.parent.mkdir(parents=True, exist_ok=True)
            originals[path] = path.read_bytes() if path.exists() else None
            fd, temp = tempfile.mkstemp(prefix=path.name + ".", suffix=".tmp", dir=path.parent)
            with os.fdopen(fd, "wb") as stream:
                stream.write(content)
                stream.flush()
                os.fsync(stream.fileno())
            staged[path] = Path(temp)
        for path in changes:
            if digest(path) != expected[path]:
                raise ValueError(f"Concurrent edit detected: {path}")
        for path, temp in staged.items():
            if digest(path) != expected[path]:
                raise ValueError(f"Concurrent edit detected: {path}")
            os.replace(temp, path)
            committed.append(path)
    except BaseException:
        for path in reversed(committed):
            if digest(path) != hashlib.sha256(changes[path]).hexdigest():
                raise RuntimeError(f"External edit during rollback; preserve and review: {path}")
            if originals[path] is None:
                path.unlink()
            else:
                # Restore using the same atomic replacement path, not an in-place write.
                temp = staged[path]
                temp.write_bytes(originals[path])
                os.replace(temp, path)
        raise
    finally:
        for temp in staged.values():
            temp.unlink(missing_ok=True)


def apply(request, root: Path = ROOT):
    if request.get("schemaVersion") != 1:
        raise ValueError("Unsupported animation sync request")
    if request.get("mode") == "model":
        return apply_model(request, root)
    book_path, bindings_path = root / MODEL_BOOK, root / BINDINGS
    fingerprints = request["fingerprints"]
    if str(MODEL_BOOK) not in fingerprints or str(ACTOR_BOOK) not in fingerprints:
        raise ValueError("Authoritative workbook fingerprints are required")
    with ExitStack() as locks:
        for path in sorted((book_path, bindings_path)):
            locks.enter_context(writer_lock(path))
        if book_path.with_name("~$" + book_path.name).exists():
            raise ValueError("Model workbook is open in Excel; close it before synchronization")
        check_fingerprints(root, fingerprints)
        if digest(bindings_path) != request["bindingHash"]:
            raise ValueError("Animation bindings changed since scan; scan again")
        book, _ = read_book(book_path)
        try:
            indexed = {target_key(t): (t, node, sheet, cell)
                       for t, node, sheet, cell in enumerate_actions(book)}
            # Mutable JSON per variants cell is kept separate from openpyxl; only
            # that cell is replaced in the original OOXML archive below.
            cells = {}
            for sheet in book:
                headers = [c.value for c in sheet[1]]
                if "variants" not in headers:
                    continue
                for number, row in table(sheet):
                    if row.get("RowId") is not None:
                        address = f"{openpyxl.utils.get_column_letter(headers.index('variants') + 1)}{number}"
                        cells[sheet.title, address] = json.loads(row.get("variants") or "[]")
            dirty_cells, seen = set(), set()
            for change in request.get("changes", []):
                key = target_key(change["target"])
                if key in seen or key not in indexed:
                    raise ValueError(f"Duplicate or missing action target: {key}")
                seen.add(key)
                target, original, sheet, cell = indexed[key]
                before = {k: original[k] for k in FIELDS if k in original}
                if before != change["before"]:
                    raise ValueError(f"Action changed since scan: {key}")
                after = change["after"]
                if set(after) != set(FIELDS) or not after["animatorState"] or \
                        not isinstance(after["animatorState"], str) or not after["animatorState"].strip() or \
                        isinstance(after["frameCount"], bool) or not isinstance(after["frameCount"], int) or \
                        not 0 < after["frameCount"] <= 2147483647 or isinstance(after["frameRate"], bool) or \
                        not isinstance(after["frameRate"], (int, float)) or not math.isfinite(after["frameRate"]) or \
                        after["frameRate"] <= 0 or isinstance(after["layer"], bool) or \
                        not isinstance(after["layer"], int) or after["layer"] < 0:
                    raise ValueError(f"Invalid imported animation facts: {key}")
                if before == after:
                    continue
                variants = cells[sheet, cell]
                variant = next(v for v in variants if v["entityCode"] == target["entityCode"] and
                               v["path"] == target["path"] and v.get("skillId", 0) == target["skillId"] and
                               v["actionId"] == target["actionId"])
                node = variant["action"]
                for depth, node_id in enumerate(target["nodeIds"]):
                    if depth:
                        node = next(n for n in node["actions"] if n["id"] == node_id)
                    elif node["id"] != node_id:
                        raise ValueError(f"Action root changed: {key}")
                node.update(after)
                dirty_cells.add((sheet, cell))
            bindings = request["bindings"]
            if bindings.get("schemaVersion") != 1:
                raise ValueError("Unsupported animation binding schema")
            binding_keys = set()
            for binding in bindings["bindings"]:
                key = target_key(binding["target"])
                if key in binding_keys or ("modelId" not in binding["target"] and key not in indexed):
                    raise ValueError(f"Duplicate or missing binding target: {key}")
                binding_keys.add(key)
                source = binding["source"]
                for prefix in ("model", "controller", "state", "clip"):
                    if not source.get(prefix + "Guid") or not source.get(prefix + "FileId"):
                        raise ValueError(f"Missing stable {prefix} identity: {key}")
            old_bindings = json.loads(bindings_path.read_bytes()) if bindings_path.exists() else {"bindings": []}
            old_by_key = {target_key(item["target"]): item for item in old_bindings["bindings"]}
            new_by_key = {target_key(item["target"]): item for item in bindings["bindings"]}
            for key in old_by_key.keys() | new_by_key.keys():
                if key not in seen and old_by_key.get(key) != new_by_key.get(key):
                    raise ValueError(f"Unselected animation binding was modified: {key}")
            changes = {}
            if dirty_cells:
                replacements = {key: json.dumps(cells[key], ensure_ascii=False, separators=(",", ":"))
                                for key in dirty_cells}
                changes[book_path] = patch_cells(book_path.read_bytes(), replacements)
            new_bindings = json_bytes(bindings)
            if not bindings_path.exists() or json.loads(bindings_path.read_bytes()) != bindings:
                changes[bindings_path] = new_bindings
            check_fingerprints(root, fingerprints)
            expected = {book_path: fingerprints[str(MODEL_BOOK)], bindings_path: request["bindingHash"]}
            replace_files(changes, expected)
            return {"changedFiles": [str(p.relative_to(root)) for p in changes],
                    "updatedActions": len(seen), "changedCells": len(dirty_cells)}
        finally:
            book.close()


def validate_facts(after):
    if set(after) != set(FIELDS) or not isinstance(after["animatorState"], str) or not after["animatorState"].strip() or \
            type(after["frameCount"]) is not int or not 0 < after["frameCount"] <= 2147483647 or \
            type(after["frameRate"]) not in (int, float) or not math.isfinite(after["frameRate"]) or after["frameRate"] <= 0 or \
            type(after["layer"]) is not int or after["layer"] < 0:
        raise ValueError("Invalid imported animation facts")


def patch_table(content, sheet_name, updates, appended=()):
    """Patch typed cells / append owned rows without re-saving unrelated ZIP members."""
    with zipfile.ZipFile(io.BytesIO(content)) as source:
        workbook = ET.fromstring(source.read("xl/workbook.xml"))
        rels = {r.attrib["Id"]: r.attrib["Target"] for r in ET.fromstring(source.read("xl/_rels/workbook.xml.rels"))}
        sheet = next(s for s in workbook.find(f"{{{XML}}}sheets") if s.attrib["name"] == sheet_name)
        target = rels[sheet.attrib[f"{{{REL}}}id"]]
        member = target.lstrip("/") if target.startswith("/") else "xl/" + target
        document = ET.fromstring(source.read(member))
        rows = document.find(f"{{{XML}}}sheetData")
        cells = {c.attrib["r"]: c for c in document.iter(f"{{{XML}}}c")}

        def write(cell, value):
            for child in list(cell):
                cell.remove(child)
            if isinstance(value, str):
                cell.set("t", "inlineStr")
                ET.SubElement(ET.SubElement(cell, f"{{{XML}}}is"), f"{{{XML}}}t").text = value
            else:
                cell.attrib.pop("t", None)
                ET.SubElement(cell, f"{{{XML}}}v").text = str(value)

        for address, value in updates.items():
            cell = cells.get(address)
            if cell is None:
                number = int(''.join(c for c in address if c.isdigit()))
                row = next(r for r in rows if int(r.attrib["r"]) == number)
                cell = ET.SubElement(row, f"{{{XML}}}c", r=address)
                cells[address] = cell
            write(cell, value)
        next_row = max((int(r.attrib["r"]) for r in rows), default=0) + 1
        for values in appended:
            row = ET.SubElement(rows, f"{{{XML}}}row", r=str(next_row))
            for col, value in enumerate(values, 1):
                if value is not None:
                    write(ET.SubElement(row, f"{{{XML}}}c", r=f"{openpyxl.utils.get_column_letter(col)}{next_row}"), value)
            next_row += 1
        # Keep cells in column order and the used range accurate.
        for row in rows:
            row[:] = sorted(row, key=lambda c: openpyxl.utils.column_index_from_string(
                ''.join(v for v in c.attrib["r"] if v.isalpha())))
        dim = document.find(f"{{{XML}}}dimension")
        if dim is not None:
            max_col = max((openpyxl.utils.column_index_from_string(''.join(v for v in c.attrib["r"] if v.isalpha()))
                           for c in document.iter(f"{{{XML}}}c")), default=1)
            dim.set("ref", f"A1:{openpyxl.utils.get_column_letter(max_col)}{next_row - 1}")
        output = io.BytesIO()
        with zipfile.ZipFile(output, "w") as dest:
            for info in source.infolist():
                dest.writestr(info, ET.tostring(document, encoding="utf-8", xml_declaration=True)
                              if info.filename == member else source.read(info.filename))
        return output.getvalue()


def apply_model(request, root: Path = ROOT):
    path, binding_path = root / ANIMATION_BOOK, root / BINDINGS
    fingerprints = request["fingerprints"]
    if str(ANIMATION_BOOK) not in fingerprints or str(MODEL_BOOK) not in fingerprints:
        raise ValueError("Authoritative workbook fingerprints are required")
    with ExitStack() as locks:
        for item in sorted((path, binding_path)):
            locks.enter_context(writer_lock(item))
        if path.with_name("~$" + path.name).exists():
            raise ValueError("Animation workbook is open in Excel; close it before synchronization")
        check_fingerprints(root, fingerprints)
        if digest(binding_path) != request["bindingHash"]:
            raise ValueError("Animation bindings changed since scan; scan again")
        current = snapshot(root, model_only=True)
        model_ids = {m["Id"] for m in current["models"]}
        book, _ = read_book(path)
        try:
            sheet = book["animations"]
            headers = [c.value for c in sheet[1]]
            indexed, ids = {}, set()
            for number, row in table(sheet):
                if row.get("Id") is None:
                    continue
                key = ("model", row["ModelId"], row["ActionKey"])
                if key in indexed or row["Id"] in ids:
                    raise ValueError("Duplicate model animation")
                indexed[key] = (number, row)
                ids.add(row["Id"])
            updates, appended, seen = {}, [], set()
            next_id = max(ids, default=0) + 1
            for change in request.get("changes", []):
                target = change["target"]
                if set(target) != {"modelId", "animationKey"} or type(target["modelId"]) is not int or \
                        target["modelId"] not in model_ids or not isinstance(target["animationKey"], str) or \
                        not target["animationKey"].strip() or target["animationKey"] != target["animationKey"].strip():
                    raise ValueError("Invalid model animation target")
                key = target_key(target)
                if key in seen:
                    raise ValueError("Duplicate model animation target")
                seen.add(key)
                after = change["after"]
                validate_facts(after)
                column_fields = dict(zip(FIELDS, ("AnimatorState", "FrameCount", "FrameRate", "Layer")))
                existing = indexed.get(key)
                before = {k: existing[1][v] for k, v in column_fields.items()} if existing else {}
                if before != change["before"]:
                    raise ValueError("Model animation changed since scan")
                if before == after:
                    continue
                values = {"Id": next_id, "ModelId": target["modelId"], "ActionKey": target["animationKey"],
                          **{column_fields[k]: v for k, v in after.items()}}
                if existing:
                    for k, field in column_fields.items():
                        if before[k] != after[k]:
                            updates[f"{openpyxl.utils.get_column_letter(headers.index(field) + 1)}{existing[0]}"] = after[k]
                else:
                    appended.append([values.get(h) for h in headers])
                    next_id += 1
            if not seen:
                raise ValueError("Select at least one model animation")
            bindings = request["bindings"]
            if bindings.get("schemaVersion") != 1:
                raise ValueError("Unsupported animation binding schema")
            old = {target_key(b["target"]): b for b in current["bindings"]["bindings"]}
            new = {}
            for b in bindings["bindings"]:
                key = target_key(b["target"])
                if key in new:
                    raise ValueError("Duplicate animation binding")
                new[key] = b
                if key in seen:
                    for prefix in ("model", "controller", "state", "clip"):
                        if not b["source"].get(prefix + "Guid") or not b["source"].get(prefix + "FileId"):
                            raise ValueError("Missing stable resource identity")
            if not seen.issubset(new):
                raise ValueError("Selected model animation requires a stable binding")
            for key in old.keys() | new.keys():
                if key not in seen and old.get(key) != new.get(key):
                    raise ValueError("Unselected animation binding was modified")
            changes = {}
            if updates or appended:
                changes[path] = patch_table(path.read_bytes(), sheet.title, updates, appended)
            if current["bindings"] != bindings:
                changes[binding_path] = json_bytes(bindings)
            check_fingerprints(root, fingerprints)
            replace_files(changes, {path: fingerprints[str(ANIMATION_BOOK)], binding_path: request["bindingHash"]})
            return {"changedFiles": [str(p.relative_to(root)) for p in changes], "updatedActions": len(seen),
                    "changedCells": len(updates), "appendedRows": len(appended)}
        finally:
            book.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("snapshot", "snapshot-model", "apply"))
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--request", type=Path)
    args = parser.parse_args()
    if args.command == "apply" and args.request is None:
        parser.error("apply requires --request")
    result = snapshot(model_only=args.command == "snapshot-model") if args.command != "apply" else apply(json.loads(args.request.read_bytes()))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_bytes(json_bytes(result))


if __name__ == "__main__":
    main()
