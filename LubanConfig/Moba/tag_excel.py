"""Validate and compile the MOBA tag workbook into runtime documents."""

import re
from pathlib import Path
from xml.etree import ElementTree


TAG_TABLES = ("tag_definitions", "tag_rule_groups", "tag_rule_nodes",
              "tag_result_flags", "tag_result_mappings", "tag_behavior_gates",
              "continuous_tag_templates")
REFERENCE_TABLES = {"tag_rule_groups", "tag_rule_nodes", "tag_result_flags", "tag_result_mappings", "tag_behavior_gates"}
OPERATORS = {1: "Tag", 2: "All", 3: "Any", 4: "Not", 5: "GroupRef"}
TAG_NAME = re.compile(r"[A-Z][A-Za-z0-9]*(?:\.[A-Z][A-Za-z0-9]*)*")
RETIRED_TAG_IDS = {1003, 1004, 1005, *range(1007, 1013), *range(1023, 1038)}
GATE_ENUM_SOURCE = Path(__file__).resolve().parent / "Production/Defines/strong_types.xml"
PUBLISHED_GAMEPLAY_TAG_NAMES = {
    1000: "State", 1001: "State.ControlImmune", 1002: "State.SuperArmor",
    1006: "Debuff.Slow", 1013: "State.Asleep", 1014: "State.Charmed",
    1015: "State.Disabled", 1016: "State.Feared", 1017: "State.Invulnerable",
    1018: "State.Rooted", 1019: "State.Silenced", 1020: "State.Stunned",
    1021: "State.Suppressed", 1022: "State.Untargetable", 1040: "Debuff",
}
TEMPLATE_FIELDS = {
    "continuous_tag_templates": (
        ("ActivationRequiredTagIds", "ActivationRequiredTags"),
        ("ActivationBlockedTagIds", "ActivationBlockedTags"),
        ("ApplicationTagIds", "ApplicationTags"),
        ("RemovalRequiredTagIds", "RemovalRequiredTags"),
        ("RemovalBlockedTagIds", "RemovalBlockedTags"),
        ("OngoingRequiredTagIds", "OngoingRequiredTags"),
        ("OngoingBlockedTagIds", "OngoingBlockedTags")),
}
CONDITION_FIELDS = {
    "continuous_tag_templates": (("ActivationRuleGroupId", "ActivationCondition"),
                                 ("RemovalRuleGroupId", "RemovalCondition"),
                                 ("OngoingRuleGroupId", "OngoingCondition")),
}


def _index(rows, table):
    indexed = {}
    for row in rows:
        identity = row["Id"]
        if type(identity) is not int or identity <= 0 or identity in indexed:
            raise ValueError(f"{table}: invalid or duplicate ID {identity}")
        indexed[identity] = row
    return indexed


def gate_enum_members(path=GATE_ENUM_SOURCE):
    root = ElementTree.parse(path).getroot()
    definition = root.find("./enum[@name='MobaTagBehaviorGate']")
    if definition is None:
        raise ValueError("missing Luban MobaTagBehaviorGate enum")
    members = {}
    for item in definition.findall("var"):
        name = item.get("name")
        identity = int(item.get("value"))
        if (identity <= 0 or identity in members or
                not re.fullmatch(r"[A-Z][A-Za-z0-9_]*", name or "")):
            raise ValueError(f"invalid Luban behavior gate enum member: {name}/{identity}")
        members[identity] = name
    if not members:
        raise ValueError("Luban behavior gate enum is empty")
    return members


def _condition_references_tags(condition, granted_names):
    if condition["Operator"] == "Tag":
        queried = condition["Tag"]
        return any(name == queried or not condition["Exact"] and name.startswith(queried + ".")
                   for name in granted_names)
    return any(_condition_references_tags(child, granted_names) for child in condition["Children"])


def _carrier_grants(tables):
    for table, field in (("buffs", "TagNames"), ("continuous_processes", "Tags")):
        for row in tables.get(table, []):
            names = row.get(field) or []
            if names:
                yield table, row, names


