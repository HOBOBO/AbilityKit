#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AbilityKit.HFSM.Runtime
{
    [Serializable]
    public sealed class CompositeActionNode
    {
        [JsonProperty("id")] public string Id = string.Empty;
        [JsonProperty("type")] public string Type = string.Empty;
        [JsonProperty("actions")] public List<CompositeActionNode>? Actions;
        [JsonProperty("seconds")] public decimal Seconds;
        [JsonProperty("message")] public string Message = string.Empty;
        [JsonProperty("playState")] public string PlayState = string.Empty;
        [JsonProperty("frameCount")] public int FrameCount;
        [JsonProperty("loop")] public bool Loop;
    }

    [Serializable]
    public sealed class CompositeActionBinding
    {
        [JsonProperty("behaviorKey")] public string BehaviorKey = string.Empty;
        [JsonProperty("action")] public CompositeActionNode Action = new CompositeActionNode();
    }

    public sealed class CompositeActionCatalog
    {
        [JsonProperty("schemaVersion")] public int SchemaVersion = 1;
        [JsonProperty("states")] public List<CompositeActionBinding> States = new List<CompositeActionBinding>();

        public static CompositeActionCatalog LoadJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Composite action JSON is empty.", nameof(json));
            var token = JToken.Parse(json, new JsonLoadSettings
            {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
            });
            var catalog = token.ToObject<CompositeActionCatalog>(JsonSerializer.Create(new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Error
            })) ?? throw new InvalidOperationException("Composite action catalog is empty.");
            catalog.Validate();
            return catalog;
        }

        public string SaveJson()
        {
            Validate();
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public CompositeActionNode Get(string key)
        {
            foreach (var state in States)
                if (string.Equals(state.BehaviorKey, key, StringComparison.Ordinal)) return state.Action;
            throw new InvalidOperationException("Unknown composite action binding: " + key);
        }

        public void Validate()
        {
            if (SchemaVersion != 1 || States == null || States.Count == 0)
                throw new InvalidOperationException("Unsupported composite action catalog.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var state in States)
            {
                if (state == null || string.IsNullOrWhiteSpace(state.BehaviorKey) || !keys.Add(state.BehaviorKey))
                    throw new InvalidOperationException("Composite action binding keys must be unique.");
                ValidateNode(state.Action, new HashSet<string>(StringComparer.Ordinal), 0);
            }
        }

        private static void ValidateNode(CompositeActionNode node, HashSet<string> ids, int depth)
        {
            if (node == null || depth > 16 || string.IsNullOrWhiteSpace(node.Id) || !ids.Add(node.Id))
                throw new InvalidOperationException("Composite action IDs must be unique and nesting bounded.");
            switch (node.Type)
            {
                case "sequence":
                case "parallel":
                    if (node.Actions == null || node.Actions.Count == 0)
                        throw new InvalidOperationException("Composite action requires children.");
                    foreach (var child in node.Actions) ValidateNode(child, ids, depth + 1);
                    break;
                case "wait":
                    if (node.Seconds < 0 || node.Seconds > 3600m || node.Actions != null)
                        throw new InvalidOperationException("Wait seconds must be between 0 and 3600.");
                    break;
                case "log":
                    if (node.Actions != null || node.Message == null)
                        throw new InvalidOperationException("Log action requires a message.");
                    break;
                case "play":
                    if (node.Actions != null || string.IsNullOrWhiteSpace(node.PlayState) || node.FrameCount < 0)
                        throw new InvalidOperationException("Play action requires a state and non-negative frame count.");
                    break;
                default:
                    throw new InvalidOperationException("Unsupported composite action: " + node.Type);
            }
        }
    }

    public readonly struct CompositeActionIntent
    {
        public readonly string Type;
        public readonly string ActionId;
        public readonly int Frame;
        public readonly int LocalFrame;
        public readonly string Message;
        public readonly string PlayState;
        public readonly int FrameCount;
        public readonly bool Loop;

        public CompositeActionIntent(string type, string actionId, int frame, int localFrame,
            string message, string playState, int frameCount, bool loop)
        {
            Type = type; ActionId = actionId; Frame = frame; LocalFrame = localFrame;
            Message = message; PlayState = playState; FrameCount = frameCount; Loop = loop;
        }
    }

    public interface ICompositeActionSink
    {
        void OnCompositeAction(in CompositeActionIntent intent);
    }

    public sealed class CompositeStateAction<TOwner> : RuntimeStateBase<TOwner>, IStateSnapshotParticipant
        where TOwner : ICompositeActionSink
    {
        private readonly CompositeActionNode _root;
        private readonly int _tickRate;
        private readonly string _hash;
        private readonly HashSet<string> _logged = new HashSet<string>(StringComparer.Ordinal);
        private int _startFrame = -1;
        private int _lastFrame = -1;
        private bool _active;

        public CompositeStateAction(CompositeActionNode root, int tickRate)
        {
            if (tickRate <= 0) throw new ArgumentOutOfRangeException(nameof(tickRate));
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _tickRate = tickRate;
            var json = tickRate + ":" + JsonConvert.SerializeObject(root);
            using (var sha = SHA256.Create())
                _hash = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(json)));
        }

        public bool IsActive => _active;
        public int LocalFrame => _active ? _lastFrame - _startFrame : -1;
        public int SnapshotVersion => 1;

        public override void OnEnter(TOwner owner, in TickContext context)
        {
            _active = true;
            _startFrame = context.Frame;
            _lastFrame = context.Frame;
            _logged.Clear();
            Evaluate(owner, context.Frame, true);
        }

        public override void OnTick(TOwner owner, in TickContext context)
        {
            if (!_active || context.Frame < _lastFrame) return;
            _lastFrame = context.Frame;
            Evaluate(owner, context.Frame, true);
        }

        public override void OnExit(TOwner owner, in TickContext context) => _active = false;

        public void Seek(TOwner owner, int frame)
        {
            if (_active && frame >= _startFrame) Evaluate(owner, frame, false);
        }

        private void Evaluate(TOwner owner, int frame, bool emitLog)
        {
            var cursor = 0;
            CompositeActionIntent? playback = null;
            Execute(_root, owner, frame, frame - _startFrame, emitLog, ref cursor, ref playback);
            if (playback.HasValue)
            {
                var intent = playback.Value;
                owner.OnCompositeAction(in intent);
            }
        }

        private void Execute(CompositeActionNode node, TOwner owner, int frame, int localFrame,
            bool emitLog, ref int cursor, ref CompositeActionIntent? playback)
        {
            if (node.Type == "sequence")
            {
                foreach (var child in node.Actions!)
                    Execute(child, owner, frame, localFrame, emitLog, ref cursor, ref playback);
                return;
            }
            if (node.Type == "parallel")
            {
                foreach (var child in node.Actions!)
                {
                    var branch = cursor;
                    Execute(child, owner, frame, localFrame, emitLog, ref branch, ref playback);
                }
                return;
            }
            if (node.Type == "wait")
            {
                cursor = checked(cursor + (int)decimal.Ceiling(node.Seconds * _tickRate));
                return;
            }
            if (node.Type == "log")
            {
                if (emitLog && localFrame >= cursor && _logged.Add(node.Id))
                {
                    var intent = new CompositeActionIntent("log", node.Id, frame, localFrame,
                        node.Message, string.Empty, 0, false);
                    owner.OnCompositeAction(in intent);
                }
                return;
            }
            if (localFrame >= cursor)
                playback = new CompositeActionIntent("play", node.Id, frame, localFrame - cursor,
                    string.Empty, node.PlayState, node.FrameCount, node.Loop);
        }

        public string CaptureSnapshot() => JsonConvert.SerializeObject(new Snapshot
        {
            Hash = _hash, StartFrame = _startFrame, LastFrame = _lastFrame,
            Active = _active, Logged = new List<string>(_logged)
        });

        public void ValidateSnapshot(int version, string payload)
        {
            var state = Parse(version, payload);
            if (state.Hash != _hash || state.StartFrame < -1 || state.LastFrame < -1 ||
                (state.Active && (state.StartFrame < 0 || state.LastFrame < state.StartFrame)) ||
                state.Logged == null || new HashSet<string>(state.Logged, StringComparer.Ordinal).Count != state.Logged.Count)
                throw new InvalidOperationException("Composite action snapshot or binding differs.");
            var logIds = new HashSet<string>(StringComparer.Ordinal);
            CollectLogIds(_root, logIds);
            foreach (var id in state.Logged)
                if (!logIds.Contains(id))
                    throw new InvalidOperationException("Composite action snapshot contains an unknown log ID.");
        }

        private static void CollectLogIds(CompositeActionNode node, HashSet<string> ids)
        {
            if (node.Type == "log") ids.Add(node.Id);
            if (node.Actions == null) return;
            foreach (var child in node.Actions) CollectLogIds(child, ids);
        }

        public void RestoreSnapshot(int version, string payload)
        {
            ValidateSnapshot(version, payload);
            var state = Parse(version, payload);
            _startFrame = state.StartFrame;
            _lastFrame = state.LastFrame;
            _active = state.Active;
            _logged.Clear();
            foreach (var id in state.Logged!) _logged.Add(id);
        }

        private static Snapshot Parse(int version, string payload)
        {
            if (version != 1 || string.IsNullOrWhiteSpace(payload))
                throw new InvalidOperationException("Unsupported composite action snapshot.");
            return JsonConvert.DeserializeObject<Snapshot>(payload)
                ?? throw new InvalidOperationException("Invalid composite action snapshot.");
        }

        private sealed class Snapshot
        {
            public string Hash = string.Empty;
            public int StartFrame;
            public int LastFrame;
            public bool Active;
            public List<string>? Logged;
        }
    }
}
