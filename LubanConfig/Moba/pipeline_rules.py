"""Deterministic extension points for the MOBA Luban export pipeline."""

import importlib
import json
from dataclasses import dataclass, field
from pathlib import Path

from relations import ReferenceSpec


@dataclass
class ExportContext:
    tables: dict
    stage: Path
    published_roots: tuple[Path, ...]
    preview_rule: int | None = None
    preview_tags: str = ""
    explain_tags: bool = False
    state: dict = field(default_factory=dict)


class RulePipeline:
    """Rules run in manifest order; every generated code file has one owner."""

    def __init__(self, manifest: Path):
        entries = json.loads(manifest.read_text(encoding="utf-8"))["rules"]
        if not isinstance(entries, list) or not entries:
            raise ValueError(f"{manifest}: rules must be a nonempty, unique list")
        definitions = {}
        for entry in entries:
            name, after = (entry, []) if isinstance(entry, str) else (entry.get("module"), entry.get("after", []))
            if (not isinstance(name, str) or not name.startswith("business.") or
                    not isinstance(after, list) or any(not isinstance(item, str) for item in after) or
                    name in definitions):
                raise ValueError(f"{manifest}: invalid or duplicate business rule {name!r}")
            definitions[name] = after
        ordered = []
        visiting = set()

        def visit(name):
            if name not in definitions:
                raise ValueError(f"{manifest}: missing business rule dependency {name}")
            if name in visiting:
                raise ValueError(f"{manifest}: cyclic business rule dependency {name}")
            if name in ordered:
                return
            visiting.add(name)
            for dependency in definitions[name]:
                visit(dependency)
            visiting.remove(name)
            ordered.append(name)

        for name in definitions:
            visit(name)
        self.rules = []
        for name in ordered:
            module = importlib.import_module(name)
            self.rules.append(module.Rule())

    def normalize(self, table: str, row: dict) -> dict:
        for rule in self.rules:
            method = getattr(rule, "normalize", None)
            if method is not None:
                result = method(table, row)
                if result is not None:
                    row = result
        return row

    def reference_specs(self):
        fields = {}
        required = set()
        for rule in self.rules:
            method = getattr(rule, "references", None)
            if method is None:
                continue
            mappings, mandatory = method()
            for table, references in mappings.items():
                owner = fields.setdefault(table, {})
                for field, target in references.items():
                    if field in owner:
                        raise ValueError(f"duplicate reference owner: {table}.{field}")
                    owner[field] = target
            required.update(mandatory)
        if any(table not in fields or field not in fields[table] for table, field in required):
            raise ValueError("required reference is not declared")
        return fields, required

    def relations(self):
        fields, required = self.reference_specs()
        specs = [ReferenceSpec(table, field, target, (table, field) in required)
                 for table, references in fields.items() for field, target in references.items()]
        for rule in self.rules:
            method = getattr(rule, "nested_relations", None)
            if method is not None:
                specs.extend(method())
        return specs

    def validate_graph(self, graph, tables, external):
        for rule in self.rules:
            method = getattr(rule, "validate_graph", None)
            if method is not None:
                method(graph, tables, external)

    def run(self, phase: str, context: ExportContext) -> None:
        if phase not in ("project", "transform", "validate", "generate"):
            raise ValueError(f"unknown export phase: {phase}")
        generated = {}
        generated_keys = set()
        for rule in self.rules:
            method = getattr(rule, phase, None)
            if method is None:
                continue
            result = method(context)
            if phase != "generate":
                if result is not None:
                    raise ValueError(f"{type(rule).__name__}.{phase} must not return artifacts")
                continue
            for name, content in (result or {}).items():
                path = Path(name)
                if path.name != name or path.suffix != ".cs" or name.casefold() in generated_keys:
                    raise ValueError(f"invalid or duplicate generated code artifact: {name}")
                if not isinstance(content, str):
                    raise ValueError(f"generated code artifact must be text: {name}")
                generated[name] = content
                generated_keys.add(name.casefold())
        collisions = [name for name in generated if (context.stage / "code" / name).exists()]
        if collisions:
            raise ValueError(f"generated code conflicts with Luban output: {collisions}")
        for name, content in generated.items():
            (context.stage / "code" / name).write_text(content, encoding="utf-8")