def validate_carrier_tag_dependencies(tables, compiled_rules):
    templates = {row["Id"]: row for row in tables["continuous_tag_templates"]}
    for table, row, grants in _carrier_grants(tables):
        template_id = row.get("ContinuousTagTemplateId", 0)
        if not template_id:
            continue
        template = templates.get(template_id)
        if template is None:
            continue
        for phase in ("Removal", "Ongoing"):
            for suffix in ("Required", "Blocked"):
                for tag_id in template[phase + suffix + "TagIds"]:
                    tag = next(item for item in tables["tag_definitions"] if item["Id"] == tag_id)
                    if any(name == tag["Name"] or name.startswith(tag["Name"] + ".") for name in grants):
                        raise ValueError(f"{table}/{row['Id']}: {phase} depends on its own direct grant via template {template_id}")
            group_id = template[phase + "RuleGroupId"]
            if group_id and _condition_references_tags(compiled_rules[group_id], grants):
                raise ValueError(f"{table}/{row['Id']}: {phase} rule depends on its own direct grant via template {template_id}")


def validate_tag_definitions(rows):
    tags = _index(rows, "tag_definitions")
    names = {}
    for identity, row in tags.items():
        name = row.get("Name")
        namespace = row.get("Namespace")
        parent_id = row.get("ParentId")
        display = row.get("DisplayName")
        kind = row.get("Kind")
        status = row.get("Status")
        if not isinstance(name, str) or name in names:
            raise ValueError(f"tag_definitions/{identity}: duplicate or empty name")
        names[name] = identity
        if not isinstance(display, str) or not re.search(r"[\u4e00-\u9fff]", display):
            raise ValueError(f"tag_definitions/{identity}: missing Chinese DisplayName")
        if type(parent_id) is not int or parent_id < 0:
            raise ValueError(f"tag_definitions/{identity}: invalid ParentId")
        if kind not in (0, 1) or status not in (0, 1):
            raise ValueError(f"tag_definitions/{identity}: invalid Kind or Status")
        if namespace == 1:
            if (not 1000 <= identity <= 1999 or identity in RETIRED_TAG_IDS or
                    not TAG_NAME.fullmatch(name) or name.split(".")[0] not in ("State", "Debuff") or
                    name == "State" and identity != 1000 or name == "Debuff" and identity != 1040 or
                    identity < 1100 and PUBLISHED_GAMEPLAY_TAG_NAMES.get(identity) != name or
                    1100 <= identity < 1500 and not name.startswith("State.") or
                    identity >= 1500 and not name.startswith("Debuff.")):
                raise ValueError(f"tag_definitions/{identity}: invalid gameplay tag ID or name")
            if identity < 1100 and (status != 0 or kind != (0 if identity in (1000, 1040) else 1)):
                raise ValueError(f"tag_definitions/{identity}: published tag Kind or Status changed")
        elif namespace == 2:
            if (not (identity == 90 or 2000 <= identity <= 2999) or
                    not TAG_NAME.fullmatch(name) or
                    (name == "Skill") != (identity == 90) or
                    name != "Skill" and not name.startswith("Skill.")):
                raise ValueError(f"tag_definitions/{identity}: invalid skill tag ID or name")
            if identity == 90 and (kind != 0 or status != 0):
                raise ValueError("tag_definitions/90: Skill root must be an active category")
        else:
            raise ValueError(f"tag_definitions/{identity}: invalid Namespace")

    for identity, row in tags.items():
        name = row["Name"]
        parent_name = name.rpartition(".")[0]
        expected_parent = names.get(parent_name, 0) if parent_name else 0
        if row["ParentId"] != expected_parent or parent_name and not expected_parent:
            raise ValueError(f"tag_definitions/{identity}: ParentId must match dotted name")
        if expected_parent and tags[expected_parent]["Namespace"] != row["Namespace"]:
            raise ValueError(f"tag_definitions/{identity}: parent crosses Namespace")
        if expected_parent and (tags[expected_parent]["Kind"] != 0 or
                                row["Status"] == 0 and tags[expected_parent]["Status"] != 0):
            raise ValueError(f"tag_definitions/{identity}: parent must be an active category for active children")
    return tags


