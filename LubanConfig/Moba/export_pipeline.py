"""Build and validate MOBA Luban artifacts from Production/Datas.

Run without --apply to validate into local/luban-export. --apply publishes
generated assets to the Unity package and Console replica.
"""

import argparse
import json
import shutil
import subprocess
from pathlib import Path, PurePosixPath


ROOT = Path(__file__).resolve().parents[2]
PRODUCTION = ROOT / "LubanConfig/Moba/Production"
LUBAN = ROOT / "LubanConfig/Tools/Luban/Luban.dll"
STAGE = ROOT / "local/luban-export"
UNITY = ROOT / "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/luban"
CONSOLE = ROOT / "src/AbilityKit.Demo.Moba.Console/Configs/luban"
CODE = ROOT / "Unity/Packages/com.abilitykit.demo.moba.runtime/Runtime/Infrastructure/Config/BattleDemo/LubanBinaryGen"
RESOURCES = UNITY.parent
RESOURCE_TABLE = "resource_documents"
TABLE_KEYS = PRODUCTION / "table_keys.json"
TABLE_ROOTS = PRODUCTION / "table_roots.json"
AGGREGATE = "ability/ability_trigger_plans.json"


def run_luban(kind, output):
    command = ["dotnet", str(LUBAN), "-t", "all", kind, "bin" if kind == "-d" and output.name == "bin" else
               "json" if kind == "-d" else "cs-bin", "--conf", str(PRODUCTION / "luban.conf"),
               "-x", ("outputCodeDir=" if kind == "-c" else "outputDataDir=") + str(output)]
    subprocess.run(command, cwd=ROOT, check=True)


def normalize_row(row, complex_columns):
    omitted = set(filter(None, row.pop("OmittedFields", "").split(",")))
    nulls = set(filter(None, row.pop("NullFields", "").split(",")))
    for field, kind in complex_columns.items():
        value = row.get(field)
        if not isinstance(value, str):
            continue
        if not value:
            row[field] = [] if kind == "array" else None
        else:
            row[field] = json.loads(value)
    for field in omitted:
        if row.get(field) in (None, "", 0, False, []):
            row.pop(field, None)
    for field in nulls:
        if row.get(field) in (None, "", 0, False, []):
            row[field] = None
    return row


def restored_table(name, rows, roots):
    if name not in roots:
        return rows
    definition = roots[name]
    if definition["syntheticKey"]:
        rows = [{key: value for key, value in row.items() if key != "RowId"} for row in rows]
    if definition["kind"] == "object":
        if len(rows) != 1:
            raise ValueError(f"{name}: object root requires exactly one row")
        return rows[0]
    if definition["kind"] != "array":
        raise ValueError(f"{name}: unknown root kind {definition['kind']}")
    return rows


def verify_tables(tables, json_dir, check_baseline, roots):
    baseline = ROOT / "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba"
    keys = json.loads(TABLE_KEYS.read_text(encoding="utf-8")) if TABLE_KEYS.exists() else {}
    for name in tables:
        generated_rows = json.loads((json_dir / f"{name}.json").read_text(encoding="utf-8-sig"))
        generated_ids = [row[keys.get(name, "Id")] for row in generated_rows]
        if len(generated_ids) != len(set(generated_ids)):
            raise ValueError(f"{name}: duplicated IDs")
        if check_baseline and name != "ability_nodes":
            source_rows = json.loads((baseline / f"{name}.json").read_text(encoding="utf-8-sig"))
            if source_rows != restored_table(name, generated_rows, roots):
                raise ValueError(f"{name}: generated JSON differs from the migration baseline")
        if not (STAGE / "bin" / f"{name}.bytes").is_file():
            raise ValueError(f"{name}: binary output missing")
        print(f"validated {name}: {len(generated_ids)} rows")


def valid_resource_path(name, owner):
    path = PurePosixPath(name)
    if (not isinstance(name, str) or "\\" in name or path.is_absolute() or
            ".." in path.parts or path.suffix != ".json" or
            len(path.parts) < 2 or path.parts[0] not in ("moba", "ability")):
        raise ValueError(f"{owner}: invalid resource path {name}")
    return path


