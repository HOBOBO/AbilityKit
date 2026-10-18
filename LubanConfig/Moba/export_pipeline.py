"""Build and validate MOBA Luban artifacts from Production/Datas.

Run without --apply to validate into local/luban-export. --apply publishes
generated assets to the Unity package and Console replica.
"""

import argparse
import json
import re
import shutil
import subprocess
from pathlib import Path, PurePosixPath

from tag_excel import REFERENCE_TABLES as TAG_REFERENCE_TABLES
from pipeline_rules import ExportContext, RulePipeline
from publication import PublicationPlan
from relations import RelationGraph, relation_report
from business.actors import ATTRIBUTE_KEYS, validate_attribute_types
from business.skills import validate_skill_level_tables
from business.actors import project_loadouts
from business.tags import project_tag_references, stable_action_id
from business.modifiers import project_modifier_bindings
from tag_bundle import build_tag_bundle_manifest


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
STATE_MACHINE_SOURCE = ROOT / "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaStateMachineAuthoring"
STATE_MACHINE_NAMES = ("character_hfsm", "actor_state_machines")
TAG_TEMPLATE_TABLES = ("continuous_tag_templates",)
REFERENCE_TABLES = {"modifier_templates", "modifier_definitions", "modifier_parameters", "tag_definitions", "attr_types", *TAG_REFERENCE_TABLES,
                    *TAG_TEMPLATE_TABLES}

def run_luban(kind, output):
    command = ["dotnet", str(LUBAN), "-t", "all", kind, "bin" if kind == "-d" and output.name == "bin" else
               "json" if kind == "-d" else "cs-bin", "--conf", str(PRODUCTION / "luban.conf"),
               "-x", ("outputCodeDir=" if kind == "-c" else "outputDataDir=") + str(output)]
    subprocess.run(command, cwd=ROOT, check=True)


def validate_table_inventory(expected, stage, keys, roots):
    names = set(expected)
    for folder, suffix in (("json", "json"), ("bin", "bytes")):
        actual = {path.stem for path in (stage / folder).glob(f"*.{suffix}")}
        if actual != names:
            raise ValueError(f"Luban {folder} table inventory differs: "
                             f"missing={sorted(names - actual)}, unexpected={sorted(actual - names)}")
    for label, registered in (("table_keys", keys), ("table_roots", roots)):
        extra = set(registered) - names
        if extra:
            raise ValueError(f"{label}: unknown tables {sorted(extra)}")


def normalize_row(row, complex_columns, rules=None, table=""):
    omitted = list(filter(None, row.pop("OmittedFields", "").split(",")))
    nulls = list(filter(None, row.pop("NullFields", "").split(",")))
    for field, kind in complex_columns.items():
        value = row.get(field)
        if not isinstance(value, str):
            continue
        if not value:
            row[field] = [] if kind == "array" else None
        else:
            row[field] = json.loads(value)
    if rules is not None:
        row = rules.normalize(table, row)
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


def expand_references(tables):
    project_loadouts(tables)
    resolve_tags = project_tag_references(tables)
    project_modifier_bindings(tables, resolve_tags)


def verify_tables(tables, json_dir, check_baseline, roots):
    baseline = ROOT / "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba"
    keys = json.loads(TABLE_KEYS.read_text(encoding="utf-8")) if TABLE_KEYS.exists() else {}
    for name in tables:
        generated_rows = json.loads((json_dir / f"{name}.json").read_text(encoding="utf-8-sig"))
        generated_ids = [row[keys.get(name, "Id")] for row in generated_rows]
        if len(generated_ids) != len(set(generated_ids)):
            raise ValueError(f"{name}: duplicated IDs")
        if check_baseline and name != "ability_nodes" and name not in REFERENCE_TABLES:
            baseline_path = RESOURCES / "vfx/vfx.json" if name == "vfx" else baseline / f"{name}.json"
            source_rows = json.loads(baseline_path.read_text(encoding="utf-8-sig"))
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


def verify_editor_trigger_publication():
    published = [root / AGGREGATE for root in (RESOURCES, CONSOLE)]
    plans = [json.loads(path.read_text(encoding="utf-8-sig")) for path in published]
    if plans[0] != plans[1]:
        raise ValueError("editor trigger aggregate differs between Unity and Console")
    triggers = plans[0].get("Triggers", [])
    ids = [trigger["TriggerId"] for trigger in triggers]
    if not ids or len(ids) != len(set(ids)):
        raise ValueError("editor trigger aggregate is empty or contains duplicate IDs")
    legacy_actions = ("add_skill_param_modifier", "remove_skill_param_modifiers")
    legacy_ids = {name: stable_action_id(name) for name in legacy_actions}
    for trigger in triggers:
        for action in trigger.get("Actions", []):
            for name, action_id in legacy_ids.items():
                if action.get("ActionId") == action_id:
                    raise ValueError(f"trigger/{trigger['TriggerId']}: legacy modifier action {name} is not publishable")