def compile_tag_tables(tables, compiled_rules=None):
    tags = validate_tag_definitions(tables["tag_definitions"])
    groups = _index(tables["tag_rule_groups"], "tag_rule_groups")
    nodes = _index(tables["tag_rule_nodes"], "tag_rule_nodes")
    for identity in groups:
        if not (2001 <= identity <= 2999 or 10000000 <= identity <= 19999999):
            raise ValueError(f"tag_rule_groups/{identity}: invalid ID range")
    flags = _index(tables["tag_result_flags"], "tag_result_flags")
    mappings = _index(tables["tag_result_mappings"], "tag_result_mappings")
    children = {}
    for node in nodes.values():
        identity, group_id, parent = node["Id"], node["GroupId"], node["ParentId"]
        if group_id not in groups:
            raise ValueError(f"tag_rule_nodes/{identity}: missing group {group_id}")
        if identity // 100 != group_id or not 1 <= identity % 100 <= 99:
            raise ValueError(f"tag_rule_nodes/{identity}: ID must use group block")
        if parent and (parent not in nodes or nodes[parent]["GroupId"] != group_id):
            raise ValueError(f"tag_rule_nodes/{identity}: invalid parent {parent}")
        if node["Operator"] not in OPERATORS:
            raise ValueError(f"tag_rule_nodes/{identity}: invalid operator")
        children.setdefault((group_id, parent), []).append(node)
    for group_id, group in groups.items():
        root = group["RootNodeId"]
        if root not in nodes or nodes[root]["GroupId"] != group_id or nodes[root]["ParentId"] != 0:
            raise ValueError(f"tag_rule_groups/{group_id}: invalid root")
        roots = [node for node in nodes.values() if node["GroupId"] == group_id and node["ParentId"] == 0]
        if len(roots) != 1:
            raise ValueError(f"tag_rule_groups/{group_id}: requires exactly one root")
    for (group_id, parent), siblings in children.items():
        order = [node["Order"] for node in siblings]
        if len(order) != len(set(order)):
            raise ValueError(f"tag_rule_nodes/{group_id}/{parent}: duplicate sibling order")
        siblings.sort(key=lambda row: row["Order"])

    visited = set()

    def compile_group(group_id, stack, depth, count):
        if group_id not in groups:
            raise ValueError(f"tag_rule_groups: missing group {group_id}")
        if group_id in stack:
            raise ValueError(f"tag_rule_groups/{group_id}: cyclic group reference")
        return compile_node(groups[group_id]["RootNodeId"], stack + (group_id,), depth, count)

    def compile_node(node_id, stack, depth, count):
        count[0] += 1
        if depth >= 16 or count[0] > 64:
            raise ValueError(f"tag_rule_nodes/{node_id}: rule exceeds depth or node limit")
        node = nodes[node_id]
        visited.add(node_id)
        operator = OPERATORS[node["Operator"]]
        descendants = children.get((node["GroupId"], node_id), [])
        tag_id, ref_id, exact = node["TagId"], node["RefGroupId"], node["Exact"]
        if operator == "Tag":
            if (descendants or ref_id or tag_id not in tags or tags[tag_id]["Namespace"] == 2 or
                    tags[tag_id]["Status"] != 0 or tags[tag_id]["Kind"] == 0 and exact):
                raise ValueError(f"tag_rule_nodes/{node_id}: invalid gameplay tag leaf")
            return {"Operator": "Tag", "Tag": tags[tag_id]["Name"], "Exact": bool(exact)}
        if operator == "GroupRef":
            if descendants or tag_id or exact or not ref_id:
                raise ValueError(f"tag_rule_nodes/{node_id}: invalid group reference")
            return compile_group(ref_id, stack, depth + 1, count)
        if tag_id or ref_id or exact or not descendants or operator == "Not" and len(descendants) != 1:
            raise ValueError(f"tag_rule_nodes/{node_id}: invalid compound node")
        return {"Operator": operator, "Children": [
            compile_node(child["Id"], stack, depth + 1, count) for child in descendants]}

    compiled = {identity: compile_group(identity, (), 0, [0]) for identity in groups}
    if compiled_rules is not None:
        compiled_rules.update(compiled)
    if len(visited) != len(nodes):
        raise ValueError("tag_rule_nodes: orphan or cyclic nodes")

    keys = [row["Key"] for row in flags.values()]
    bits = [row["BitIndex"] for row in flags.values()]
    if any(not re.fullmatch(r"[A-Z][A-Za-z0-9_]*", key or "") or key == "None" for key in keys) or len(keys) != len(set(keys)):
        raise ValueError("tag_result_flags: invalid or duplicate Key")
    if any(type(bit) is not int or bit < 0 or bit >= 64 for bit in bits) or len(bits) != len(set(bits)):
        raise ValueError("tag_result_flags: invalid or duplicate BitIndex")
    for identity, row in mappings.items():
        if row["FlagId"] not in flags or row["RuleGroupId"] not in groups:
            raise ValueError(f"tag_result_mappings/{identity}: missing flag or rule group")
    if len({(row["FlagId"], row["RuleGroupId"]) for row in mappings.values()}) != len(mappings):
        raise ValueError("tag_result_mappings: duplicate flag and rule group pair")
    if {row["FlagId"] for row in mappings.values()} != set(flags):
        raise ValueError("tag_result_flags: every flag needs at least one mapping")

    gates = _index(tables["tag_behavior_gates"], "tag_behavior_gates")
    gate_members = gate_enum_members()
    for identity, row in gates.items():
        blocked = row["BlockedFlagIds"]
        if identity not in gate_members:
            raise ValueError(f"tag_behavior_gates/{identity}: ID missing from Luban enum")
        if not blocked or len(blocked) != len(set(blocked)) or any(flag_id not in flags for flag_id in blocked):
            raise ValueError(f"tag_behavior_gates/{identity}: invalid BlockedFlagIds")

    normalized = {}

    for table in ("continuous_tag_templates",):
        rows = _index(tables[table], table)
        output = []
        for identity, row in rows.items():
            if not row["Name"]:
                raise ValueError(f"{table}/{identity}: empty name")
            result = {"Id": identity, "Name": row["Name"]}
            for source, target in TEMPLATE_FIELDS[table]:
                ids = row[source]
                if len(ids) != len(set(ids)) or any(
                    tag_id not in tags or tags[tag_id]["Namespace"] == 2 or
                    tags[tag_id]["Status"] != 0 or
                    source == "ApplicationTagIds" and tags[tag_id]["Kind"] != 1
                    for tag_id in ids
                ):
                    raise ValueError(f"{table}/{identity}/{source}: invalid tag IDs")
                if ids:
                    result[target] = [tags[tag_id]["Name"] for tag_id in ids]
            for source, target in CONDITION_FIELDS[table]:
                group_id = row[source]
                if group_id:
                    if group_id not in compiled:
                        raise ValueError(f"{table}/{identity}/{source}: missing rule group")
                    result[target] = compiled[group_id]
            granted_names = [tags[tag_id]["Name"] for tag_id in row["ApplicationTagIds"]]
            for field in ("RemovalRequiredTagIds", "RemovalBlockedTagIds",
                          "OngoingRequiredTagIds", "OngoingBlockedTagIds"):
                if any(granted == tags[tag_id]["Name"] or granted.startswith(tags[tag_id]["Name"] + ".")
                       for tag_id in row[field] for granted in granted_names):
                    raise ValueError(f"{table}/{identity}/{field}: references its own granted tag")
            for field in ("RemovalCondition", "OngoingCondition"):
                if field in result and _condition_references_tags(result[field], granted_names):
                    raise ValueError(f"{table}/{identity}/{field}: references its own granted tag")
            blocked_flags = row.get("ExecutionBlockedFlagIds", [])
            if len(blocked_flags) != len(set(blocked_flags)) or any(flag_id not in flags for flag_id in blocked_flags):
                raise ValueError(f"{table}/{identity}/ExecutionBlockedFlagIds: invalid result flag IDs")
            result["ExecutionBlockedResultMask"] = sum(1 << flags[flag_id]["BitIndex"] for flag_id in blocked_flags)
            output.append(result)
        normalized[table] = output
    dependencies = build_template_dependencies(tables, compiled, tags)
    ongoing = {}
    for edge in dependencies:
        if edge["phase"] == "Ongoing":
            ongoing.setdefault(edge["templateId"], []).append(edge)

    def check_cycle(template_id, path, negative_edges):
        for edge in ongoing.get(template_id, ()):
            target = edge["dependsOnTemplateId"]
            is_negative = edge["polarity"] == "Blocked"
            if target in path:
                if is_negative or any(negative_edges[path.index(target):]):
                    raise ValueError(f"continuous_tag_templates: negative ongoing dependency cycle: {path + (target,)}")
            else:
                check_cycle(target, path + (target,), negative_edges + (is_negative,))

    for template_id in ongoing:
        check_cycle(template_id, (template_id,), ())
    validate_carrier_tag_dependencies(tables, compiled)
    skills = []
    for row in tables.get("skills", []):
        identity = row["Id"]
        blocked_flags = row.get("ExecutionBlockedFlagIds", [])
        if len(blocked_flags) != len(set(blocked_flags)) or any(flag_id not in flags for flag_id in blocked_flags):
            raise ValueError(f"skills/{identity}/ExecutionBlockedFlagIds: invalid result flag IDs")
        skill = dict(row)
        skill.pop("ExecutionBlockedFlagIds", None)
        skill["ExecutionBlockedResultMask"] = sum(1 << flags[flag_id]["BitIndex"] for flag_id in blocked_flags)
        skills.append(skill)
    if "skills" in tables:
        normalized["skills"] = skills
    return normalized


