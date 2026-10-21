import unittest

from business.buffs import validate_buff_hud_templates


class BuffHudTableTests(unittest.TestCase):
    def test_accepts_display_rows_for_existing_buffs(self):
        validate_buff_hud_templates(
            [{"Id": 101, "Name": "Speed"}],
            [{"Id": 101}])

    def test_rejects_unknown_buff_id(self):
        with self.assertRaisesRegex(ValueError, "missing Buff 999"):
            validate_buff_hud_templates(
                [{"Id": 999, "Name": "Unknown"}],
                [{"Id": 101}])

    def test_requires_display_name(self):
        with self.assertRaisesRegex(ValueError, "Name is required"):
            validate_buff_hud_templates(
                [{"Id": 101, "Name": ""}],
                [{"Id": 101}])
