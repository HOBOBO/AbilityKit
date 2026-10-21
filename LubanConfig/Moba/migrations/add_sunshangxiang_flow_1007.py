"""Append the 1007 skill Flow definitions to the authoritative Unity asset."""

from __future__ import annotations

import re
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
FLOW = ROOT / "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Configs/Moba/SkillFlowCO.asset"
PUBLISHED = [
    ROOT / "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/skill_flows.json",
    ROOT / "src/AbilityKit.Demo.Moba.Console/Configs/moba/skill_flows.json",
]


def main():
    text = FLOW.read_text(encoding="utf-8")
    if "- Id: 10070001" in text:
        for published in PUBLISHED:
            flows = json.loads(published.read_text(encoding="utf-8-sig"))
            if not any(row["Id"] == 10070001 for row in flows):
                for source_id in (10060001, 10060101, 10060201, 10060301):
                    row = next(item for item in flows if item["Id"] == source_id)
                    clone = json.loads(json.dumps(row, ensure_ascii=False))
                    clone = json.loads(json.dumps(clone, ensure_ascii=False).replace("1006", "1007").replace("yz_", "ssx_"))
                    flows.append(clone)
                published.write_text(json.dumps(flows, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print("1007 skill flows already present")
        return

    data_marker = "  legacyDataList: []\n"
    if data_marker not in text:
        raise RuntimeError("SkillFlowCO.asset data list marker not found")
    data = """  - Id: 10070001
    Name: "\\u5B59\\u5C1A\\u9999-\\u666E\\u901A\\u653B\\u51FB"
    PipelineContinuousTagTemplateId: 0
    Phases:
    - rid: 8479231920601825350
  - Id: 10070101
    Name: "\\u5B59\\u5C1A\\u9999-\\u7FFB\\u6EDA\\u7A81\\u88AD"
    PipelineContinuousTagTemplateId: 0
    Phases:
    - rid: 8479231920601825351
    - rid: 8479231920601825352
    - rid: 8479231920601825353
  - Id: 10070201
    Name: "\\u5B59\\u5C1A\\u9999-\\u7EA2\\u83B2\\u7206\\u5F39"
    PipelineContinuousTagTemplateId: 0
    Phases:
    - rid: 8479231920601825354
    - rid: 8479231920601825355
    - rid: 8479231920601825356
  - Id: 10070301
    Name: "\\u5B59\\u5C1A\\u9999-\\u7A76\\u6781\\u5F29\\u70AE"
    PipelineContinuousTagTemplateId: 0
    Phases:
    - rid: 8479231920601825357
    - rid: 8479231920601825358
    - rid: 8479231920601825359
"""
    text = text.replace(data_marker, data + data_marker, 1)

    # Clone嬴政's compact three-phase flow. Phase IDs, event effect IDs and
    # tags move to the 1007 namespace while common release/commit validation
    # triggers remain shared pipeline definitions.
    blocks = []
    for rid in range(8479231920601825332, 8479231920601825342):
        pattern = rf"^    - rid: {rid}\n.*?(?=^    - rid:|\Z)"
        match = re.search(pattern, text, re.MULTILINE | re.DOTALL)
        if not match:
            raise RuntimeError(f"missing source flow reference {rid}")
        block = match.group(0)
        new_rid = 8479231920601825350 + (rid - 8479231920601825332)
        block = block.replace(str(rid), str(new_rid))
        block = block.replace("1006", "1007").replace("yz_", "ssx_")
        blocks.append(block)

    # The basic attack block is one timeline phase; active skills use release,
    # commit and cast phases. The cloned order matches the RefIds listed above.
    ref_marker = "    - rid: 8479231920601825332\n"
    first_ref = text.find(ref_marker)
    if first_ref < 0:
        raise RuntimeError("flow references marker not found")
    insertion = "".join(blocks)
    text = text + "\n" + insertion
    FLOW.write_text(text, encoding="utf-8")
    # Keep the checked-in runtime copies synchronized when Unity batch mode is
    # unavailable. The entries are cloned from the currently published 1006
    # flow, which is the same transformation performed by the editor exporter.
    for published in PUBLISHED:
        flows = json.loads(published.read_text(encoding="utf-8-sig"))
        if not any(row["Id"] == 10070001 for row in flows):
            for source_id in (10060001, 10060101, 10060201, 10060301):
                row = next(item for item in flows if item["Id"] == source_id)
                clone = json.loads(json.dumps(row, ensure_ascii=False))
                raw = json.dumps(clone, ensure_ascii=False).replace("1006", "1007").replace("yz_", "ssx_")
                clone = json.loads(raw)
                clone["Name"] = clone["Name"].replace("嬴政", "孙尚香")
                flows.append(clone)
            published.write_text(json.dumps(flows, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("added 1007 skill flows")


if __name__ == "__main__":
    main()
