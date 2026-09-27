using AbilityKit.Demo.Tiny;
using AbilityKit.Network.Room;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;
using AbilityKit.Protocol.Room;

// This envelope is an isolated migration exercise, not the live Tiny input wire format.
const byte v1 = 1;
const byte v2 = 2;
var input = new TinyInput(1, 0, true);
var legacy = new byte[] { v1 }.Concat(input.Encode()).ToArray();
var current = new byte[] { v2 }.Concat(input.Encode()).Append((byte)7).ToArray();
var decodedLegacy = Decode(legacy);
var decodedCurrent = Decode(current);
Require(decodedLegacy.MoveX == decodedCurrent.MoveX &&
    decodedLegacy.Attack == decodedCurrent.Attack && decodedCurrent.MoveY == 0,
    "V2 reader changed V1 input semantics.");
Require(TryReject(new byte[] { 3, 2, 1, 1 }), "Future version was accepted.");
Require(TryReject(new byte[] { v2, 2, 1, 1 }), "Truncated V2 payload was accepted.");
Require(TryReject(new byte[] { v2, 2, 1, 1, 255 }), "Unknown action was accepted.");
var profile = NetworkSyncProfiles.AuthoritativeInterpolation;
var options = new NetworkSyncSessionOptions
{
    RequiredProfile = profile,
    RequiredMinimumSchemaVersion = 1,
    RequiredMaximumSchemaVersion = 1,
    AvailableCapabilities = NetworkSyncCapabilities.FromProfile(in profile, 1, 1)
};
Require(Binding(1, in profile).Negotiate(options).IsRemoteNegotiated,
    "V1 Room capability was not negotiated.");
Require(RejectSchema(() => Binding(2, in profile).Negotiate(options)),
    "V2-only Room capability was accepted by the V1 client.");
Console.WriteLine("Tiny protocol evolution passed: V1/V2 envelope read, future/truncated/invalid rejected, Room V1 accepted/V2 refused");

static RoomGatewayNetworkSyncSessionBinding Binding(int schemaVersion,
    in NetworkSyncProfile profile)
{
    var capabilities = NetworkSyncCapabilities.FromProfile(in profile,
        schemaVersion, schemaVersion);
    var wire = new WireNetworkSyncCapabilities
    {
        MetadataVersion = 1,
        ProfileName = nameof(NetworkSyncModel.AuthoritativeInterpolation),
        MinimumSchemaVersion = capabilities.MinimumSchemaVersion,
        MaximumSchemaVersion = capabilities.MaximumSchemaVersion,
        ClientPlayback = (int)capabilities.ClientPlayback,
        Input = (int)capabilities.Input,
        Snapshot = (int)capabilities.Snapshot,
        Interest = (int)capabilities.Interest,
        Recovery = (int)capabilities.Recovery,
        ServerValidation = (int)capabilities.ServerValidation,
        ReliableEvent = (int)capabilities.ReliableEvent
    };
    return RoomGatewayNetworkSyncSessionBinding.Create(
        RoomGatewayNetworkSyncCapabilitiesConverter.FromWire(wire),
        nameof(NetworkSyncModel.AuthoritativeInterpolation),
        NetworkSyncRemoteCapabilityPolicy.Require);
}

static bool RejectSchema(Action negotiate)
{
    try { negotiate(); return false; }
    catch (NetworkSyncConfigurationException exception)
    {
        return exception.Report.Issues.Any(issue =>
            issue.Code == NetworkSyncConfigurationIssueCode.SchemaVersionMismatch);
    }
}

static TinyInput Decode(ReadOnlySpan<byte> bytes)
{
    if (bytes.Length == 4 && bytes[0] == 1)
        return TinyInput.Decode(bytes.Slice(1, 3));
    if (bytes.Length == 5 && bytes[0] == 2 && bytes[4] is 0 or 7)
        return TinyInput.Decode(bytes.Slice(1, 3));
    throw new InvalidDataException("Unsupported Tiny teaching envelope.");
}

static bool TryReject(byte[] bytes)
{
    try { Decode(bytes); return false; }
    catch (InvalidDataException) { return true; }
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