def build_resource_documents():
    rows = json.loads((STAGE / "json" / f"{RESOURCE_TABLE}.json").read_text(encoding="utf-8"))
    registered = set(json.loads((PRODUCTION / "complex_columns.json").read_text(encoding="utf-8")))
    documents = {}
    ids = set()
    for row in rows:
        row_id, name, part, content = (row[key] for key in ("Id", "Path", "Part", "Content"))
        if row_id in ids:
            raise ValueError(f"resource_documents: duplicate row ID {row_id}")
        ids.add(row_id)
        path = valid_resource_path(name, RESOURCE_TABLE)
        if name.startswith("moba/") and path.stem in registered and len(path.parts) == 2:
            raise ValueError(f"resource_documents: {name} is owned by its dedicated workbook")
        documents.setdefault(name, []).append((part, content))
    output = {}
    for name, chunks in documents.items():
        chunks.sort()
        if [part for part, _ in chunks] != list(range(len(chunks))):
            raise ValueError(f"resource_documents: missing or duplicate part in {name}")
        content = "".join(value for _, value in chunks)
        json.loads(content)
        output[name] = content
    print(f"validated {len(output)} resource documents")
    return output


def build_ability_documents():
    rows = json.loads((STAGE / "json/ability_nodes.json").read_text(encoding="utf-8-sig"))
    nodes = {}
    ids = set()
    for row in rows:
        row_id, name, pointer, kind, value = (row[key] for key in
                                              ("Id", "Path", "Pointer", "Kind", "ValueJson"))
        valid_resource_path(name, "ability_nodes")
        if not name.startswith("ability/") or name == AGGREGATE:
            raise ValueError(f"ability_nodes: invalid ability path {name}")
        if row_id in ids:
            raise ValueError(f"ability_nodes: duplicate row ID {row_id}")
        ids.add(row_id)
        if pointer and (not pointer.startswith("/") or any(
                "~" in token.replace("~0", "").replace("~1", "")
                for token in pointer[1:].split("/"))):
            raise ValueError(f"ability_nodes: invalid pointer {name}{pointer}")
        entries = nodes.setdefault(name, {})
        if pointer in entries:
            raise ValueError(f"ability_nodes: duplicate pointer {name}{pointer}")
        if kind not in ("json", "object", "array"):
            raise ValueError(f"ability_nodes: invalid kind {kind}")
        if kind == "json":
            if not value.startswith("json:"):
                raise ValueError(f"ability_nodes: missing JSON marker at {name}{pointer}")
            parsed = json.loads(value[5:])
        else:
            if value:
                raise ValueError(f"ability_nodes: container has ValueJson at {name}{pointer}")
            parsed = None
        entries[pointer] = (kind, parsed)

    def decode(token):
        return token.replace("~1", "/").replace("~0", "~")

    def assemble(name, entries):
        if "" not in entries:
            raise ValueError(f"ability_nodes: missing root in {name}")
        children = {}
        for pointer in entries:
            if not pointer:
                continue
            parent, _, token = pointer.rpartition("/")
            children.setdefault(parent, []).append((decode(token), pointer))
        visited = set()

        def visit(pointer):
            visited.add(pointer)
            kind, value = entries[pointer]
            descendants = children.get(pointer, [])
            if kind == "json":
                if descendants:
                    raise ValueError(f"ability_nodes: scalar has children at {name}{pointer}")
                return value
            if kind == "object":
                return {token: visit(child) for token, child in descendants}
            indexes = []
            for token, child in descendants:
                if not token.isdigit() or (len(token) > 1 and token[0] == "0"):
                    raise ValueError(f"ability_nodes: invalid array index at {name}{child}")
                indexes.append((int(token), child))
            indexes.sort()
            if [index for index, _ in indexes] != list(range(len(indexes))):
                raise ValueError(f"ability_nodes: non-contiguous array at {name}{pointer}")
            return [visit(child) for _, child in indexes]

        result = visit("")
        if visited != entries.keys():
            raise ValueError(f"ability_nodes: orphan pointers in {name}: {entries.keys() - visited}")
        return result

    documents = {name: json.dumps(assemble(name, entries), ensure_ascii=False, indent=2) + "\n"
                 for name, entries in nodes.items()}
    print(f"validated {len(documents)} ability documents from {len(rows)} nodes")
    return documents


