using AbilityKit.Triggering.Blackboard;
using Newtonsoft.Json;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class BlackboardSnapshotJsonCodecTests
{
    [Fact]
    public void Keeps_existing_json_shape_for_owner_and_set()
    {
        var owner = new BlackboardSnapshot
        {
            OwnerKey = 41,
            Boards = new List<BlackboardSnapshotBoard>
            {
                new()
                {
                    BoardId = 51,
                    Entries = new List<BlackboardSnapshotEntry>
                    {
                        new() { KeyId = 61, Type = BlackboardKeyType.String, HasValue = true,
                            ValueKind = BlackboardSnapshotValueKind.String, StringValue = "value\n☃" },
                        new() { KeyId = 62, Type = BlackboardKeyType.Double, HasValue = true,
                            ValueKind = BlackboardSnapshotValueKind.Double, DoubleValue = 2.125 },
                        BlackboardSnapshotEntry.Missing(63, BlackboardKeyType.Bool)
                    }
                }
            }
        };
        var set = new BlackboardSnapshotSet { Owners = new List<BlackboardSnapshot> { owner } };
        var oldOwnerJson = JsonConvert.SerializeObject(owner, Formatting.None);
        var oldSetJson = JsonConvert.SerializeObject(set, Formatting.None);

        Assert.Equal(oldOwnerJson, BlackboardSnapshotJsonCodec.Serialize(owner));
        Assert.Equal(oldSetJson, BlackboardSnapshotJsonCodec.Serialize(set));
        Assert.Equal(owner.Boards[0].Entries, BlackboardSnapshotJsonCodec.DeserializeSnapshot(oldOwnerJson).Boards[0].Entries);
        Assert.Equal(oldSetJson, BlackboardSnapshotJsonCodec.Serialize(BlackboardSnapshotJsonCodec.DeserializeSet(oldSetJson)));
    }

    [Fact]
    public void Rejects_missing_and_malformed_json()
    {
        Assert.Throws<ArgumentException>(() => BlackboardSnapshotJsonCodec.DeserializeSet(" "));
        Assert.Throws<InvalidOperationException>(() => BlackboardSnapshotJsonCodec.DeserializeSnapshot("null"));
        Assert.Throws<InvalidOperationException>(() => BlackboardSnapshotJsonCodec.DeserializeSet("null"));
        Assert.ThrowsAny<JsonException>(() => BlackboardSnapshotJsonCodec.DeserializeSet("{"));
    }
}
