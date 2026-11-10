#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.HFSM.Definition;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    public static class MobaStateMachinePublisher
    {
        public const string SourceFolder =
            "Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaStateMachineAuthoring";
        private const string UnityFolder =
            "Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba";
        private const string ConsoleFolder = "../src/AbilityKit.Demo.Moba.Console/Configs/moba";
        private const string ConsoleLubanFolder = "../src/AbilityKit.Demo.Moba.Console/Configs/luban/moba";
        private const string ActorStateMachinesName = "actor_state_machines";

        public static string[] CharacterProfileNames()
        {
            var sourceFolder = ProjectPath(SourceFolder);
            return Directory.GetFiles(sourceFolder, "character_hfsm*.json")
                .Select(path => Path.GetFileNameWithoutExtension(path))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] SourceNames() => CharacterProfileNames()
            .Concat(new[] { ActorStateMachinesName, "character_bindings" })
            .ToArray();

        public static string SourcePath(string name) => ProjectPath(SourceFolder + "/" + name + ".json");

        private static string ProjectPath(string relative) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));

        [MenuItem("Tools/AbilityKit/MOBA/Publish State Machines")]
        public static void Publish()
        {
            var names = SourceNames();
            var documents = names.Select(name => File.ReadAllText(SourcePath(name))).ToArray();
            for (var i = 0; i < names.Length; i++)
                Validate(names[i], documents[i]);
            for (var i = 0; i < names.Length; i++)
            {
                var name = names[i];
                foreach (var folder in new[] { UnityFolder, ConsoleFolder, ConsoleLubanFolder })
                    WriteIfChanged(ProjectPath(folder + "/" + name + ".json"), documents[i]);
            }
            foreach (var folder in new[] { UnityFolder, ConsoleFolder, ConsoleLubanFolder })
            {
                var retired = ProjectPath(folder + "/character_hfsm.json");
                if (File.Exists(retired)) File.Delete(retired);
                if (File.Exists(retired + ".meta")) File.Delete(retired + ".meta");
            }
            AssetDatabase.Refresh();
            Debug.Log("MOBA 状态机已发布到 Unity 和 Console。");
        }

        [MenuItem("Tools/AbilityKit/MOBA/Check State Machines")]
        public static void Check()
        {
            foreach (var name in SourceNames())
            {
                var json = File.ReadAllText(SourcePath(name));
                Validate(name, json);
                var expected = JToken.Parse(json);
                foreach (var path in new[] { UnityFolder, ConsoleFolder, ConsoleLubanFolder })
                {
                    var target = ProjectPath(path + "/" + name + ".json");
                    if (!File.Exists(target) || !JToken.DeepEquals(expected, JToken.Parse(File.ReadAllText(target))))
                        throw new InvalidOperationException($"状态机发布产物与源文件不一致：{target}");
                }
            }
            Debug.Log("MOBA 状态机源文件与两端发布产物一致。");
        }

        public static void Validate(string name, string json, string bindingsJson = null)
        {
            if (name == "character_bindings")
            {
                var declarations = JsonConvert.DeserializeObject<MobaCharacterBindings[]>(json) ?? Array.Empty<MobaCharacterBindings>();
                var profiles = CharacterProfileNames();
                var ids = new HashSet<string>(profiles.Select(profile => DefinitionJson.Load(File.ReadAllText(SourcePath(profile))).DefinitionId), StringComparer.Ordinal);
                if (declarations.Any(item => item == null || !ids.Contains(item.DefinitionId)) ||
                    declarations.Length != ids.Count ||
                    declarations.Select(item => item.DefinitionId).Distinct(StringComparer.Ordinal).Count() != ids.Count)
                    throw new InvalidOperationException("Every character definition must have exactly one binding declaration.");
                foreach (var profile in profiles) Validate(profile, File.ReadAllText(SourcePath(profile)), json);
                return;
            }
            if (name.StartsWith("character_hfsm", StringComparison.Ordinal))
            {
                var definition = MobaCharacterBindings.Load(DefinitionJson.Load(json), new AuthoringTextLoader(bindingsJson));
                var expectedId = ExpectedDefinitionId(name);
                if (!string.Equals(definition.DefinitionId, expectedId, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Character HFSM '{name}' must use definitionId '{expectedId}'.");
                if (name == "character_hfsm_default" && definition.DefinitionId != MobaCharacterHfsmProfile.Id)
                    throw new InvalidOperationException("角色 HFSM definitionId 与运行时不一致。");
                foreach (var machine in definition.Machines)
                    foreach (var state in machine.States)
                    {
                        ValidateStateActions(state.Id, state.LogicEntryActionKeys);
                        ValidateStateActions(state.Id, state.LogicExitActionKeys);
                    }
                foreach (var machine in definition.Machines)
                    foreach (var transition in machine.Transitions)
                        if (!string.IsNullOrEmpty(transition.ConditionKey) &&
                            !MobaCharacterHfsmProfile.IsSupportedConditionKey(definition, transition.ConditionKey))
                            throw new InvalidOperationException($"角色 HFSM 未注册条件：{transition.ConditionKey}");
            }
            else if (name == "actor_state_machines")
            {
                var catalog = new MobaActorStateMachineProfileCatalog();
                try
                {
                    if (MobaActorStateMachineProfileJsonLoader.LoadJson(json, catalog) == 0)
                        throw new InvalidOperationException("投射物状态机列表为空。");
                }
                finally { catalog.Dispose(); }
            }
            else throw new ArgumentException("Unknown state machine source", nameof(name));
        }

        private sealed class AuthoringTextLoader : AbilityKit.Ability.Config.ITextAssetLoader
        {
            private readonly string _bindingsJson;
            public AuthoringTextLoader(string bindingsJson = null) => _bindingsJson = bindingsJson;
            public bool TryLoadBytes(string path, out byte[] bytes) { bytes = null; return false; }
            public bool TryLoadText(string path, out string text)
            {
                if (path == MobaCharacterBindings.ResourcePath && _bindingsJson != null)
                { text = _bindingsJson; return true; }
                var file = SourcePath(Path.GetFileName(path));
                text = File.Exists(file) ? File.ReadAllText(file) : null;
                return text != null;
            }
        }

        private static string ExpectedDefinitionId(string name)
        {
            if (name == "character_hfsm_default") return MobaCharacterHfsmProfile.Id;
            const string prefix = "character_hfsm_";
            if (name.StartsWith(prefix, StringComparison.Ordinal) &&
                int.TryParse(name.Substring(prefix.Length), out var heroId) && heroId > 0)
                return MobaCharacterHfsmProfile.HeroDefinitionId(heroId);
            throw new InvalidOperationException($"Unsupported character HFSM source name '{name}'.");
        }

        private static void ValidateStateActions(string stateId, List<string> keys)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in keys ?? new List<string>())
            {
                if (!MobaCharacterHfsmProfile.IsSupportedStateActionKey(key))
                    throw new InvalidOperationException($"HFSM state '{stateId}' has unsupported logic action '{key}'.");
                if (!seen.Add(key))
                    throw new InvalidOperationException($"HFSM state '{stateId}' repeats logic action '{key}'.");
            }
        }

        private static void WriteIfChanged(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (!File.Exists(path) || File.ReadAllText(path) != text)
                File.WriteAllText(path, text);
        }
    }

    public sealed class MobaStateMachineAuthoringWindow : EditorWindow
    {
        private string[] _characterNames = Array.Empty<string>();
        private string _activeCharacterName = "character_hfsm_default";
        private int _kind;
        private int _profile;
        private int _newHeroId;
        private int _selectedState = -1;
        private int _selectedTransition = -1;
        private readonly List<JObject> _nested = new List<JObject>();
        private JObject _character;
        private JArray _actors;
        private JArray _characterBindings;
        private string _bindingJson;
        private bool _showBindings;
        private Vector2 _scroll;
        private bool _dirty;

        [MenuItem("Window/AbilityKit/MOBA 状态机编辑器")]
        public static void Open()
        {
            var window = GetWindow<MobaStateMachineAuthoringWindow>();
            window.titleContent = new GUIContent("MOBA 状态机");
            window.minSize = new Vector2(780, 550);
            window.Show();
        }

        private void OnEnable() => Reload();

        private void Reload()
        {
            _characterNames = MobaStateMachinePublisher.CharacterProfileNames();
            if (!_characterNames.Contains(_activeCharacterName, StringComparer.Ordinal))
                _activeCharacterName = _characterNames.FirstOrDefault() ?? "character_hfsm_default";
            _character = JObject.Parse(File.ReadAllText(MobaStateMachinePublisher.SourcePath(_activeCharacterName)));
            _actors = JArray.Parse(File.ReadAllText(MobaStateMachinePublisher.SourcePath("actor_state_machines")));
            _characterBindings = JArray.Parse(File.ReadAllText(MobaStateMachinePublisher.SourcePath("character_bindings")));
            LoadSelectedBinding();
            _dirty = false;
            _selectedState = -1;
            _selectedTransition = -1;
            _nested.Clear();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var next = GUILayout.Toolbar(_kind, new[] { "角色 HFSM", "投射物状态机" }, EditorStyles.toolbarButton,
                    GUILayout.Width(220));
                if (next != _kind)
                {
                    _kind = next;
                    _profile = 0;
                    _selectedState = _selectedTransition = -1;
                    _nested.Clear();
                }
                if (_kind == 0 && _characterNames.Length > 0)
                {
                    var activeIndex = Array.IndexOf(_characterNames, _activeCharacterName);
                    var selectedCharacter = EditorGUILayout.Popup(activeIndex,
                        _characterNames, EditorStyles.toolbarPopup, GUILayout.Width(210));
                    if (selectedCharacter != activeIndex)
                    {
                        Save();
                        _activeCharacterName = _characterNames[selectedCharacter];
                        _character = JObject.Parse(File.ReadAllText(
                            MobaStateMachinePublisher.SourcePath(_activeCharacterName)));
                        LoadSelectedBinding();
                        _profile = 0;
                        _selectedState = _selectedTransition = -1;
                        _nested.Clear();
                    }
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("重新加载", EditorStyles.toolbarButton)) Reload();
                if (GUILayout.Button("保存源文件", EditorStyles.toolbarButton)) Save();
                if (GUILayout.Button("发布", EditorStyles.toolbarButton))
                {
                    Save();
                    MobaStateMachinePublisher.Publish();
                }
                if (GUILayout.Button("校验", EditorStyles.toolbarButton))
                {
                    Save();
                    MobaStateMachinePublisher.Check();
                }
            }

            if (_character == null || _actors == null) return;
            EditorGUILayout.Space(6);
            if (_kind == 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _newHeroId = EditorGUILayout.IntField("新英雄 ID", _newHeroId, GUILayout.Width(260));
                    if (GUILayout.Button("创建角色 HFSM", GUILayout.Width(130))) CreateCharacterProfile();
                }
                StringField(_character, "definitionId", "Definition ID");
                DrawParameters();
                _showBindings = EditorGUILayout.Foldout(_showBindings, "条件、Cast 与瞄准作用域声明", true);
                if (_showBindings)
                {
                    EditorGUILayout.HelpBox("声明条件组合、完整状态路径及 Cast 快照。保存与发布会同时校验状态图和声明。", MessageType.Info);
                    EditorGUI.BeginChangeCheck();
                    _bindingJson = EditorGUILayout.TextArea(_bindingJson, GUILayout.Height(190));
                    if (EditorGUI.EndChangeCheck()) _dirty = true;
                }
            }
            var containers = _kind == 0 ? (JArray)_character["machines"] : _actors;
            if (GUILayout.Button(_kind == 0 ? "新增角色状态机" : "新增投射物配置"))
            {
                var id = (_kind == 0 ? "new_machine_" : "new_profile_") + containers.Count;
                containers.Add(_kind == 0
                    ? new JObject { ["id"] = id, ["initialStateId"] = "",
                        ["states"] = new JArray(), ["transitions"] = new JArray() }
                    : new JObject { ["id"] = id, ["startState"] = "",
                        ["states"] = new JArray(), ["transitions"] = new JArray() });
                _profile = containers.Count - 1;
                _nested.Clear();
                _selectedState = _selectedTransition = -1;
                _dirty = true;
            }
            if (containers.Count == 0) return;
            _profile = Mathf.Clamp(_profile, 0, containers.Count - 1);
            var names = containers.Select(item => (string)item["id"]).ToArray();
            var selected = EditorGUILayout.Popup(_kind == 0 ? "状态机" : "配置", _profile, names);
            if (selected != _profile)
            {
                _profile = selected;
                _nested.Clear();
                _selectedState = _selectedTransition = -1;
            }
            var container = _kind == 1 && _nested.Count > 0
                ? _nested[_nested.Count - 1]
                : (JObject)containers[_profile];
            if (_kind == 1 && _nested.Count > 0 && GUILayout.Button("返回上级状态机"))
            {
                _nested.RemoveAt(_nested.Count - 1);
                _selectedState = _selectedTransition = -1;
                container = _nested.Count > 0 ? _nested[_nested.Count - 1] : (JObject)containers[_profile];
            }
            if (_kind == 1 && _nested.Count == 0)
                StringField(container, "id", "配置 ID");
            StringField(container, _kind == 0 ? "initialStateId" : "startState", "初始状态");

            var states = (JArray)container["states"];
            var transitions = (JArray)container["transitions"];
            DrawGraph(states, transitions);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawStateList(states);
                DrawTransitionList(transitions);
            }
            EditorGUILayout.EndScrollView();
            if (_dirty) EditorGUILayout.HelpBox("源文件有未保存的修改。", MessageType.Info);
        }

        private void CreateCharacterProfile()
        {
            if (_newHeroId <= 0)
            {
                EditorUtility.DisplayDialog("角色 HFSM", "请输入有效的英雄 ID。", "确定");
                return;
            }

            var name = "character_hfsm_" + _newHeroId;
            var path = MobaStateMachinePublisher.SourcePath(name);
            if (!File.Exists(path))
            {
                var definition = MobaCharacterHfsmProfile.CreateGenericDefinition();
                definition.DefinitionId = MobaCharacterHfsmProfile.HeroDefinitionId(_newHeroId);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, DefinitionJson.Save(definition));
                var binding = MobaCharacterBindings.For(definition);
                binding.DefinitionId = definition.DefinitionId;
                _characterBindings.Add(JObject.FromObject(binding));
                File.WriteAllText(MobaStateMachinePublisher.SourcePath("character_bindings"), _characterBindings.ToString(Formatting.Indented) + "\n");
            }

            AssetDatabase.Refresh();
            _activeCharacterName = name;
            Reload();
        }

        private void DrawParameters()
        {
            var parameters = _character["parameters"] as JArray;
            if (parameters == null)
            {
                parameters = new JArray();
                _character["parameters"] = parameters;
                _dirty = true;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("HFSM Parameters", EditorStyles.boldLabel);
                    if (GUILayout.Button("+", GUILayout.Width(28)))
                    {
                        var nextId = parameters.OfType<JObject>()
                            .Select(parameter => (int?)parameter["id"] ?? 0)
                            .DefaultIfEmpty(0).Max() + 1;
                        parameters.Add(new JObject
                        {
                            ["id"] = nextId,
                            ["name"] = "Parameter" + nextId,
                            ["defaultValue"] = 0f,
                            ["supportedOperations"] = new JArray()
                        });
                        _dirty = true;
                    }
                }

                for (var i = 0; i < parameters.Count; i++)
                {
                    var parameter = parameters[i] as JObject;
                    if (parameter == null) continue;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        IntField(parameter, "id", "ID");
                        StringField(parameter, "name", "Name");
                        FloatField(parameter, "defaultValue", "Default");
                        IntegerListField(parameter, "supportedOperations", "Operations");
                        if (GUILayout.Button("-", GUILayout.Width(24)))
                        {
                            parameters.RemoveAt(i--);
                            _dirty = true;
                        }
                    }
                }
            }
        }

        private void IntegerListField(JObject obj, string key, string label)
        {
            var values = obj[key] as JArray ?? new JArray();
            var before = string.Join(",", values.Select(value => (int)value));
            var after = EditorGUILayout.TextField(label, before);
            if (after == before) return;
            var parsed = new List<int>();
            foreach (var part in after.Split(','))
            {
                var value = part.Trim();
                if (value.Length == 0) continue;
                if (!int.TryParse(value, out var operation))
                {
                    ShowNotification(new GUIContent("Operations must be integer ModifierOp values."));
                    return;
                }
                parsed.Add(operation);
            }
            obj[key] = new JArray(parsed);
            _dirty = true;
        }

        private void DrawGraph(JArray states, JArray transitions)
        {
            var bounds = GUILayoutUtility.GetRect(100, 235, GUILayout.ExpandWidth(true));
            GUI.Box(bounds, GUIContent.none);
            var count = Mathf.Max(1, states.Count);
            var width = Mathf.Min(150, (bounds.width - 32) / count - 12);
            var rects = new Rect[states.Count];
            for (var i = 0; i < states.Count; i++)
            {
                var x = bounds.x + 16 + i * ((bounds.width - 32) / count);
                rects[i] = new Rect(x, bounds.y + 80, width, 52);
            }
            Handles.BeginGUI();
            foreach (JObject transition in transitions)
            {
                var from = IndexOf(states, (string)transition["fromStateId"] ?? (string)transition["from"]);
                var to = IndexOf(states, (string)transition["toStateId"] ?? (string)transition["to"]);
                if (from < 0 || to < 0) continue;
                var start = new Vector3(rects[from].center.x, rects[from].yMax, 0);
                var end = new Vector3(rects[to].center.x, rects[to].yMax, 0);
                if (from == to) end.x += 12;
                Handles.color = Color.gray;
                Handles.DrawBezier(start, end, start + Vector3.down * 70,
                    end + Vector3.down * 70, Color.gray, null, 2);
            }
            Handles.EndGUI();
            for (var i = 0; i < states.Count; i++)
                if (GUI.Button(rects[i], (string)states[i]["id"], i == _selectedState ? EditorStyles.miniButtonMid : GUI.skin.button))
                {
                    _selectedState = i;
                    _selectedTransition = -1;
                }
        }

        private static int IndexOf(JArray states, string id)
        {
            for (var i = 0; i < states.Count; i++)
                if ((string)states[i]["id"] == id) return i;
            return -1;
        }

        private void DrawStateList(JArray states)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(320)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("状态", EditorStyles.boldLabel);
                    if (GUILayout.Button("+", GUILayout.Width(28)))
                    {
                        var id = "new_state_" + states.Count;
                        states.Add(_kind == 0
                            ? JObject.Parse("{\"id\":\"" + id + "\",\"behaviorKey\":\"\",\"childMachineId\":\"\",\"requiresExitApproval\":false,\"logicEntryActionKeys\":[],\"logicExitActionKeys\":[]}")
                            : JObject.Parse("{\"id\":\"" + id + "\",\"kind\":\"actionState\",\"completionPolicy\":\"hold\",\"behaviorRoot\":{\"kind\":\"action\",\"type\":\"projectile.resume\"}}"));
                        _selectedState = states.Count - 1;
                        _dirty = true;
                    }
                }
                for (var i = 0; i < states.Count; i++)
                {
                    if (GUILayout.Button((string)states[i]["id"], i == _selectedState ? EditorStyles.miniButtonMid : EditorStyles.miniButton))
                        _selectedState = i;
                }
                if (_selectedState < 0 || _selectedState >= states.Count) return;
                var state = (JObject)states[_selectedState];
                StringField(state, "id", "状态 ID");
                if (_kind == 0)
                {
                    StringField(state, "behaviorKey", "行为绑定");
                    StringField(state, "childMachineId", "子状态机");
                    BoolField(state, "requiresExitApproval", "需要退出批准");
                    StringListField(state, "logicEntryActionKeys", "逻辑进入动作", "trigger.enter.<id>");
                    StringListField(state, "logicExitActionKeys", "逻辑退出动作", "trigger.exit.<id>");
                }
                else
                {
                    var isMachine = (string)state["kind"] == "stateMachine";
                    var nextKind = EditorGUILayout.Popup("节点类型", isMachine ? 1 : 0,
                        new[] { "动作状态", "子状态机" });
                    if ((nextKind == 1) != isMachine)
                    {
                        state["kind"] = nextKind == 1 ? "stateMachine" : "actionState";
                        if (nextKind == 1)
                        {
                            state.Remove("behaviorRoot");
                            state.Remove("completionPolicy");
                            state["startState"] = "";
                            state["states"] = new JArray();
                            state["transitions"] = new JArray();
                        }
                        else
                        {
                            state.Remove("states");
                            state.Remove("transitions");
                            state.Remove("startState");
                            state["completionPolicy"] = "hold";
                            state["behaviorRoot"] = NewBehavior("action");
                        }
                        _dirty = true;
                    }
                    BoolField(state, "needsExitTime", "等待退出");
                    if ((string)state["kind"] == "actionState")
                    {
                        StringField(state, "completionPolicy", "完成策略");
                        DrawBehavior((JObject)state["behaviorRoot"]);
                    }
                    else
                    {
                        StringField(state, "startState", "子状态初始项");
                        BoolField(state, "rememberLastState", "记住上次状态");
                        if (GUILayout.Button("进入子状态机"))
                        {
                            _nested.Add(state);
                            _selectedState = _selectedTransition = -1;
                        }
                    }
                }
                if (GUILayout.Button("删除所选状态"))
                {
                    states.RemoveAt(_selectedState);
                    _selectedState = -1;
                    _dirty = true;
                }
            }
        }

        private void DrawBehavior(JObject behavior)
        {
            if (behavior == null) return;
            var kinds = new[] { "action", "condition", "delay", "sequence", "selector",
                "parallel", "invert", "repeat", "timeout" };
            var kind = (string)behavior["kind"] ?? "action";
            var index = Array.IndexOf(kinds, kind);
            var next = EditorGUILayout.Popup("行为类型", Math.Max(0, index), kinds);
            if (next != index)
            {
                var replacement = NewBehavior(kinds[next]);
                behavior.RemoveAll();
                foreach (var property in replacement.Properties().ToArray())
                    behavior.Add(property.Name, property.Value.DeepClone());
                kind = kinds[next];
                _dirty = true;
            }
            if (kind == "action")
            {
                StringField(behavior, "type", "动作");
                StringField(behavior, "argument", "参数");
            }
            else if (kind == "condition")
                StringField(behavior, "condition", "条件");
            else if (kind == "delay")
            {
                FloatField(behavior, "durationSeconds", "时长 (秒)");
                BoolField(behavior, "useUnscaledTime", "非缩放时间");
            }
            else if (kind == "sequence" || kind == "selector" || kind == "parallel")
            {
                if (kind == "parallel")
                {
                    StringField(behavior, "successPolicy", "成功策略");
                    StringField(behavior, "failurePolicy", "失败策略");
                }
                var children = (JArray)behavior["children"];
                for (var i = 0; i < children.Count; i++)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField("子行为 " + (i + 1), EditorStyles.boldLabel);
                        DrawBehavior((JObject)children[i]);
                        if (GUILayout.Button("删除子行为"))
                        {
                            children.RemoveAt(i);
                            _dirty = true;
                            i--;
                        }
                    }
                }
                if (GUILayout.Button("添加子行为"))
                {
                    children.Add(NewBehavior("action"));
                    _dirty = true;
                }
            }
            else
            {
                if (kind == "repeat") IntField(behavior, "repeatCount", "重复次数");
                if (kind == "timeout")
                {
                    FloatField(behavior, "durationSeconds", "超时 (秒)");
                    BoolField(behavior, "useUnscaledTime", "非缩放时间");
                }
                EditorGUILayout.LabelField("子行为", EditorStyles.boldLabel);
                DrawBehavior((JObject)behavior["child"]);
            }
        }

        private static JObject NewBehavior(string kind)
        {
            var behavior = new JObject { ["kind"] = kind };
            switch (kind)
            {
                case "action": behavior["type"] = "projectile.resume"; break;
                case "condition": behavior["condition"] = ""; break;
                case "delay": behavior["durationSeconds"] = 0.1f; break;
                case "sequence":
                case "selector":
                case "parallel": behavior["children"] = new JArray(NewBehavior("action")); break;
                case "invert":
                case "repeat":
                case "timeout": behavior["child"] = NewBehavior("action"); break;
            }
            if (kind == "repeat") behavior["repeatCount"] = 1;
            if (kind == "timeout") behavior["durationSeconds"] = 1f;
            return behavior;
        }

        private void DrawTransitionList(JArray transitions)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(320)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("转换", EditorStyles.boldLabel);
                    if (GUILayout.Button("+", GUILayout.Width(28)))
                    {
                        transitions.Add(_kind == 0
                            ? JObject.Parse("{\"id\":\"new_transition\",\"fromStateId\":\"\",\"toStateId\":\"\",\"conditionKey\":\"\",\"priority\":0}")
                            : JObject.Parse("{\"from\":\"\",\"to\":\"\",\"mode\":\"onSucceeded\"}"));
                        _selectedTransition = transitions.Count - 1;
                        _dirty = true;
                    }
                }
                for (var i = 0; i < transitions.Count; i++)
                {
                    var t = transitions[i];
                    var label = ((string)t["fromStateId"] ?? (string)t["from"]) + " -> " +
                                ((string)t["toStateId"] ?? (string)t["to"]);
                    if (GUILayout.Button(label, i == _selectedTransition ? EditorStyles.miniButtonMid : EditorStyles.miniButton))
                        _selectedTransition = i;
                }
                if (_selectedTransition < 0 || _selectedTransition >= transitions.Count) return;
                var transition = (JObject)transitions[_selectedTransition];
                if (_kind == 0)
                {
                    StringField(transition, "id", "转换 ID");
                    StringField(transition, "fromStateId", "源状态");
                    StringField(transition, "toStateId", "目标状态");
                    StringField(transition, "conditionKey", "条件绑定");
                    IntField(transition, "priority", "优先级");
                }
                else
                {
                    StringField(transition, "from", "源状态");
                    StringField(transition, "to", "目标状态");
                    StringField(transition, "mode", "触发模式");
                    StringField(transition, "condition", "条件");
                    IntField(transition, "priority", "优先级");
                }
                if (GUILayout.Button("删除所选转换"))
                {
                    transitions.RemoveAt(_selectedTransition);
                    _selectedTransition = -1;
                    _dirty = true;
                }
            }
        }

        private void StringField(JObject obj, string key, string label)
        {
            var before = (string)obj[key] ?? string.Empty;
            var after = EditorGUILayout.TextField(label, before);
            if (after == before) return;
            obj[key] = after;
            _dirty = true;
        }

        private void StringListField(JObject obj, string key, string label, string placeholder)
        {
            var values = obj[key] as JArray ?? new JArray();
            var before = string.Join(",", values.Select(value => (string)value ?? string.Empty));
            var after = EditorGUILayout.TextField(new GUIContent(label, placeholder), before);
            if (after == before) return;
            obj[key] = new JArray(after.Split(',').Select(value => value.Trim())
                .Where(value => value.Length > 0));
            _dirty = true;
        }

        private void BoolField(JObject obj, string key, string label)
        {
            var before = (bool?)obj[key] ?? false;
            var after = EditorGUILayout.Toggle(label, before);
            if (after == before) return;
            obj[key] = after;
            _dirty = true;
        }

        private void IntField(JObject obj, string key, string label)
        {
            var before = (int?)obj[key] ?? 0;
            var after = EditorGUILayout.IntField(label, before);
            if (after == before) return;
            obj[key] = after;
            _dirty = true;
        }

        private void FloatField(JObject obj, string key, string label)
        {
            var before = (float?)obj[key] ?? 0;
            var after = EditorGUILayout.FloatField(label, before);
            if (Math.Abs(after - before) < 0.00001f) return;
            obj[key] = after;
            _dirty = true;
        }

        private void LoadSelectedBinding()
        {
            var id = (string)_character["definitionId"];
            var binding = _characterBindings.OfType<JObject>().SingleOrDefault(item => (string)item["DefinitionId"] == id);
            _bindingJson = (binding ?? JObject.FromObject(new MobaCharacterBindings { DefinitionId = id })).ToString(Formatting.Indented);
        }

        private void Save()
        {
            if (_character == null || _actors == null) return;
            var characterJson = _character.ToString(Formatting.Indented) + "\n";
            var actorJson = _actors.ToString(Formatting.Indented) + "\n";
            var binding = JObject.Parse(_bindingJson);
            var definition = DefinitionJson.Load(characterJson);
            binding.ToObject<MobaCharacterBindings>().Validate(definition);
            var pendingBindings = (JArray)_characterBindings.DeepClone();
            var old = pendingBindings.OfType<JObject>().SingleOrDefault(item =>
                (string)item["DefinitionId"] == definition.DefinitionId);
            if (old != null) old.Replace(binding); else pendingBindings.Add(binding);
            var bindingsJson = pendingBindings.ToString(Formatting.Indented) + "\n";
            MobaStateMachinePublisher.Validate(_activeCharacterName, characterJson, bindingsJson);
            MobaStateMachinePublisher.Validate("actor_state_machines", actorJson);
            File.WriteAllText(MobaStateMachinePublisher.SourcePath(_activeCharacterName), characterJson);
            File.WriteAllText(MobaStateMachinePublisher.SourcePath("actor_state_machines"), actorJson);
            File.WriteAllText(MobaStateMachinePublisher.SourcePath("character_bindings"), bindingsJson);
            _characterBindings = pendingBindings;
            _dirty = false;
            AssetDatabase.Refresh();
        }
    }
}
#endif
