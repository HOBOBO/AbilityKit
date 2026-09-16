using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.HFSM.Definition;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AbilityKit.Game.Battle.Component
{
    public sealed class CharacterPresentationAction
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("type")] public string Type;
        [JsonProperty("actions")] public List<CharacterPresentationAction> Actions;
        [JsonProperty("message")] public string Message;
        [JsonProperty("seconds")] public decimal Seconds;
        [JsonProperty("animatorState")] public string AnimatorState;
        [JsonProperty("frameCount")] public int FrameCount;
        [JsonProperty("loop")] public bool Loop;
        [JsonProperty("layer")] public int Layer;
    }

    public readonly struct CharacterPlaybackIntent
    {
        public readonly string Path;
        public readonly long InstanceId;
        public readonly int Frame;
        public readonly int LocalFrame;
        public readonly string ActionId;
        public readonly string AnimatorState;
        public readonly int FrameCount;
        public readonly bool Loop;
        public readonly int Layer;
        public readonly int BindingVersion;

        public CharacterPlaybackIntent(string path, long instanceId, int frame, int localFrame,
            string actionId, string animatorState, int frameCount, bool loop, int layer, int bindingVersion)
        {
            Path = path;
            InstanceId = instanceId;
            Frame = frame;
            LocalFrame = localFrame;
            ActionId = actionId;
            AnimatorState = animatorState;
            FrameCount = frameCount;
            Loop = loop;
            Layer = layer;
            BindingVersion = bindingVersion;
        }
    }

    public readonly struct CharacterLogIntent
    {
        public readonly string Path;
        public readonly long InstanceId;
        public readonly int Frame;
        public readonly string ActionId;
        public readonly string Message;

        public CharacterLogIntent(string path, long instanceId, int frame, string actionId, string message)
        {
            Path = path;
            InstanceId = instanceId;
            Frame = frame;
            ActionId = actionId;
            Message = message;
        }
    }

    public interface ICharacterPresentationSink
    {
        void OnPlay(in CharacterPlaybackIntent intent);
        void OnLog(in CharacterLogIntent intent);
    }

    public sealed class CharacterPresentationActionCatalog
    {
        public const string ResourcePath = "moba/character_view_actions";
        private readonly Dictionary<string, CharacterPresentationAction> _states =
            new Dictionary<string, CharacterPresentationAction>(StringComparer.Ordinal);
        private readonly List<VariantDto> _variants = new List<VariantDto>();

        public int Version { get; private set; } = 1;

        public static CharacterPresentationActionCatalog Load(ITextAssetLoader loader)
        {
            if (loader != null && loader.TryLoadText(ResourcePath, out var json) &&
                !string.IsNullOrWhiteSpace(json)) return LoadJson(json);
            return CreateDefault();
        }

        public static CharacterPresentationActionCatalog LoadJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Action catalog is empty.", nameof(json));
            var token = JToken.Parse(json, new JsonLoadSettings
            {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
            });
            var dto = token.ToObject<CatalogDto>(JsonSerializer.Create(new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Error
            }));
            if (dto == null || dto.SchemaVersion != 1 || dto.States == null)
                throw new InvalidOperationException("Unsupported character presentation catalog.");
            var catalog = new CharacterPresentationActionCatalog();
            foreach (var entry in dto.States)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Path) || entry.Action == null)
                    throw new InvalidOperationException("Character presentation state requires a path and action.");
                ValidateAction(entry.Action, new HashSet<string>(StringComparer.Ordinal));
                if (!catalog._states.TryAdd(entry.Path, Clone(entry.Action)))
                    throw new InvalidOperationException("Duplicate character presentation state path: " + entry.Path);
            }
            if (dto.Variants != null)
            {
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var variant in dto.Variants)
                {
                    if (variant == null || variant.EntityCode <= 0 ||
                        string.IsNullOrEmpty(variant.Path) || string.IsNullOrEmpty(variant.ActionId) ||
                        variant.Action == null ||
                        !keys.Add(variant.EntityCode + "/" + variant.Path + "/" + variant.ActionId))
                        throw new InvalidOperationException("Character presentation variant is invalid or duplicated.");
                    if (!catalog._states.ContainsKey(variant.Path))
                        throw new InvalidOperationException("Character presentation variant has unknown state path.");
                    ValidateAction(variant.Action, new HashSet<string>(StringComparer.Ordinal));
                    catalog._variants.Add(variant);
                }
                foreach (var variant in catalog._variants)
                    catalog.CreateForEntityCode(variant.EntityCode);
            }
            return catalog;
        }

        public CharacterPresentationActionCatalog CreateForEntityCode(int entityCode)
        {
            if (entityCode <= 0) return this;
            var result = new CharacterPresentationActionCatalog();
            foreach (var entry in _states)
                result._states.Add(entry.Key, Clone(entry.Value));
            result.Version = Version;
            foreach (var variant in _variants)
                if (variant.EntityCode == entityCode)
                    result.ReplaceAction(variant.Path, variant.ActionId, variant.Action);
            return result;
        }

        public void ValidateAgainst(StateMachineDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var paths = new HashSet<string>(StringComparer.Ordinal);
            CollectPaths(definition, definition.RootMachineId, string.Empty, paths);
            foreach (var path in _states.Keys)
                if (!paths.Contains(path))
                    throw new InvalidOperationException("Character presentation state path is unknown: " + path);
        }

        private static void CollectPaths(StateMachineDefinition definition, string machineId,
            string prefix, HashSet<string> paths)
        {
            var machine = definition.Machines.Find(m => m.Id == machineId);
            if (machine == null) throw new InvalidOperationException("Character HFSM action references unknown machine.");
            foreach (var state in machine.States)
            {
                var path = prefix + machineId + "/" + state.Id;
                if (string.IsNullOrEmpty(state.ChildMachineId)) paths.Add(path);
                else CollectPaths(definition, state.ChildMachineId, path + "/", paths);
            }
        }

        public static CharacterPresentationActionCatalog CreateDefault()
        {
            var catalog = new CharacterPresentationActionCatalog();
            catalog._states.Add("life/alive/action/idle", Sequence("idle", Play("idle.play", "Idle", true)));
            catalog._states.Add("life/alive/action/moving", Sequence("moving", Play("moving.play", "Run", true)));
            catalog._states.Add("life/alive/action/casting", Sequence("casting",
                new CharacterPresentationAction { Id = "casting.log", Type = "log", Message = "casting" },
                new CharacterPresentationAction { Id = "attack.phase", Type = "wait", Seconds = 0.5m }));
            catalog._states.Add("life/alive/action/controlled", Sequence("controlled", Play("controlled.play", "Stun", true)));
            catalog._states.Add("life/dead", Sequence("dead", Play("dead.play", "Die", false)));
            return catalog;
        }

        internal bool TryGet(string path, out CharacterPresentationAction action) =>
            _states.TryGetValue(path ?? string.Empty, out action);

        public void ReplaceAction(string path, string actionId, CharacterPresentationAction replacement)
        {
            if (!_states.TryGetValue(path ?? string.Empty, out var root) || string.IsNullOrWhiteSpace(actionId))
                throw new InvalidOperationException("Character presentation action slot is unknown.");
            var next = Clone(root);
            if (!Replace(next, actionId, replacement))
                throw new InvalidOperationException("Character presentation action slot is unknown: " + actionId);
            ValidateAction(next, new HashSet<string>(StringComparer.Ordinal));
            _states[path] = next;
            Version = checked(Version + 1);
        }

        private static bool Replace(CharacterPresentationAction root, string id, CharacterPresentationAction replacement)
        {
            if (root.Actions == null) return false;
            for (var i = 0; i < root.Actions.Count; i++)
            {
                if (root.Actions[i].Id == id)
                {
                    if (replacement == null) throw new ArgumentNullException(nameof(replacement));
                    var next = Clone(replacement);
                    next.Id = id;
                    root.Actions[i] = next;
                    return true;
                }
                if (Replace(root.Actions[i], id, replacement)) return true;
            }
            return false;
        }

        private static CharacterPresentationAction Clone(CharacterPresentationAction source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = new CharacterPresentationAction
            {
                Id = source.Id, Type = source.Type, Message = source.Message,
                Seconds = source.Seconds, AnimatorState = source.AnimatorState,
                FrameCount = source.FrameCount, Loop = source.Loop, Layer = source.Layer
            };
            if (source.Actions != null)
            {
                copy.Actions = new List<CharacterPresentationAction>(source.Actions.Count);
                foreach (var child in source.Actions) copy.Actions.Add(Clone(child));
            }
            return copy;
        }

        private static void ValidateAction(CharacterPresentationAction action, HashSet<string> ids)
        {
            if (action == null || string.IsNullOrWhiteSpace(action.Id) || !ids.Add(action.Id))
                throw new InvalidOperationException("Character presentation action IDs must be unique and non-empty.");
            switch (action.Type)
            {
                case "sequence":
                    if (action.Actions == null || action.Actions.Count == 0)
                        throw new InvalidOperationException("Sequence action requires children.");
                    foreach (var child in action.Actions) ValidateAction(child, ids);
                    break;
                case "wait":
                    if (action.Seconds < 0 || action.Seconds > 3600m || action.Actions != null)
                        throw new InvalidOperationException("Wait action duration must be non-negative.");
                    break;
                case "play":
                    if (action.FrameCount < 0 || action.Layer < 0 || action.Actions != null)
                        throw new InvalidOperationException("Play action frame count and layer must be non-negative.");
                    break;
                case "log":
                    if (action.Message == null || action.Actions != null)
                        throw new InvalidOperationException("Log action requires a message.");
                    break;
                default:
                    throw new InvalidOperationException("Unsupported character presentation action: " + action.Type);
            }
        }

        private static CharacterPresentationAction Sequence(string id, params CharacterPresentationAction[] children) =>
            new CharacterPresentationAction { Id = id, Type = "sequence", Actions = new List<CharacterPresentationAction>(children) };

        private static CharacterPresentationAction Play(string id, string state, bool loop) =>
            new CharacterPresentationAction { Id = id, Type = "play", AnimatorState = state, Loop = loop };

        private sealed class CatalogDto
        {
            [JsonProperty("schemaVersion")] public int SchemaVersion;
            [JsonProperty("states")] public List<StateDto> States;
            [JsonProperty("variants")] public List<VariantDto> Variants;
        }

        private sealed class StateDto
        {
            [JsonProperty("path")] public string Path;
            [JsonProperty("action")] public CharacterPresentationAction Action;
        }

        private sealed class VariantDto
        {
            [JsonProperty("entityCode")] public int EntityCode;
            [JsonProperty("path")] public string Path;
            [JsonProperty("actionId")] public string ActionId;
            [JsonProperty("action")] public CharacterPresentationAction Action;
        }
    }

    public sealed class CharacterPresentationActionRunner
    {
        private readonly CharacterPresentationActionCatalog _catalog;
        private readonly HashSet<string> _logged = new HashSet<string>(StringComparer.Ordinal);
        private long _instanceId;
        private int _lastPlayFrame = int.MinValue;
        private int _lastPlayVersion;
        private string _lastPlayActionId;

        public CharacterPresentationActionRunner(CharacterPresentationActionCatalog catalog) =>
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

        public CharacterPlaybackIntent? Evaluate(string path, long instanceId, int localFrame,
            int frame, int tickRate, ICharacterPresentationSink sink = null)
        {
            if (localFrame < 0 || tickRate <= 0) throw new ArgumentOutOfRangeException(nameof(localFrame));
            if (_instanceId != instanceId)
            {
                _instanceId = instanceId;
                _logged.Clear();
                _lastPlayFrame = int.MinValue;
            }
            if (!_catalog.TryGet(path, out var root)) return null;
            CharacterPlaybackIntent? playback = null;
            var cursor = 0;
            Execute(root, path, instanceId, localFrame, frame, tickRate, sink, ref cursor, ref playback);
            if (playback.HasValue && sink != null &&
                (frame != _lastPlayFrame || _lastPlayVersion != _catalog.Version ||
                 !string.Equals(_lastPlayActionId, playback.Value.ActionId, StringComparison.Ordinal)))
            {
                var intent = playback.Value;
                sink.OnPlay(in intent);
                _lastPlayFrame = frame;
                _lastPlayVersion = _catalog.Version;
                _lastPlayActionId = intent.ActionId;
            }
            return playback;
        }

        private void Execute(CharacterPresentationAction action, string path, long instanceId,
            int localFrame, int frame, int tickRate, ICharacterPresentationSink sink,
            ref int cursor, ref CharacterPlaybackIntent? playback)
        {
            if (action.Type == "sequence")
            {
                foreach (var child in action.Actions)
                    Execute(child, path, instanceId, localFrame, frame, tickRate, sink, ref cursor, ref playback);
                return;
            }
            if (action.Type == "wait")
            {
                cursor = checked(cursor + (int)decimal.Ceiling(action.Seconds * tickRate));
                return;
            }
            if (action.Type == "log")
            {
                if (cursor == localFrame && sink != null && !_logged.Contains(action.Id))
                {
                    var intent = new CharacterLogIntent(path, instanceId, frame, action.Id, action.Message);
                    sink.OnLog(in intent);
                    _logged.Add(action.Id);
                }
                return;
            }
            if (cursor <= localFrame)
                playback = new CharacterPlaybackIntent(path, instanceId, frame, localFrame - cursor,
                    action.Id, action.AnimatorState, action.FrameCount, action.Loop,
                    action.Layer, _catalog.Version);
        }
    }
}
