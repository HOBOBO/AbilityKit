"""One-time append of Hit damage and summon lifetime parameters."""

from copy import copy
from pathlib import Path

from openpyxl import load_workbook


def main():
    workbook_path = Path(__file__).resolve().parents[1] / "Production/Datas/J-效果表.xlsx"
    workbook = load_workbook(workbook_path)
    sheet = workbook["修饰参数"]
    ids = {sheet.cell(row, 2).value for row in range(4, sheet.max_row + 1)}
    additions = (
        (15, "Damage", "RequestedBase", 33, 1, "Float", 15, False, "Hit"),
        (16, "Summon", "LifetimeMs", 40, 6, "Integer", 15, False, "SummonCreate"),
    )
    changed = False
    for identity, *fields in additions:
        if identity in ids:
            continue
        row = sheet.max_row + 1
        for column, value in enumerate((None, identity, *fields), 1):
            previous = sheet.cell(row - 1, column)
            cell = sheet.cell(row, column, value)
            if previous.has_style:
                cell._style = copy(previous._style)
        changed = True
    definitions = workbook["修饰器定义"]
    headers = {definitions.cell(1, column).value: column
               for column in range(1, definitions.max_column + 1)}
    for row in range(4, definitions.max_row + 1):
        if definitions.cell(row, headers["Id"]).value != 20:
            continue
        if (definitions.cell(row, headers["TargetKind"]).value == "SkillSlotCandidate" and
                definitions.cell(row, headers["TargetId"]).value == 4):
            break
        definitions.cell(row, headers["TargetKind"], "SkillSlotCandidate")
        definitions.cell(row, headers["TargetId"], 4)
        definitions.cell(row, headers["Value"], 0)
        definitions.cell(row, headers["Op"], "Add")
        definitions.cell(row, headers["CandidateSkillId"], 10040112)
        changed = True
        break
    if changed:
        workbook.save(workbook_path)


if __name__ == "__main__":
    main()
