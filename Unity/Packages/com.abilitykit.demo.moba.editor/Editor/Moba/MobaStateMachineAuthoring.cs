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
        private static readonly string[] Names = { "character_hfsm", "actor_state_machines" };

        public static string SourcePath(string name) => ProjectPath(SourceFolder + "/" + name + ".json");

        private static string ProjectPath(string relative) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));

        [MenuItem("Tools/AbilityKit/MOBA/Publish State Machines")]
        public static void Publish()
        {
            var documents = Names.Select(name => File.ReadAllText(SourcePath(name))).ToArray();
            for (var i = 0; i < Names.Length; i++)
                Validate(Names[i], documents[i]);
            for (var i = 0; i < Names.Length; i++)
            {
                var name = Names[i];
                WriteIfChanged(ProjectPath(UnityFolder + "/" + name + ".json"), documents[i]);
                WriteIfChanged(ProjectPath(ConsoleFolder + "/" + name + ".json"), documents[i]);
            }
            AssetDatabase.Refresh();
            Debug.Log("MOBA 状态机已发布到 Unity 和 Console。");
        }

        [MenuItem("Tools/AbilityKit/MOBA/Check State Machines")]
        public static void Check()
        {
            foreach (var name in Names)
            {
                var json = File.ReadAllText(SourcePath(name));
                Validate(name, json);
                var expected = JToken.Parse(json);
                foreach (var path in new[] { UnityFolder, ConsoleFolder })
                {
                    var target = ProjectPath(path + "/" + name + ".json");
                    if (!File.Exists(target) || !JToken.DeepEquals(expected, JToken.Parse(File.ReadAllText(target))))
                        throw new InvalidOperationException($"状态机发布产物与源文件不一致：{target}");
                }
            }
            Debug.Log("MOBA 状态机源文件与两端发布产物一致。");
        }

        public static void Validate(string name, string json)
        {
            if (name == "character_hfsm")
            {
                var definition = DefinitionJson.Load(json);
                if (definition.DefinitionId != MobaCharacterHfsmProfile.Id)
                    throw new InvalidOperationException("角色 HFSM definitionId 与运行时不一致。");
                foreach (var machine in definition.Machines)
                    foreach (var transition in machine.Transitions)
                        if (!string.IsNullOrEmpty(transition.ConditionKey) &&
                            !KnownCharacterConditions.Contains(transition.ConditionKey))
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

        private static readonly string[] KnownCharacterConditions =
        {
            "dead", "alive", "controlled", "casting", "moving", "idle",
            "free.cast", "free.move", "free.idle"
        };

        private static void WriteIfChanged(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (!File.Exists(path) || File.ReadAllText(path) != text)
                File.WriteAllText(path, text);
        }
    }

    public sealed class MobaStateMachineAuthoringWindow : EditorWindow
    {
        private readonly string[] _names = { "character_hfsm", "actor_state_machines" };
        private int _kind;
        private int _profile;
        private int _selectedState = -1;
        private int _selectedTransition = -1;
        private readonly List<JObject> _nested = new List<JObject>();
        private JObject _character;
        private JArray _actors;
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
            _character = JObject.Parse(File.ReadAllText(MobaStateMachinePublisher.SourcePath(_names[0])));
            _actors = JArray.Parse(File.ReadAllText(MobaStateMachinePublisher.SourcePath(_names[1])));
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
                            ? JObject.Parse("{\"id\":\"" + id + "\",\"behaviorKey\":\"\",\"childMachineId\":\"\",\"requiresExitApproval\":false}")
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

        private void Save()
        {
            if (_character == null || _actors == null) return;
            var characterJson = _character.ToString(Formatting.Indented) + "\n";
            var actorJson = _actors.ToString(Formatting.Indented) + "\n";
            MobaStateMachinePublisher.Validate(_names[0], characterJson);
            MobaStateMachinePublisher.Validate(_names[1], actorJson);
            File.WriteAllText(MobaStateMachinePublisher.SourcePath(_names[0]), characterJson);
            File.WriteAllText(MobaStateMachinePublisher.SourcePath(_names[1]), actorJson);
            _dirty = false;
            AssetDatabase.Refresh();
        }
    }
}
#endif
