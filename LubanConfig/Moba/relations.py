"""Typed references between Luban tables and editor-owned documents."""

from dataclasses import dataclass


@dataclass(frozen=True)
class ReferenceSpec:
    source: str
    path: str
    target: str
    required: bool = False
    zero_is_empty: bool = True


class RelationGraph:
    def __init__(self, tables, external=()):
        self.rows = dict(tables)
        self.rows.update(external)
        self.index = {}
        self.edges = []
        keys = {"brains": "BrainId", "triggers": "TriggerId", "rules": "id"}
        for name, entries in self.rows.items():
            key = keys.get(name, "Id")
            indexed = {}
            for row in entries:
                if key not in row:
                    continue
                identity = row[key]
                if type(identity) is not int or identity in indexed:
                    raise ValueError(f"{name}: invalid or duplicate ID {identity}")
                indexed[identity] = row
            self.index[name] = indexed

    def check_value(self, owner, row, field, target, required=False, zero_is_empty=True):
        values = row.get(field)
        if values is None:
            values = []
        if not isinstance(values, list):
            values = [values]
        if required and not any(type(value) is int and value != 0 for value in values):
            raise ValueError(f"{owner}: {field} requires a {target} ID")
        for value in values:
            if value is None and not required:
                continue
            if type(value) is not int or value < 0:
                raise ValueError(f"{owner}: {field} requires an integer {target} ID")
            if value == 0 and zero_is_empty:
                continue
            if target not in self.index:
                raise ValueError(f"{owner}: {field} references unknown target table {target}")
            if value not in self.index[target]:
                raise ValueError(f"{owner}: {field} references missing {target} ID {value}")
            self.edges.append({"source": owner, "field": field, "target": target, "targetId": value})

    def validate(self, specs):
        owners = set()
        for spec in specs:
            key = (spec.source, spec.path)
            if key in owners:
                raise ValueError(f"duplicate relation owner: {spec.source}.{spec.path}")
            owners.add(key)
            if spec.source not in self.rows:
                raise ValueError(f"relation source table missing: {spec.source}")
            parts = spec.path.split(".")
            for row in self.rows[spec.source]:
                identity = row.get("Id", row.get("BrainId", row.get("RowId", row.get("TriggerId", "?"))))
                self._walk(row, parts, f"{spec.source}/{identity}", spec)
        return self.edges

    def _walk(self, row, parts, owner, spec):
        head, *tail = parts
        if head.endswith("[]"):
            field = head[:-2]
            children = row.get(field) or []
            if not isinstance(children, list):
                raise ValueError(f"{owner}: {field} must be a list")
            for index, child in enumerate(children):
                if not isinstance(child, dict):
                    raise ValueError(f"{owner}: {field}[{index}] must be an object")
                self._walk(child, tail, f"{owner}/{field}/{index}", spec)
        elif tail:
            child = row.get(head)
            if child is None:
                return
            if not isinstance(child, dict):
                raise ValueError(f"{owner}: {head} must be an object")
            self._walk(child, tail, f"{owner}/{head}", spec)
        else:
            self.check_value(owner, row, head, spec.target, spec.required, spec.zero_is_empty)


def relation_report(edges):
    ordered = sorted(edges, key=lambda edge: (edge["source"], edge["field"], edge["target"], edge["targetId"]))
    reverse = {}
    for edge in ordered:
        reverse.setdefault((edge["target"], edge["targetId"]), []).append(
            {"source": edge["source"], "field": edge["field"]})
    return {"edges": ordered,
            "dependents": [{"target": table, "targetId": identity, "usedBy": reverse[(table, identity)]}
                           for table, identity in sorted(reverse)]}
