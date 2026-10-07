"""Projection of battle start enum values into the runtime document."""

from relations import ReferenceSpec


class Rule:
    def nested_relations(self):
        return [ReferenceSpec("battle_start", "Players[].HeroId", "characters", required=True),
                ReferenceSpec("battle_start", "Players[].BrainId", "brains")]

    def references(self):
        return ({"gameplays": {"TriggerIds": "triggers"}}, set())

    def normalize(self, table, row):
        if isinstance(row.get("RunMode"), int) and "Network" in row:
            row["RunMode"] = ("Normal", "Record", "Replay")[row["RunMode"]]
        if isinstance(row.get("SyncMode"), int) and "Network" in row:
            row["SyncMode"] = ("Lockstep", "SnapshotAuthority", "StateSync", "Hybrid")[row["SyncMode"]]
        return row
