using AbilityKit.Demo.Host.Console;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Network;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Shooter.Host;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.Console;

internal sealed class ShooterConsoleApp : IConsoleHostGame, IDisposable
{
    private const int LocalPlayerId = 1;
    private const int TargetFrameRate = 30;
    private const string WorldId = "console-local";

    private readonly IShooterConsoleInputSource _input;
    private readonly ShooterConsoleRenderer _renderer;
    private readonly ConsoleLog _log;
    private ShooterWorldHost? _worldHost;
    private IWorld? _world;
    private IShooterBattleRuntimePort? _runtime;
    private InProcessHostNetwork? _hostNetwork;
    private ConnectionManager? _hostClient;
    private int _nextSequence;
    private bool _disposed;
    private bool _paused;
    private bool _quit;
    private float _lastAimX = 1f;
    private float _lastAimY;

    public ShooterConsoleApp(IShooterConsoleInputSource input, ShooterConsoleRenderer renderer, ConsoleLog log)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public bool Start()
    {
        ThrowIfDisposed();
        _worldHost = new ShooterWorldHost();
        _world = _worldHost.CreateBattleWorld(WorldId);
        if (!_world.Services.TryResolve<IShooterBattleRuntimePort>(out _runtime) || _runtime == null)
        {
            _log.Error("Shooter console world did not register its runtime port.");
            return false;
        }

        _hostNetwork = new InProcessHostNetwork(
            UnsupportedHostMessageCodec.Instance,
            new ShooterHostNetworkRequestHandler());
        _hostNetwork.Connections.Attach(_worldHost.HostRuntime);
        _hostNetwork.Connections.OnClientConnected += OnHostClientConnected;
        _hostNetwork.Start();
        _hostClient = _hostNetwork.CreateClientConnection();
        _hostClient.PacketReceived += OnHostResponse;
        _hostClient.Open("inprocess", 1);

        var start = CreateDefaultStartPayload();
        if (!_runtime.StartGame(in start))
        {
            _log.Error("Shooter console failed to start runtime.");
            return false;
        }

        _renderer.RenderHelp();
        _renderer.Render(_runtime.GetSnapshot(), _runtime.ComputeStateHash(), _paused);
        return true;
    }

    public ConsoleHostFrameResult Tick(float deltaSeconds)
    {
        ThrowIfDisposed();
        if (_runtime == null || _worldHost == null || _hostClient == null || _hostNetwork == null)
        {
            return ConsoleHostFrameResult.Quit(1);
        }

        var input = _input.Poll();
        if (input.Help)
        {
            _renderer.RenderHelp();
        }

        if (input.Pause)
        {
            _paused = !_paused;
        }

        if (input.Quit)
        {
            _quit = true;
            return ConsoleHostFrameResult.Quit();
        }

        if (!_paused)
        {
            var command = input.ToCommand(LocalPlayerId, _lastAimX, _lastAimY);
            _lastAimX = command.AimX;
            _lastAimY = command.AimY;
            SendLocalInput(_runtime.CurrentFrame, in command);
            _runtime.SubmitInput(_runtime.CurrentFrame, new[] { CreateBotCommand(_runtime.CurrentFrame) });
            _hostNetwork.Tick();
            _hostClient.Tick(deltaSeconds);
            _worldHost.Tick(deltaSeconds);
        }

        _renderer.Render(_runtime.GetSnapshot(), _runtime.ComputeStateHash(), _paused);
        return _quit ? ConsoleHostFrameResult.Quit() : ConsoleHostFrameResult.Continue;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_hostClient != null)
        {
            _hostClient.PacketReceived -= OnHostResponse;
            _hostClient.Dispose();
        }
        _hostNetwork?.Dispose();
        if (_worldHost != null && _world != null)
        {
            _worldHost.DestroyBattleWorld(_world.Id.Value);
        }
    }

    private void SendLocalInput(int frame, in ShooterPlayerCommand command)
    {
        if (_hostClient == null) return;
        var request = new ShooterHostInputRequest(WorldId, frame, new[] { command });
        var sequence = unchecked((uint)Interlocked.Increment(ref _nextSequence));
        if (sequence == 0)
        {
            sequence = unchecked((uint)Interlocked.Increment(ref _nextSequence));
        }

        _hostClient.Send(
            (uint)ShooterOpCodes.Input.PlayerCommand,
            ShooterHostInputCodec.Serialize(in request),
            (ushort)NetworkPacketFlags.Request,
            sequence);
    }

    private void OnHostClientConnected(IServerConnection connection)
    {
        if (connection is not HostNetworkServerConnection networkConnection || _hostNetwork == null) return;

        _hostNetwork.Connections.TryBindClient(
            networkConnection.Session.Id,
            new ServerClientId("shooter-console-client"));
        var binding = new ShooterHostSessionBinding(WorldId, LocalPlayerId);
        ShooterHostSessionBindings.Bind(networkConnection.Session, in binding);
    }

    private void OnHostResponse(uint opCode, uint sequence, ArraySegment<byte> payload)
    {
        if (opCode != (uint)ShooterOpCodes.Input.PlayerCommand || sequence == 0) return;

        try
        {
            var response = ShooterHostInputCodec.DeserializeResponse(payload);
            if (!response.Accepted)
            {
                _log.Error($"Shooter Host rejected local input. reason={response.ReasonCode} frame={response.ServerFrame}");
            }
        }
        catch (Exception exception)
        {
            _log.Error($"Shooter Host returned an invalid input response: {exception.Message}");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ShooterConsoleApp));
    }

    private static ShooterStartGamePayload CreateDefaultStartPayload()
    {
        return new ShooterStartGamePayload(
            WorldId,
            TargetFrameRate,
            20260615,
            new[]
            {
                new ShooterStartPlayer(1, "P1", -2f, 0f),
                new ShooterStartPlayer(2, "BOT", 2f, 0f)
            });
    }

    private static ShooterPlayerCommand CreateBotCommand(int frame)
    {
        var fire = frame % 45 == 0;
        return new ShooterPlayerCommand(2, 0f, 0f, -1f, 0f, fire);
    }
}