def verify_editor_flow_publication():
    paths = [root / "moba/skill_flows.json" for root in (RESOURCES, CONSOLE.parent)]
    flows = [json.loads(path.read_text(encoding="utf-8-sig")) for path in paths]
    if flows[0] != flows[1] or not isinstance(flows[0], list) or not flows[0]:
        raise ValueError("Pipeline skill_flows publication differs between Unity and Console")


def verify_state_machine_publication(tables):
    sources = {}
    for name in STATE_MACHINE_NAMES:
        source = STATE_MACHINE_SOURCE / f"{name}.json"
        if not source.exists():
            raise ValueError(f"state machine source missing: {source}")
        sources[name] = json.loads(source.read_text(encoding="utf-8-sig"))
        for root in (RESOURCES, CONSOLE):
            published = root / "moba" / f"{name}.json"
            if not published.exists() or json.loads(published.read_text(encoding="utf-8-sig")) != sources[name]:
                raise ValueError(f"state machine publication differs: {published}")
    profiles = sources["actor_state_machines"]
    if not isinstance(profiles, list) or not profiles:
        raise ValueError("actor_state_machines: no profiles")
    ids = [profile["id"] for profile in profiles]
    if len(ids) != len(set(ids)):
        raise ValueError("actor_state_machines: duplicate profile IDs")
    for projectile in tables["projectiles"]:
        profile_id = projectile.get("StateMachineProfileId")
        if profile_id and profile_id not in ids:
            raise ValueError(f"projectiles/{projectile['Id']}: missing state machine profile {profile_id}")


def verify_documents(documents):
    baseline = ROOT / "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources"
    for name, content in documents.items():
        if name == AGGREGATE:
            continue  # The old aggregate drifted from its individual trigger sources.
        source = json.loads((baseline / name).read_text(encoding="utf-8-sig"))
        if source != json.loads(content):
            raise ValueError(f"{name}: generated JSON differs from the migration baseline")


def validate_reference_graph(tables, flows, triggers, rules=(), tag_report_path=None):
    graph = RelationGraph(tables, {"skill_flows": flows, "triggers": triggers, "rules": rules})
    if not graph.index["skill_flows"]:
        raise ValueError("Pipeline skill_flows.json is empty or has duplicate IDs")
    if not graph.index["triggers"]:
        raise ValueError("Trigger Authoring aggregate is empty or has duplicate IDs")
    pipeline = RulePipeline(PRODUCTION / "export_rules.json")
    graph.validate(pipeline.relations())
    pipeline.validate_graph(
        graph, tables, {"skill_flows": flows, "triggers": triggers, "rules": rules,
                        "tag_report_path": tag_report_path})
    return graph.edges


