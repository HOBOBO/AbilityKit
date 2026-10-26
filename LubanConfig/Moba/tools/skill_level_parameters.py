"""Read the flat skill parameter authoring table and rebuild Luban's nested table."""

from collections import defaultdict
from pathlib import Path

from openpyxl import load_workbook


def _value(row, index):
    return row[index] if index < len(row) else None


def read_parameter_rows(path):
    workbook = load_workbook(path, data_only=False, read_only=True)
    try:
        sheet = workbook[workbook.sheetnames[0]]
        rows = list(sheet.iter_rows(values_only=True))
    finally:
        workbook.close()
    if len(rows) < 4:
        raise ValueError(f"{path}: skill parameter table must contain four header rows")
    headers = [str(value).strip() if value is not None else "" for value in rows[0]]
    required = ("Id", "SkillId", "Level", "ParamIndex", "ParamName", "Value")
    missing = [name for name in required if name not in headers]
    if missing:
        raise ValueError(f"{path}: missing columns {missing}")
    columns = {name: headers.index(name) for name in required}
    result = []
    seen = set()
    seen_ids = set()
    for row_number, row in enumerate(rows[3:], 4):
        if all(value in (None, "") for value in row):
            continue
        item = {name: _value(row, index) for name, index in columns.items()}
        try:
            item["Id"] = int(item["Id"])
            item["SkillId"] = int(item["SkillId"])
            item["Level"] = int(item["Level"])
            item["ParamIndex"] = int(item["ParamIndex"])
            item["Value"] = float(item["Value"])
        except (TypeError, ValueError) as error:
            raise ValueError(f"{path}: row {row_number} contains invalid numeric data") from error
        item["ParamName"] = str(item["ParamName"] or "").strip()
        key = (item["SkillId"], item["Level"], item["ParamIndex"])
        if item["Id"] in seen_ids:
            raise ValueError(f"{path}: duplicate row Id {item['Id']}")
        seen_ids.add(item["Id"])
        if key in seen:
            raise ValueError(f"{path}: duplicate skill parameter row {key}")
        seen.add(key)
        if item["SkillId"] <= 0 or item["Level"] <= 0 or item["ParamIndex"] < 0 or not item["ParamName"]:
            raise ValueError(f"{path}: row {row_number} has invalid skill, level, index, or name")
        result.append(item)
    return result


def _read_nested_metadata(path):
    workbook = load_workbook(path, data_only=False, read_only=True)
    try:
        sheet = next((item for item in workbook.worksheets
                      if any(cell.value == "ParamNames" for cell in item[1])), None)
        if sheet is None:
            raise ValueError(f"{path}: cannot find the skill level worksheet")
        result = {}
        current_id = None
        level = 0
        for row in sheet.iter_rows(min_row=5, values_only=True):
            if all(value in (None, "") for value in row):
                continue
            if row[1] not in (None, ""):
                current_id = int(row[1])
                level = 0
            if current_id is None:
                continue
            level += 1
            key = (current_id, level)
            if key in result:
                raise ValueError(f"{path}: duplicate skill level metadata {key}")
            result[key] = (int(row[2] or 0), int(row[3] or 0))
        return result
    finally:
        workbook.close()


def materialize_nested_workbook(flat_path, nested_path):
    rows = read_parameter_rows(flat_path)
    metadata = _read_nested_metadata(nested_path)
    grouped = defaultdict(list)
    for row in rows:
        grouped[row["SkillId"]].append(row)
    for skill_id, entries in grouped.items():
        names = {}
        levels = defaultdict(list)
        for entry in entries:
            index = entry["ParamIndex"]
            name = entry["ParamName"]
            if index in names and names[index] != name:
                raise ValueError(f"skill_level_parameters/{skill_id}: ParamIndex {index} has multiple names")
            if name in names.values() and names.get(index) != name:
                raise ValueError(f"skill_level_parameters/{skill_id}: ParamName {name} has multiple indexes")
            names[index] = name
            levels[entry["Level"]].append(entry)
        expected_indexes = list(range(len(names)))
        if sorted(names) != expected_indexes:
            raise ValueError(f"skill_level_parameters/{skill_id}: ParamIndex must be contiguous from zero")
        for level, values in levels.items():
            if sorted(item["ParamIndex"] for item in values) != expected_indexes:
                raise ValueError(f"skill_level_parameters/{skill_id}/Level/{level}: parameter set is incomplete")
        if sorted(levels) != list(range(1, max(levels) + 1)):
            raise ValueError(f"skill_level_parameters/{skill_id}: levels must be contiguous from one")
        metadata_levels = {level for table_id, level in metadata if table_id == skill_id}
        if set(levels) != metadata_levels:
            raise ValueError(
                f"skill_level_parameters/{skill_id}: configured levels {sorted(levels)} "
                f"do not match skill level metadata {sorted(metadata_levels)}")

    for skill_id, level in metadata:
        grouped.setdefault(skill_id, [])

    workbook = load_workbook(nested_path)
    try:
        sheet = next((item for item in workbook.worksheets
                      if any(cell.value == "ParamNames" for cell in item[1])), None)
        if sheet is None:
            raise ValueError(f"{nested_path}: cannot find the skill level worksheet")
        while sheet.max_row > 4:
            sheet.delete_rows(5)
        row_index = 5
        for skill_id in sorted(grouped):
            entries = grouped[skill_id]
            names_by_index = {entry["ParamIndex"]: entry["ParamName"] for entry in entries}
            level_numbers = sorted({level for table_id, level in metadata if table_id == skill_id})
            for level in level_numbers:
                values = {entry["ParamIndex"]: entry["Value"] for entry in levels_for(entries, level)}
                cooldown, cost = metadata.get((skill_id, level), (0, 0))
                sheet.cell(row_index, 2).value = skill_id if level == min(level_numbers) else None
                sheet.cell(row_index, 3).value = cooldown
                sheet.cell(row_index, 4).value = cost
                sheet.cell(row_index, 5).value = ",".join(str(values[index]) for index in range(len(names_by_index))) if names_by_index else None
                sheet.cell(row_index, 6).value = ",".join(names_by_index[index] for index in range(len(names_by_index))) if level == min(level_numbers) and names_by_index else None
                row_index += 1
        workbook.save(nested_path)
    finally:
        workbook.close()


def levels_for(entries, level):
    return [entry for entry in entries if entry["Level"] == level]
