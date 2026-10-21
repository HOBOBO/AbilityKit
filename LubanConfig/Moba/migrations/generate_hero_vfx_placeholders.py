"""Generate Resources-backed placeholder VFX prefabs for 1007/1008.

The demo intentionally uses lightweight authored prefabs, but each configured
VFX ID must still resolve to a real Unity resource. Reusing the existing
projectile and area meshes keeps the assets deterministic and editor-friendly.
"""

from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
EFFECTS = ROOT / "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/effect"

SOURCES = {
    "lianpo_skill1_charge": "zhaoyun_skill1_thunder.prefab",
    "lianpo_skill3_impact": "lianpo_skill2_circle.prefab",
    "sunshangxiang_skill1_roll": "zhaoyun_skill1_thunder.prefab",
    "sunshangxiang_skill2_grenade": "mozi_skill2_cannon.prefab",
    "sunshangxiang_skill2_explosion": "mozi_skill2_crater.prefab",
    "sunshangxiang_skill3_cannon": "zhaoyun_skill3_landing.prefab",
    "sunshangxiang_buff_enhanced": "zhaoyun_skill1_thunder.prefab",
    "sunshangxiang_buff_mark": "daji_skill2_charm_area.prefab",
    "luban_basic_bullet": "bullet_1.prefab",
    "luban_skill1_grenade": "mozi_skill2_cannon.prefab",
    "luban_skill1_explosion": "mozi_skill2_crater.prefab",
    "luban_skill2_cannon": "zhaoyun_skill2_spear.prefab",
    "luban_passive_sweep": "xiaoqiao_skill1_fan.prefab",
    "luban_skill3_bomb": "daji_skill3_foxfire.prefab",
}


def main():
    for name, source_name in SOURCES.items():
        source = EFFECTS / source_name
        target = EFFECTS / (name + ".prefab")
        text = source.read_text(encoding="utf-8")
        text = text.replace(source.stem, name)
        target.write_text(text, encoding="utf-8", newline="\n")


if __name__ == "__main__":
    main()
