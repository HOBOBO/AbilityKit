#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    public sealed class MobaCharacterAnimationSyncWindow : EditorWindow
    {
        private int _entityCode;
        private int _modelId;
        private bool _modelMode = true;
        private Vector2 _scroll;
        private MobaCharacterAnimationSync.ScanResult _scan;
        private string _message;
        private bool _failed;

        [MenuItem("Window/AbilityKit/MOBA 角色动画同步")]
        public static void Open() => OpenForEntity(0);

        public static void OpenForEntity(int entityCode)
        {
            var window = GetWindow<MobaCharacterAnimationSyncWindow>();
            window.titleContent = new GUIContent("角色动画同步");
            window.minSize = new Vector2(900, 550);
            window._entityCode = entityCode;
            if (entityCode > 0) window._modelId = MobaCharacterAnimationSync.ModelForEntity(entityCode);
            window._scan = null;
            window._message = null;
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            _modelMode = GUILayout.Toolbar(_modelMode ? 0 : 1, new[] { "模型动作目录（独立导出）", "旧状态节点（兼容维护）" }) == 0;
            if (EditorGUI.EndChangeCheck()) _scan = null;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                if (_modelMode) _modelId = EditorGUILayout.IntField("模型 ID", _modelId, GUILayout.Width(200));
                else _entityCode = EditorGUILayout.IntField("角色 ID（0=全部）", _entityCode, GUILayout.Width(280));
                if (EditorGUI.EndChangeCheck()) _scan = null;
                if (_modelMode && GUILayout.Button("选择模型", EditorStyles.toolbarButton)) Run(() =>
                {
                    var menu = new GenericMenu();
                    foreach (var model in MobaCharacterAnimationSync.ConfiguredModels())
                    {
                        var modelId = model.Value<int>("Id");
                        menu.AddItem(new GUIContent(modelId + " " + model.Value<string>("Name")), modelId == _modelId,
                            () => { _modelId = modelId; _scan = null; Repaint(); });
                    }
                    menu.ShowAsContext();
                });
                if (GUILayout.Button("扫描 / 预览差异", EditorStyles.toolbarButton)) Run(Scan);
                using (new EditorGUI.DisabledScope(_scan == null))
                {
                    if (GUILayout.Button("应用选中差异", EditorStyles.toolbarButton)) Apply(false);
                    if (GUILayout.Button("同步并发布", EditorStyles.toolbarButton)) Apply(true);
                }
                if (GUILayout.Button("仅检查已绑定动画漂移", EditorStyles.toolbarButton)) Run(MobaCharacterAnimationSync.Check);
            }
            EditorGUILayout.HelpBox((_modelMode ? "按模型扫描，不要求角色或 HFSM。选择要导出的动作，为新动作设置稳定动作键；状态 play 使用 modelId + animationKey 引用。" :
                "仅维护尚未迁移的内联状态节点；新内容请优先使用模型动作目录。") +
                "导出 State 名、素材帧数、帧率和 Layer，保留技能时序与资源内容。" +
                "带时序告警的动作默认不选中；手动选中表示仅接受所列 Clip 元数据，不代表动作已完整验收。", MessageType.Info);
            var python = EditorPrefs.GetString(MobaCharacterAnimationSync.PythonPreference, "python");
            var nextPython = EditorGUILayout.TextField("Python 可执行文件", python);
            if (nextPython != python) EditorPrefs.SetString(MobaCharacterAnimationSync.PythonPreference, nextPython);
            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, _failed ? MessageType.Error : MessageType.Info);
            if (_scan == null) return;
            EditorGUILayout.LabelField($"动作 {_scan.Rows.Count}；引用错误 {_scan.Rows.Count(row => row.Error != null)}；" +
                $"配置差异 {_scan.Rows.Count(row => row.Changed)}；未登记 {_scan.Rows.Count(row => !row.Bound)}");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("选中无告警项"))
                    foreach (var row in _scan.Rows) row.Selected = row.Error == null && row.Warnings.Count == 0;
                if (GUILayout.Button("选中可解析项（含时序告警）"))
                    foreach (var row in _scan.Rows) row.Selected = row.Error == null;
                if (GUILayout.Button("取消全选"))
                    foreach (var row in _scan.Rows) row.Selected = false;
            }
            foreach (var error in _scan.Errors) EditorGUILayout.HelpBox(error, MessageType.Error);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var row in _scan.Rows)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(row.Error != null))
                            row.Selected = EditorGUILayout.Toggle(row.Selected, GUILayout.Width(18));
                        EditorGUILayout.SelectableLabel(row.Label, GUILayout.Height(20));
                        GUILayout.Label(row.Error != null ? "引用错误" : !row.Bound ? "未登记" : row.Changed ? "需要同步" : "已同步", GUILayout.Width(80));
                    }
                    var current = row.State == null ? 0 : row.Choices.IndexOf(row.State) + 1;
                    if (_modelMode)
                    {
                        using (new EditorGUI.DisabledScope(row.Before.Count > 0))
                            row.Target["animationKey"] = EditorGUILayout.TextField("稳定动作键", row.Target.Value<string>("animationKey"));
                        using (new EditorGUI.DisabledScope(row.Before.Count == 0 || row.Error != null))
                            if (GUILayout.Button("复制 play 引用（loop 按状态需求配置）", GUILayout.Width(310)))
                                EditorGUIUtility.systemCopyBuffer = new Newtonsoft.Json.Linq.JObject {
                                    ["id"] = "pose.play", ["type"] = "play", ["modelId"] = _scan.ModelId,
                                    ["animationKey"] = row.Target.Value<string>("animationKey"), ["loop"] = false
                                }.ToString(Newtonsoft.Json.Formatting.None);
                    }
                    var labels = new[] { "请选择正式 Animator State" }.Concat(row.Choices.Select(state => state.Label)).ToArray();
                    var selected = EditorGUILayout.Popup("State → Clip", current, labels);
                    if (selected != current)
                    {
                        row.Selected = false;
                        if (selected > 0) MobaCharacterAnimationSync.Select(_scan, row, row.Choices[selected - 1]);
                        else { row.State = null; row.After = null; row.Error = "请选择正式 Animator State。"; }
                        MobaCharacterAnimationSync.WriteReport(_scan);
                    }
                    EditorGUILayout.LabelField("当前配置", row.Before.ToString(Newtonsoft.Json.Formatting.None));
                    if (row.After != null)
                    {
                        EditorGUILayout.LabelField("待同步", row.After.ToString(Newtonsoft.Json.Formatting.None));
                        var clip = row.State.Clip;
                        var frames = row.After.Value<int>("frameCount");
                        var rate = row.After.Value<double>("frameRate");
                        EditorGUILayout.LabelField("导入 / 逻辑跨度", $"{clip.length:F6} 秒；{clip.frameRate} FPS；" +
                            $"{frames} 素材帧；30 Hz={Math.Ceiling(frames * 30 / rate)} 帧，60 Hz={Math.Ceiling(frames * 60 / rate)} 帧");
                        using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("正式 Clip", clip, typeof(AnimationClip), false);
                    }
                    if (row.Error != null) EditorGUILayout.HelpBox(row.Error, MessageType.Error);
                    foreach (var warning in row.Warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void Apply(bool publish) => Run(() =>
        {
            MobaCharacterAnimationSync.Apply(_scan, _scan.Rows.Where(row => row.Selected), publish);
            Scan();
        });

        private void Scan() => _scan = _modelMode ? MobaCharacterAnimationSync.ScanModel(_modelId) :
            MobaCharacterAnimationSync.Scan(_entityCode);

        private void Run(Action action)
        {
            try { action(); _failed = false; _message = "操作完成。差异报告：" + MobaCharacterAnimationSync.ReportDirectory; }
            catch (Exception ex) { _failed = true; _message = ex.Message; Debug.LogException(ex); }
        }
    }
}
#endif
