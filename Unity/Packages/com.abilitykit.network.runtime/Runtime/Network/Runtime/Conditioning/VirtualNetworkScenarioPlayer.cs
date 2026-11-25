#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AbilityKit.Network.Runtime.Conditioning
{
    /// <summary>
    /// 供确定性虚拟网络播放消费的运行时无关命令。
    /// </summary>
    public sealed class VirtualNetworkCommand
    {
        public VirtualNetworkCommand(long atMs, string name,
            IReadOnlyDictionary<string, string>? parameters = null)
        {
            if (atMs < 0) throw new ArgumentOutOfRangeException(nameof(atMs));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Command name is required.", nameof(name));
            AtMs = atMs;
            Name = name;
            Parameters = parameters == null
                ? EmptyParameters
                : new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>(parameters));
        }

        private static readonly IReadOnlyDictionary<string, string> EmptyParameters =
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        public long AtMs { get; }
        public string Name { get; }
        public IReadOnlyDictionary<string, string> Parameters { get; }

        public string RequireParameter(string name)
        {
            foreach (var pair in Parameters)
            {
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(pair.Value))
                    return pair.Value;
            }

            throw new InvalidOperationException(
                $"Missing parameter '{name}' in {Name} atMs={AtMs}.");
        }
    }

    /// <summary>
    /// 在无套接字链路上播放带时间戳的网络命令。同时间戳命令保留编排顺序；执行该时间戳命令前先投递到期数据包。
    /// </summary>
    public sealed class VirtualNetworkScenarioPlayer
    {
        public const string DisconnectCommand = "network.disconnect";
        public const string ReconnectCommand = "network.reconnect";
        public const string PacketCommand = "network.packet";
        public const string PhaseCommand = "network.phase";

        private readonly VirtualNetworkConditionLink _link;
        private readonly Action<VirtualNetworkConditionLink, VirtualNetworkCommand> _packetHandler;

        public VirtualNetworkScenarioPlayer(
            VirtualNetworkConditionLink link,
            Action<VirtualNetworkConditionLink, VirtualNetworkCommand> packetHandler)
        {
            _link = link ?? throw new ArgumentNullException(nameof(link));
            _packetHandler = packetHandler ?? throw new ArgumentNullException(nameof(packetHandler));
        }

        public VirtualNetworkConditionLink Link => _link;

        public void Play(IEnumerable<VirtualNetworkCommand> commands, long? finishAtMs = null)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));

            var ordered = new List<IndexedCommand>();
            var index = 0;
            foreach (var command in commands)
            {
                if (command == null) throw new ArgumentException("Commands cannot contain null.", nameof(commands));
                ordered.Add(new IndexedCommand(index++, command));
            }

            ordered.Sort(IndexedCommand.Compare);
            foreach (var item in ordered)
            {
                _link.AdvanceTo(item.Command.AtMs);
                Execute(item.Command);
            }

            if (finishAtMs.HasValue) _link.AdvanceTo(finishAtMs.Value);
        }

        public void Execute(VirtualNetworkCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (command.AtMs != _link.NowMs)
                throw new InvalidOperationException(
                    $"Command time {command.AtMs} does not match virtual clock {_link.NowMs}.");

            switch (command.Name)
            {
                case DisconnectCommand:
                    _link.Disconnect();
                    break;
                case ReconnectCommand:
                    _link.Reconnect();
                    break;
                case PacketCommand:
                    _packetHandler(_link, command);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unknown virtual-network command '{command.Name}' atMs={command.AtMs}.");
            }
        }

        private readonly struct IndexedCommand
        {
            public IndexedCommand(int index, VirtualNetworkCommand command)
            {
                Index = index;
                Command = command;
            }

            public int Index { get; }
            public VirtualNetworkCommand Command { get; }

            public static int Compare(IndexedCommand left, IndexedCommand right)
            {
                var time = left.Command.AtMs.CompareTo(right.Command.AtMs);
                return time != 0 ? time : left.Index.CompareTo(right.Index);
            }
        }
    }
}
