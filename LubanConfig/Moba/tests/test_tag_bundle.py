import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from tag_bundle import TAG_BUNDLE_TABLES, build_tag_bundle_manifest


class TagBundleTests(unittest.TestCase):
    def test_manifest_covers_every_tag_behavior_binary_deterministically(self):
        with tempfile.TemporaryDirectory() as directory:
            binary_dir = Path(directory)
            for name in TAG_BUNDLE_TABLES:
                (binary_dir / f"{name}.bytes").write_bytes(name.encode("ascii"))
            first = build_tag_bundle_manifest(binary_dir)
            self.assertEqual(first, build_tag_bundle_manifest(binary_dir))
            manifest = json.loads(first)
            self.assertEqual(set(TAG_BUNDLE_TABLES), set(manifest["tables"]))
            self.assertEqual(hashlib.sha256(b"tag_rule_nodes").hexdigest(),
                             manifest["tables"]["tag_rule_nodes"])
            (binary_dir / "tag_rule_nodes.bytes").write_bytes(b"changed")
            self.assertNotEqual(manifest["bundleVersion"],
                                json.loads(build_tag_bundle_manifest(binary_dir))["bundleVersion"])


if __name__ == "__main__":
    unittest.main()