def compile_trigger_aggregate(documents):
    trigger_dir = STAGE / "ability/triggers"
    if trigger_dir.exists():
        shutil.rmtree(trigger_dir)
    trigger_dir.mkdir(parents=True)
    triggers = {name: content for name, content in documents.items()
                if name.startswith("ability/triggers/")}
    if not triggers:
        raise ValueError("ability_nodes: no trigger plan sources")
    for name, content in triggers.items():
        path = STAGE / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")
    output = STAGE / AGGREGATE
    subprocess.run(["dotnet", "run", "--project", str(ROOT / "tools/MobaTriggerAggregate"),
                    "--", str(trigger_dir), str(output)], cwd=ROOT, check=True)
    documents[AGGREGATE] = output.read_text(encoding="utf-8")


def verify_documents(documents):
    baseline = ROOT / "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources"
    for name, content in documents.items():
        if name == AGGREGATE:
            continue  # The old aggregate drifted from its individual trigger sources.
        source = json.loads((baseline / name).read_text(encoding="utf-8-sig"))
        if source != json.loads(content):
            raise ValueError(f"{name}: generated JSON differs from the migration baseline")


def validate_references():
    def rows(name):
        return json.loads((STAGE / "normalized-json" / f"{name}.json").read_text(encoding="utf-8"))

    characters = rows("characters")
    skills = {row["Id"]: row for row in rows("skills")}
    passives = {row["Id"] for row in rows("passive_skills")}
    flows = {row["Id"] for row in rows("skill_flows")}
    attributes = {row["Id"] for row in rows("attribute_templates")}
    models = {row["Id"] for row in rows("models")}
    for hero in characters:
        hero_id = hero["Id"]
        for field, available in (("SkillIds", skills), ("PassiveSkillIds", passives)):
            for value in hero.get(field, []):
                if value not in available:
                    raise ValueError(f"characters/{hero_id}: {field} references missing ID {value}")
        for field, available in (("AttributeTemplateId", attributes), ("ModelId", models)):
            value = hero.get(field, 0)
            if value and value not in available:
                raise ValueError(f"characters/{hero_id}: {field} references missing ID {value}")
    for skill_id, skill in skills.items():
        for field in ("PreCastFlowId", "CastFlowId"):
            value = skill.get(field, 0)
            if value and value not in flows:
                raise ValueError(f"skills/{skill_id}: {field} references missing flow {value}")


def check_published(tables, documents):
    def same_text(left, right):
        return left.read_text(encoding="utf-8-sig").replace("\r\n", "\n") == \
            right.read_text(encoding="utf-8-sig").replace("\r\n", "\n")

    for root in (UNITY, CONSOLE):
        for name in tables:
            for folder, suffix in (("moba", "json"), ("moba_bytes", "bytes")):
                source = STAGE / ("normalized-json" if suffix == "json" else "bin") / f"{name}.{suffix}"
                published = root / folder / f"{name}.{suffix}"
                if suffix == "json" and root == CONSOLE and f"moba/{name}.json" in documents:
                    if (not published.exists() or
                            json.loads(published.read_text(encoding="utf-8-sig")) !=
                            json.loads(documents[f"moba/{name}.json"])):
                        raise ValueError(f"published artifact differs: {published}")
                    continue
                if not published.exists() or (not same_text(source, published) if suffix == "json" else source.read_bytes() != published.read_bytes()):
                    raise ValueError(f"published artifact differs: {published}")
    for name, content in documents.items():
        for root in (RESOURCES, CONSOLE):
            path = root / name
            if not path.exists() or json.loads(path.read_text(encoding="utf-8-sig")) != json.loads(content):
                raise ValueError(f"published resource differs: {path}")
    promoted = json.loads(TABLE_KEYS.read_text(encoding="utf-8")) if TABLE_KEYS.exists() else {}
    for name in promoted:
        source = STAGE / "normalized-json" / f"{name}.json"
        for root in (RESOURCES, CONSOLE):
            path = root / "moba" / f"{name}.json"
            expected = documents.get(f"moba/{name}.json")
            if not path.exists() or (json.loads(path.read_text(encoding="utf-8-sig")) != json.loads(expected)
                                     if expected is not None else not same_text(source, path)):
                raise ValueError(f"published promoted table differs: {path}")
    for file in (STAGE / "code").rglob("*.cs"):
        path = CODE / file.relative_to(STAGE / "code")
        if not path.exists() or not same_text(file, path):
            raise ValueError(f"published generated code differs: {path}")