def build_template_dependencies(tables, compiled_rules, tags=None):
    tags = tags or {row["Id"]: row for row in tables["tag_definitions"]}
    templates = tables["continuous_tag_templates"]
    grants = {row["Id"]: [tags[tag_id]["Name"] for tag_id in row["ApplicationTagIds"]]
              for row in templates}
    edges = set()

    def add(template_id, phase, name, exact, polarity):
        for provider_id, granted in grants.items():
            if provider_id != template_id and any(
                    value == name or not exact and value.startswith(name + ".") for value in granted):
                edges.add((template_id, provider_id, phase, name, polarity))

    def scan(template_id, phase, condition, blocked=False):
        operator = condition["Operator"]
        if operator == "Tag":
            add(template_id, phase, condition["Tag"], condition["Exact"],
                "Blocked" if blocked else "Required")
        else:
            for child in condition["Children"]:
                scan(template_id, phase, child, blocked != (operator == "Not"))

    for row in templates:
        template_id = row["Id"]
        for phase in ("Activation", "Removal", "Ongoing"):
            for suffix in ("Required", "Blocked"):
                for tag_id in row[phase + suffix + "TagIds"]:
                    add(template_id, phase, tags[tag_id]["Name"], False, suffix)
            group_id = row[phase + "RuleGroupId"]
            if group_id:
                scan(template_id, phase, compiled_rules[group_id])
    return [{"templateId": source, "dependsOnTemplateId": target, "phase": phase,
             "tag": name, "polarity": polarity}
            for source, target, phase, name, polarity in sorted(edges)]


