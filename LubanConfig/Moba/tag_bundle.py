"""Version the published binary tables that determine MOBA tag behavior."""

import hashlib
import json

from tag_excel import TAG_TABLES


TAG_BUNDLE_TABLES = tuple(sorted((*TAG_TABLES, "buffs", "continuous_processes", "skills")))


def build_tag_bundle_manifest(binary_dir):
    hashes = {
        name: hashlib.sha256((binary_dir / f"{name}.bytes").read_bytes()).hexdigest()
        for name in TAG_BUNDLE_TABLES
    }
    identity = "".join(f"{name}={digest}\n" for name, digest in hashes.items())
    version = hashlib.sha256(identity.encode("utf-8")).hexdigest()
    return (json.dumps({"schemaVersion": 1, "bundleVersion": version, "tables": hashes},
                       ensure_ascii=False, indent=2) + "\n").encode("utf-8")
