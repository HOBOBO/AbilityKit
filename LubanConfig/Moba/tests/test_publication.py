"""Publication must reject ownership collisions and restore partial writes."""

import os
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from publication import PublicationPlan


class PublicationPlanTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.plan = PublicationPlan((self.root,))

    def test_target_must_have_one_owner_and_stay_inside_root(self):
        path = self.root / "data.json"
        self.plan.add(path, b"{}", "first")
        with self.assertRaisesRegex(ValueError, "duplicate publication target"):
            self.plan.add(path, b"{}", "second")
        with self.assertRaisesRegex(ValueError, "outside owned roots"):
            self.plan.add(self.root.parent / "outside.json", b"{}", "other")

    def test_check_compares_json_semantically(self):
        path = self.root / "data.json"
        path.write_text('{"value": 1}', encoding="utf-8")
        self.plan.add(path, b'{\n  "value": 1\n}', "table")
        self.plan.check()
        self.assertEqual(0, self.plan.apply())
        report = self.plan.report(self.root, (path,))
        self.assertEqual("data.json", report["outputs"][0]["path"])
        self.assertEqual([], report["changes"])

    def test_failed_second_write_restores_first_and_retired_file(self):
        old = self.root / "old.json"
        old.write_bytes(b'{"old": true}')
        retired = self.root / "retired.json"
        retired.write_bytes(b'{"retired": true}')
        added = self.root / "added.json"
        self.plan.add(old, b'{"new": true}', "table")
        self.plan.remove(retired, "retired")
        self.plan.add(added, b'{"added": true}', "table")
        replace = os.replace
        calls = 0

        def fail_once(source, destination):
            nonlocal calls
            calls += 1
            if calls == 2:
                raise OSError("injected failure")
            return replace(source, destination)

        with patch("publication.os.replace", side_effect=fail_once):
            with self.assertRaisesRegex(OSError, "was restored"):
                self.plan.apply()
        self.assertEqual(b'{"old": true}', old.read_bytes())
        self.assertEqual(b'{"retired": true}', retired.read_bytes())
        self.assertFalse(added.exists())
        self.assertFalse(list(self.root.glob(".moba-publish-*")))