def build_positive_dependency_cycles(tables, dependencies):
    graph = {}
    for edge in dependencies:
        if edge["phase"] == "Ongoing" and edge["polarity"] == "Required":
            graph.setdefault(edge["templateId"], set()).add(edge["dependsOnTemplateId"])
    direct_grants = {name for _, _, names in _carrier_grants(tables) for name in names}
    cycles = set()

    def visit(node, path):
        for target in graph.get(node, ()):
            if target in path:
                cycles.add(tuple(sorted(path[path.index(target):])))
            elif target not in path:
                visit(target, path + (target,))

    for node in graph:
        visit(node, (node,))
    result = []
    for cycle in sorted(cycles):
        members = set(cycle)
        required = [edge["tag"] for edge in dependencies if edge["templateId"] in members and
                    edge["dependsOnTemplateId"] in members and edge["phase"] == "Ongoing" and
                    edge["polarity"] == "Required"]
        external_template = any(edge["templateId"] in members and edge["dependsOnTemplateId"] not in members
                                and edge["phase"] == "Ongoing" and edge["polarity"] == "Required"
                                for edge in dependencies)
        external_direct = any(name == tag or name.startswith(tag + ".")
                              for name in direct_grants for tag in required)
        result.append({"templateIds": list(cycle), "hasExternalProvider": external_template or external_direct})
    return result


