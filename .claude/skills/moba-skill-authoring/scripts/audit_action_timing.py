"""Read-only source/export timing inventory; does not validate Flow wiring or Unity playback."""
from __future__ import annotations

import argparse
import json
import math
from pathlib import Path


def number(value):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value) or value < 0:
        raise ValueError(f"Invalid non-negative time: {value!r}")
    return value


def spans(document, *, source):
    result = []
    for group in document.get("groups", []):
        if not group.get("active", source):
            continue
        for track in group.get("tracks", []):
            if not track.get("active", source):
                continue
            for clip in track.get("actionClips" if source else "clips", []):
                clip_type = clip.get("$type" if source else "type")
                start = number(clip.get("startTime" if source else "start", 0))
                # ClipSignal.Length is always zero. Other omitted source lengths
                # require Unity's deserializer/type defaults; do not guess zero.
                signal = source and clip_type in ("AbilityKit.ActionEditorImpl.ExecuteEffect",
                                                  "AbilityKit.ActionEditorImpl.TriggerLog")
                length = 0 if signal else (None if source and "length" not in clip else number(clip.get("length", 0)))
                result.append({"type": clip_type, "start": start, "length": length,
                               "end": None if length is None else start + length})
    return result


def audit(path):
    report = {"source": str(path), "errors": [], "reviews": [], "exports": {}}
    try:
        source = json.loads(path.read_text(encoding="utf-8-sig"))
        if source.get("exportMobaRuntime") is not True:
            report["reviews"].append("runtime_export_not_enabled")
        length = number(source["length"])
        clips = spans(source, source=True)
        report.update(length=length, activeClipCount=len(clips),
                      firstClipStart=min((c["start"] for c in clips), default=None),
                      latestClipEnd=max((c["end"] for c in clips if c["end"] is not None), default=None), clips=clips)
        if not clips:
            report["reviews"].append("empty_timeline_check_actual_playback_owner")
        if any(c["length"] is None for c in clips):
            report["reviews"].append("source_clip_length_missing_check_unity_export")
        if any(c["end"] is not None and c["end"] > length + 0.00001 for c in clips):
            report["reviews"].append("clip_exceeds_timeline_check_full_playback_or_intentional_trim")
        for kind in ("logic", "presentation"):
            exported_path = path.with_name(path.stem + f".moba.{kind}.json")
            try:
                exported = json.loads(exported_path.read_text(encoding="utf-8-sig"))
                exported_length = number(exported["length"])
                exported_clips = spans(exported, source=False)
                report["exports"][kind] = {"path": str(exported_path), "length": exported_length,
                                           "activeClipCount": len(exported_clips), "clips": exported_clips}
                if exported.get("runtimeType") != kind or exported.get("schemaVersion") != 1:
                    report["errors"].append(f"invalid_{kind}_export_header")
                if abs(length - exported_length) > 0.00001:
                    report["errors"].append(f"{kind}_length_differs_from_source")
                if any(c["end"] > exported_length + 0.00001 for c in exported_clips):
                    report["reviews"].append(f"{kind}_clip_exceeds_timeline")
            except (OSError, ValueError, KeyError, TypeError, AttributeError) as error:
                report["errors"].append(f"{kind}_export_unreadable: {error}")
    except (OSError, ValueError, KeyError, TypeError, AttributeError) as error:
        report["errors"].append(f"source_unreadable: {error}")
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--skill-id", type=int, action="append", help="Repeat to select multiple source filenames.")
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[4])
    parser.add_argument("--output", type=Path, help="Optional local JSON report; source files are never modified.")
    args = parser.parse_args()
    folder = args.root / "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/action_timeline"
    if args.skill_id:
        paths = [folder / f"skill_{skill}.json" for skill in sorted(set(args.skill_id))]
    else:
        paths = []
        for path in sorted(folder.glob("skill_*.json")):
            if path.stem.endswith((".logic", ".presentation")):
                continue
            try:
                enabled = json.loads(path.read_text(encoding="utf-8-sig")).get("exportMobaRuntime") is True
            except (OSError, ValueError, AttributeError):
                enabled = True  # Surface unreadable sources rather than silently skipping them.
            if enabled:
                paths.append(path)
    reports = [audit(path) for path in paths]
    summary = {"timelines": len(reports), "errors": sum(len(r["errors"]) for r in reports),
               "reviews": sum(len(r["reviews"]) for r in reports)}
    result = {"scope": "source/export timing only; Flow wiring and real playback require separate checks",
              "summary": summary, "timelines": reports}
    if args.output:
        target = args.output.resolve()
        workspace = args.root.resolve()
        if not target.is_relative_to(workspace / "local") or target.suffix.lower() != ".json":
            parser.error("--output must be a JSON file under the workspace local directory")
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary))
    for report in reports:
        if report["errors"] or report["reviews"]:
            print(Path(report["source"]).name + ": " + "; ".join(report["errors"] + report["reviews"]))
    return 1 if not reports or summary["errors"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