def publish(source, target, extension):
    target.mkdir(parents=True, exist_ok=True)
    for file in source.rglob(f"*.{extension}"):
        destination = target / file.relative_to(source)
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(file, destination)


def obsolete_document_artifacts():
    files = [root / "moba_bytes/resource_documents.bytes" for root in (UNITY, CONSOLE)]
    files += [CODE / name for name in ("DRResourceDocuments.cs", "ResourceDocuments.cs")]
    files += [UNITY / "moba_bytes/resource_documents.bytes.meta"]
    files += [CODE / name for name in ("DRResourceDocuments.cs.meta", "ResourceDocuments.cs.meta")]
    return files


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--check-baseline", action="store_true")
    parser.add_argument("--check-published", action="store_true")
    args = parser.parse_args()
    tables = json.loads((PRODUCTION / "complex_columns.json").read_text(encoding="utf-8"))
    roots = json.loads(TABLE_ROOTS.read_text(encoding="utf-8")) if TABLE_ROOTS.exists() else {}
    for name in ("json", "bin", "code", "normalized-json", "ability"):
        target = STAGE / name
        if target.exists():
            shutil.rmtree(target)
        target.mkdir(parents=True)
    run_luban("-d", STAGE / "json")
    run_luban("-d", STAGE / "bin")
    run_luban("-c", STAGE / "code")
    for name, complex_columns in tables.items():
        rows = json.loads((STAGE / "json" / f"{name}.json").read_text(encoding="utf-8"))
        normalized = [normalize_row(row, complex_columns) for row in rows]
        (STAGE / "normalized-json" / f"{name}.json").write_text(
            json.dumps(normalized, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    verify_tables(tables, STAGE / "normalized-json", args.check_baseline, roots)
    validate_references()
    if "ability_nodes" in tables:
        documents = build_ability_documents()
        for name in roots:
            rows = json.loads((STAGE / "normalized-json" / f"{name}.json").read_text(encoding="utf-8"))
            documents[f"moba/{name}.json"] = json.dumps(
                restored_table(name, rows, roots), ensure_ascii=False, indent=2) + "\n"
        compile_trigger_aggregate(documents)
    else:
        documents = build_resource_documents()
    if args.check_baseline:
        verify_documents(documents)
    if args.check_published:
        check_published(tables, documents)
        if "ability_nodes" in tables:
            for path in obsolete_document_artifacts():
                if path.exists():
                    raise ValueError(f"obsolete resource_documents artifact remains: {path}")
    if args.apply:
        for root in (UNITY, CONSOLE):
            publish(STAGE / "normalized-json", root / "moba", "json")
            publish(STAGE / "bin", root / "moba_bytes", "bytes")
        publish(STAGE / "code", CODE, "cs")
        promoted = json.loads(TABLE_KEYS.read_text(encoding="utf-8")) if TABLE_KEYS.exists() else {}
        for name in promoted:
            source = STAGE / "normalized-json" / f"{name}.json"
            for root in (RESOURCES, CONSOLE):
                destination = root / "moba" / f"{name}.json"
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(source, destination)
        for name, content in documents.items():
            for root in (RESOURCES, CONSOLE):
                destination = root / name
                destination.parent.mkdir(parents=True, exist_ok=True)
                if not destination.exists() or destination.read_text(encoding="utf-8-sig") != content:
                    destination.write_text(content, encoding="utf-8")
        if "ability_nodes" in tables:
            for path in obsolete_document_artifacts():
                path.unlink(missing_ok=True)
        print("published Unity and Console Luban artifacts")
    else:
        print(f"validated in {STAGE}; pass --apply to publish")


if __name__ == "__main__":
    main()