def preview_tag_rule(condition, sample_tags):
    operator = condition["Operator"]
    if operator == "Tag":
        name = condition["Tag"]
        return any(tag == name or not condition["Exact"] and tag.startswith(name + ".")
                   for tag in sample_tags)
    results = [preview_tag_rule(child, sample_tags) for child in condition["Children"]]
    if operator == "All":
        return all(results)
    if operator == "Any":
        return any(results)
    if operator == "Not":
        return not results[0]
    raise ValueError(f"unknown tag rule operator: {operator}")


def build_tag_usage_report(tables, compiled_rules, sample_tags=()):
    """Trace configured grants and the results produced by a sample tag set."""
    tags = {row["Id"]: row for row in tables["tag_definitions"]}
    gate_names = gate_enum_members()
    sample = set(sample_tags)
    known = {row["Name"] for row in tags.values() if row["Namespace"] == 1}
    if sample - known:
        raise ValueError(f"unknown sample gameplay tags: {sorted(sample - known)}")
    flags = {row["Id"]: row for row in tables["tag_result_flags"]}
    group_names = {row["Id"]: row["Name"] for row in tables["tag_rule_groups"]}
    grants = [
        {"templateId": row["Id"], "template": row["Name"],
         "tags": [tags[tag_id]["Name"] for tag_id in row["ApplicationTagIds"]]}
        for row in tables["continuous_tag_templates"] if row["ApplicationTagIds"]
    ]
    template_consumers = [
        {"table": table, "id": row["Id"], "field": field, "templateId": row[field]}
        for table, rows in tables.items() for row in rows
        if table != "continuous_tag_templates" and isinstance(row, dict)
        for field in ("ContinuousTagTemplateId", "PipelineContinuousTagTemplateId")
        if row.get(field, 0) > 0
    ]
    nodes = [
        {"groupId": row["GroupId"], "nodeId": row["Id"], "tag": tags[row["TagId"]]["Name"]}
        for row in tables["tag_rule_nodes"] if row["TagId"]
    ]
    group_references = [
        {"groupId": row["GroupId"], "nodeId": row["Id"], "refGroupId": row["RefGroupId"]}
        for row in tables["tag_rule_nodes"] if row["RefGroupId"]
    ]
    mappings = [
        {"mappingId": row["Id"], "groupId": row["RuleGroupId"],
         "group": group_names[row["RuleGroupId"]], "flagId": row["FlagId"],
         "flag": flags[row["FlagId"]]["Key"],
         "matches": preview_tag_rule(compiled_rules[row["RuleGroupId"]], sample)}
        for row in tables["tag_result_mappings"]
    ]
    active_flags = {entry["flagId"] for entry in mappings if entry["matches"]}
    gates = [
        {"gateId": row["Id"], "key": gate_names[row["Id"]],
         "blockedFlags": [flags[identity]["Key"] for identity in row["BlockedFlagIds"]],
         "blocked": any(identity in active_flags for identity in row["BlockedFlagIds"])}
        for row in tables["tag_behavior_gates"]
    ]
    consumers = [
        {"table": table, "id": row["Id"], "blockedFlags":
         [flags[identity]["Key"] for identity in row.get("ExecutionBlockedFlagIds", [])]}
        for table in ("skills", "continuous_tag_templates") for row in tables.get(table, [])
        if row.get("ExecutionBlockedFlagIds")
    ]
    dependencies = build_template_dependencies(tables, compiled_rules)
    direct_grants = [
        {"table": table, "id": row["Id"], "tags": list(names),
         "templateId": row.get("ContinuousTagTemplateId", 0)}
        for table, row, names in _carrier_grants(tables)
    ]

    def rule_mentions(condition, name):
        if condition["Operator"] == "Tag":
            queried = condition["Tag"]
            return name == queried or not condition["Exact"] and name.startswith(queried + ".")
        return any(rule_mentions(child, name) for child in condition["Children"])

    impacts = []
    for tag in sorted(tags.values(), key=lambda row: row["Id"]):
        name = tag["Name"]
        rule_ids = sorted(identity for identity, rule in compiled_rules.items() if rule_mentions(rule, name))
        flag_ids = {row["FlagId"] for row in tables["tag_result_mappings"]
                    if row["RuleGroupId"] in rule_ids}
        impacts.append({
            "id": tag["Id"], "name": name, "status": "Active" if tag["Status"] == 0 else "Planned",
            "kind": "Fact" if tag["Kind"] == 1 else "Category",
            "grantTemplates": sorted(row["templateId"] for row in grants if name in row["tags"]),
            "directGrantCarriers": [{"table": row["table"], "id": row["id"]}
                                    for row in direct_grants if name in row["tags"]],
            "ruleGroupIds": rule_ids,
            "resultFlags": sorted(flags[identity]["Key"] for identity in flag_ids),
            "behaviorGates": sorted(gate_names[row["Id"]] for row in tables["tag_behavior_gates"]
                                    if any(identity in flag_ids for identity in row["BlockedFlagIds"])),
        })
    return {"sampleTags": sorted(sample), "grantTemplates": grants,
            "templateConsumers": template_consumers,
            "templateDependencies": dependencies,
            "positiveDependencyCycles": build_positive_dependency_cycles(tables, dependencies),
            "directGrantCarriers": direct_grants,
            "authorableGameplayFacts": [row["name"] for row in impacts if row["status"] == "Active" and
                                        row["kind"] == "Fact" and tags[row["id"]]["Namespace"] == 1],
            "tagImpact": impacts,
            "ruleTagNodes": nodes,
            "groupReferences": group_references,
            "resultMappings": mappings, "activeFlags": sorted(flags[identity]["Key"] for identity in active_flags),
            "behaviorGates": gates, "executionConsumers": consumers}