def validate_references(tables):
    flow_path = RESOURCES / "moba/skill_flows.json"
    trigger_path = RESOURCES / AGGREGATE
    flows = json.loads(flow_path.read_text(encoding="utf-8-sig"))
    triggers = json.loads(trigger_path.read_text(encoding="utf-8-sig"))["Triggers"]
    rules = [json.loads(path.read_text(encoding="utf-8-sig"))
             for path in sorted((RESOURCES / "ability/rules").rglob("*.json"))]
    edges = validate_reference_graph(tables, flows, triggers, rules, STAGE / "tag-usage-report.json")
    (STAGE / "reference-report.json").write_text(
        json.dumps(relation_report(edges), ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def build_publication_plan(tables, documents):
    plan = PublicationPlan((RESOURCES, CONSOLE.parent, CODE))
    tag_manifest = build_tag_bundle_manifest(STAGE / "bin")
    for root in (UNITY, CONSOLE):
        plan.add(root / "moba/tag_bundle_manifest.json", tag_manifest, "tag-bundle")
        for name in tables:
            document = documents.get(f"moba/{name}.json") if root == CONSOLE else None
            content = (document.encode("utf-8") if document is not None else
                       (STAGE / "normalized-json" / f"{name}.json").read_bytes())
            plan.add(root / "moba" / f"{name}.json", content, "luban-table")
            plan.add(root / "moba_bytes" / f"{name}.bytes",
                     (STAGE / "bin" / f"{name}.bytes").read_bytes(), "luban-binary")
    plan.add_tree(STAGE / "code", CODE, "cs", "luban-code")
    promoted = json.loads(TABLE_KEYS.read_text(encoding="utf-8")) if TABLE_KEYS.exists() else {}
    for name in promoted:
        if f"moba/{name}.json" not in documents:
            plan.add(RESOURCES / "moba" / f"{name}.json",
                     (STAGE / "normalized-json" / f"{name}.json").read_bytes(), "promoted-table")
    for name, content in documents.items():
        plan.add(RESOURCES / name, content.encode("utf-8"), "resource-document")
        if not (name.startswith("moba/") and Path(name).stem in tables):
            plan.add(CONSOLE / name, content.encode("utf-8"), "resource-document")
    for name in TAG_TEMPLATE_TABLES:
        plan.add(CONSOLE.parent / "moba" / f"{name}.json",
                 documents[f"moba/{name}.json"].encode("utf-8"), "tag-template")
    return plan


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--check-baseline", action="store_true")
    parser.add_argument("--check-published", action="store_true")
    parser.add_argument("--preview-rule", type=int, metavar="GROUP_ID")
    parser.add_argument("--preview-template", type=int, metavar="TEMPLATE_ID")
    parser.add_argument("--preview-tags", default="", metavar="TAG1,TAG2")
    parser.add_argument("--explain-tags", action="store_true")
    parser.add_argument("--impact-tag", metavar="TAG_NAME")
    args = parser.parse_args()
    rules = RulePipeline(PRODUCTION / "export_rules.json")
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
    keys = json.loads(TABLE_KEYS.read_text(encoding="utf-8")) if TABLE_KEYS.exists() else {}
    validate_table_inventory(tables, STAGE, keys, roots)
    normalized_tables = {}
    for name, complex_columns in tables.items():
        rows = json.loads((STAGE / "json" / f"{name}.json").read_text(encoding="utf-8"))
        normalized_tables[name] = [normalize_row(row, complex_columns, rules, name) for row in rows]
    context = ExportContext(normalized_tables, STAGE, (UNITY, CONSOLE),
                            args.preview_rule, args.preview_tags, args.explain_tags,
                            args.preview_template)
    rules.run("project", context)
    rules.run("transform", context)
    rules.run("validate", context)
    if args.explain_tags:
        print((STAGE / "tag-usage-report.json").read_text(encoding="utf-8"))
    rules.run("generate", context)
    for name, normalized in normalized_tables.items():
        (STAGE / "normalized-json" / f"{name}.json").write_text(
            json.dumps(normalized, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    verify_tables(tables, STAGE / "normalized-json", args.check_baseline, roots)
    validate_references(normalized_tables)
    if args.impact_tag:
        report = json.loads((STAGE / "tag-usage-report.json").read_text(encoding="utf-8"))
        impact = next((row for row in report["tagImpact"] if row["name"] == args.impact_tag), None)
        if impact is None:
            raise ValueError(f"unknown tag for impact preview: {args.impact_tag}")
        print(json.dumps(impact, ensure_ascii=False, indent=2))
    if "ability_nodes" in tables:
        documents = build_ability_documents()
        documents = {name: content for name, content in documents.items()
                     if not name.startswith("ability/triggers/")}
        for name in roots:
            rows = json.loads((STAGE / "normalized-json" / f"{name}.json").read_text(encoding="utf-8"))
            documents[f"moba/{name}.json"] = json.dumps(
                restored_table(name, rows, roots), ensure_ascii=False, indent=2) + "\n"
    else:
        documents = build_resource_documents()
    if "vfx" in tables:
        documents["vfx/vfx.json"] = (STAGE / "normalized-json/vfx.json").read_text(encoding="utf-8")
    for name in TAG_TEMPLATE_TABLES:
        documents[f"moba/{name}.json"] = json.dumps(
            normalized_tables[name], ensure_ascii=False, indent=2) + "\n"
    if args.check_baseline:
        verify_documents(documents)
    if args.check_published or args.apply:
        verify_editor_flow_publication()
        verify_editor_trigger_publication()
        verify_state_machine_publication(normalized_tables)
    plan = build_publication_plan(tables, documents)
    source_files = [*sorted((PRODUCTION / "Datas").glob("*.xlsx")),
                    *sorted((PRODUCTION / "Defines").glob("*.xml")),
                    PRODUCTION / "luban.conf", PRODUCTION / "complex_columns.json",
                    PRODUCTION / "table_keys.json", PRODUCTION / "table_roots.json",
                    PRODUCTION / "export_rules.json"]
    (STAGE / "publication-report.json").write_text(
        json.dumps(plan.report(ROOT, source_files), ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if args.check_published:
        plan.check()
    if args.apply:
        count = plan.apply()
        plan.check()
        print(f"published Unity and Console Luban artifacts: {count} changed")
    else:
        print(f"validated in {STAGE}; pass --apply to publish")


if __name__ == "__main__":
    main()