def generate_flags_code(rows):
    entries = sorted(rows, key=lambda row: row["BitIndex"])
    body = "\n".join(f"        {row['Key']} = 1UL << {row['BitIndex']}," for row in entries)
    return ("// <auto-generated>MOBA tag result flags from J-标签表.xlsx.</auto-generated>\n"
            "using System;\n\nnamespace AbilityKit.Demo.Moba.Services\n{\n"
            "    [Flags]\n    public enum MobaTagResultFlags : ulong\n    {\n"
            f"        None = 0,\n{body}\n    }}\n}}\n")


def validate_flag_abi(rows, published):
    current = {row["Key"]: row["BitIndex"] for row in rows}
    for row in published:
        if current.get(row["Key"]) != row["BitIndex"]:
            raise ValueError(f"tag_result_flags: published bit cannot be removed or reassigned: {row['Key']}")


def validate_tag_abi(rows, published):
    current = {row["Id"]: row for row in rows}
    for row in published:
        identity = row["Id"]
        if identity in RETIRED_TAG_IDS:
            continue
        candidate = current.get(identity)
        if candidate is None or any(candidate[field] != row[field]
                                    for field in ("Name", "Namespace", "Kind")):
            raise ValueError(f"tag_definitions: published ID cannot be removed or reassigned: {identity}")
        if row["Status"] == 0 and candidate["Status"] != 0:
            raise ValueError(f"tag_definitions: active published tag cannot become planned: {identity}")


def validate_gate_abi(rows, published):
    current = {row["Id"] for row in rows}
    for row in published:
        if row["Id"] not in current:
            raise ValueError(f"tag_behavior_gates: published ID cannot be removed: {row['Id']}")
