#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.ActionEditorImpl;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Platform.Unity.Presentation;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Game.Flow;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    /// <summary>Read-only inspection of published templates; Excel remains the authoring source.</summary>
    public sealed class MobaPresentationTemplateInspector : EditorWindow
    {
        [Serializable] private sealed class Templates { public PresentationTemplateDTO[] Items; }
        [Serializable] private sealed class Assets { public VfxDTO[] Items; }
        private PresentationTemplateDTO[] _templates = Array.Empty<PresentationTemplateDTO>();
        private VfxDTO[] _assets = Array.Empty<VfxDTO>();
        private string[] _labels = Array.Empty<string>();
        private int _selected;
        private GameObject _actor;
        private float _cueScale = 1f;
        private Vector3 _sourceScale = Vector3.one;
        private Vector2 _scroll;
        private int _mode;
        private bool _followTimeline;
        private GameObject _instance;
        private string _selectionMessage;
        private readonly List<PresentationScaleSource> _sourceSnapshot = new List<PresentationScaleSource>();

        [MenuItem("Tools/AbilityKit/Demos/Moba/Vfx/Inspect Published Templates")]
        public static void Open() => GetWindow<MobaPresentationTemplateInspector>("VFX Templates");

        private void OnEnable() => Reload();

        private void OnInspectorUpdate()
        {
            if (_followTimeline) ReadTimelineSelection();
            if (_followTimeline || _mode == 1) Repaint();
        }

        private void OnSelectionChange()
        {
            var selected = Selection.activeGameObject;
            if (selected != null && selected.GetComponentInParent<UnityPresentationInstanceState>() != null)
            {
                _instance = selected;
                _mode = 1;
            }
            Repaint();
        }

        private void ReadTimelineSelection()
        {
            if (!(NBC.ActionEditor.App.FistSelect is PlayParticle clip))
            {
                _selectionMessage = "ActionEditor 当前未选中 PlayParticle 片段。";
                return;
            }
            var index = Array.FindIndex(_templates, template => template.Id == clip.presentationTemplateId);
            if (index < 0)
            {
                _selectionMessage = "选中片段的表现模板未配置或未发布：" + clip.presentationTemplateId;
                return;
            }
            _selected = index;
            _selectionMessage = "ActionEditor 选中模板：" + clip.presentationTemplateId + "；来源对象与倍率使用下方预览输入。";
        }

        private void Reload()
        {
            var templates = Resources.Load<TextAsset>("luban/moba/presentation_templates");
            var assets = Resources.Load<TextAsset>("vfx/vfx");
            _templates = templates != null ? JsonUtility.FromJson<Templates>("{\"Items\":" + templates.text + "}").Items : Array.Empty<PresentationTemplateDTO>();
            _assets = assets != null ? JsonUtility.FromJson<Assets>("{\"Items\":" + assets.text + "}").Items : Array.Empty<VfxDTO>();
            _templates = _templates ?? Array.Empty<PresentationTemplateDTO>();
            _assets = _assets ?? Array.Empty<VfxDTO>();
            _labels = Array.ConvertAll(_templates, template => template.Id + "  " + template.Name);
            _selected = Mathf.Clamp(_selected, 0, Math.Max(0, _templates.Length - 1));
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("只读检查已发布配置。修改 T-特效表.xlsx 后运行 export_pipeline.py --apply，再刷新本窗口。", MessageType.Info);
            if (GUILayout.Button("刷新已发布模板")) Reload();
            _mode = GUILayout.Toolbar(_mode, new[] { "模板估算", "运行时实例" });
            if (_mode == 1) { InspectRuntime(); return; }
            _followTimeline = EditorGUILayout.Toggle("跟随 ActionEditor 选择", _followTimeline);
            if (GUILayout.Button("读取 ActionEditor 选中片段")) ReadTimelineSelection();
            if (!string.IsNullOrEmpty(_selectionMessage)) EditorGUILayout.HelpBox(_selectionMessage, MessageType.Info);
            if (_templates.Length == 0) return;
            _selected = EditorGUILayout.Popup("模板", _selected, _labels);
            _actor = (GameObject)EditorGUILayout.ObjectField("预览来源对象", _actor, typeof(GameObject), true);
            _cueScale = EditorGUILayout.FloatField("本次播放倍率", _cueScale);
            _sourceScale = EditorGUILayout.Vector3Field("来源倍率合成值", _sourceScale);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            Inspect(_templates[_selected]);
            EditorGUILayout.EndScrollView();
        }

        private void InspectRuntime()
        {
            _instance = (GameObject)EditorGUILayout.ObjectField("表现实例 / 模型", _instance, typeof(GameObject), true);
            if (GUILayout.Button("读取场景当前选择")) _instance = Selection.activeGameObject;
            if (_instance == null) return;
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var state = _instance.GetComponentInParent<UnityPresentationInstanceState>();
            if (state != null && state.IsBound)
            {
                var request = state.Request;
                EditorGUILayout.LabelField("后端 / 请求", state.Backend + " / " + request.LogicalKey);
                EditorGUILayout.LabelField("模板 / 资源 ID", request.TemplateId + " / " + request.AssetId);
                EditorGUILayout.LabelField("Prefab 基础缩放", state.BaseScale.ToString("F3"));
                EditorGUILayout.LabelField("已解析请求尺寸倍率", request.Scale.ToString("G6"));
                EditorGUILayout.LabelField("Radius 尺寸倍率", request.Radius > 0 ? request.Radius.ToString("G6") : "1 (未配置)");
                EditorGUILayout.LabelField("父缩放 / 偏移", request.ParentScaleMode + " / " + request.Attachment.OffsetSpace);
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("有效挂点", state.AttachmentTarget, typeof(Transform), true);
                EditorGUILayout.LabelField("最近来源世界缩放", state.LastTargetScale.ToString("F3"));
                EditorGUILayout.LabelField("脱离 / 排空", state.IsDetached + " / " + state.IsStopping);
                EditorGUILayout.LabelField("抽象排序", request.Ordering.Mode + " / " + request.Ordering.Priority);
                DrawSources(state.Sources, PresentationScaleChannel.Vfx | PresentationScaleChannel.Cue);
                EditorGUILayout.HelpBox(state.RequestScaleInSources
                    ? "请求倍率和 Radius 已在 cue.request / cue.radius 来源中，只计入一次。"
                    : "此账本列出后端来源；请求倍率和 Radius 在账本外各计入一次。", MessageType.Info);
                DrawActualTransform(state.transform);
            }
            else
            {
                var scales = _instance.GetComponentInParent<BattlePresentationScaleState>();
                if (scales != null)
                {
                    EditorGUILayout.LabelField("模型基础缩放", scales.BaseModelScale.ToString("F3"));
                    EditorGUILayout.LabelField("逻辑实体缩放", scales.LogicalEntityScale.ToString("F3"));
                    DrawSources(scales.Sources, PresentationScaleChannel.Avatar | PresentationScaleChannel.Model);
                    DrawActualTransform(scales.transform);
                }
                else EditorGUILayout.HelpBox("所选对象没有已绑定的表现实例或模型缩放账本。", MessageType.Info);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawSources(PresentationScaleResolver resolver, PresentationScaleChannel channels)
        {
            resolver.CopySources(_sourceSnapshot);
            EditorGUILayout.LabelField("实际尺寸来源账本", _sourceSnapshot.Count + " 项");
            foreach (var source in _sourceSnapshot)
            {
                var applies = (source.Channels & channels) != 0;
                EditorGUILayout.LabelField(source.SourceId + (applies ? "" : " [通道不适用]"),
                    new Vector3(source.Multiplier.X, source.Multiplier.Y, source.Multiplier.Z).ToString("G6") +
                    " / " + source.Mode + " / P" + source.Priority + " / " + source.Channels);
            }
            var effective = resolver.Resolve(channels, PresentationVector3.One);
            EditorGUILayout.LabelField("账本有效合成倍率", new Vector3(effective.X, effective.Y, effective.Z).ToString("G6"));
        }

        private static void DrawActualTransform(Transform visual)
        {
            EditorGUILayout.LabelField("实际世界位置", visual.position.ToString("F3"));
            EditorGUILayout.LabelField("实际局部缩放", visual.localScale.ToString("F3"));
            EditorGUILayout.LabelField("实际世界缩放", visual.lossyScale.ToString("F3"));
        }

        private void Inspect(PresentationTemplateDTO template)
        {
            var assetId = template.AssetId > 0 ? template.AssetId : template.Id;
            var asset = Array.Find(_assets, item => item != null && item.Id == assetId);
            if (asset == null)
            {
                EditorGUILayout.HelpBox("找不到特效资源 ID: " + assetId, MessageType.Error);
                return;
            }
            var resource = !string.IsNullOrWhiteSpace(template.ResourceKey) ? template.ResourceKey :
                !string.IsNullOrWhiteSpace(asset.ResourceKey) ? asset.ResourceKey : asset.Resource;
            EditorGUILayout.LabelField("资源", resource ?? string.Empty);
            var prefab = Resources.Load<GameObject>(resource ?? string.Empty);
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false);
            if (prefab == null) EditorGUILayout.HelpBox("资源无法通过 Resources 加载。", MessageType.Error);
            var target = _actor != null ? _actor.transform : null;
            if (target != null && !string.IsNullOrEmpty(template.Socket))
            {
                var socket = target.Find(template.Socket);
                if (socket != null) target = socket;
                else EditorGUILayout.HelpBox("挂点缺失，运行时回退到来源根节点。", MessageType.Warning);
            }
            if (template.AttachMode == 0) target = null;
            EditorGUILayout.LabelField("有效目标", target != null ? target.name : template.AttachMode == 0 ? "World" : "等待来源解析");
            EditorGUILayout.LabelField("持续跟随", (template.AttachMode != 0 && template.Follow).ToString());
            EditorGUILayout.LabelField("偏移空间", ((PresentationOffsetSpace)template.OffsetSpace).ToString());
            EditorGUILayout.LabelField("父缩放", ((PresentationParentScaleMode)template.ParentScaleMode).ToString());
            var offset = new Vector3(template.OffsetX, template.OffsetY, template.OffsetZ);
            if (target != null)
            {
                var position = template.OffsetSpace == (int)PresentationOffsetSpace.World ? target.position + offset : target.TransformPoint(offset);
                EditorGUILayout.LabelField("预计世界位置", position.ToString("F3"));
            }
            var scale = Vector3.Scale(prefab != null ? prefab.transform.localScale : Vector3.one, _sourceScale) *
                (template.Scale > 0 ? template.Scale : 1f) * (_cueScale > 0 ? _cueScale : 1f) *
                (template.Radius > 0 ? template.Radius : 1f);
            if (template.Follow && target != null && template.ParentScaleMode == (int)PresentationParentScaleMode.Inherit)
                scale = Vector3.Scale(scale, target.lossyScale);
            EditorGUILayout.LabelField("预计世界缩放", scale.ToString("F3"));
            EditorGUILayout.LabelField("抽象排序", ((PresentationOrderingMode)template.OrderingMode) + " / " + template.OrderingPriority);
            EditorGUILayout.HelpBox("最终渲染顺序由当前排序适配器解释。此窗口不绑定 Layer、Order 或深度实现。缩放显示为常见均匀父缩放下的估计。", MessageType.Info);
            var policy = MobaPresentationTemplateResolver.ResolveLifecycle(template);
            EditorGUILayout.LabelField("堆叠 / 停止", policy.Stack + " / " + policy.Stop);
            EditorGUILayout.LabelField("目标丢失 / 完成", policy.TargetLoss + " / " + policy.Completion);
            EditorGUILayout.LabelField("排空上限", policy.EffectiveStopGraceMs + " ms (unscaled)");
            var duration = template.DefaultDurationMs > 0 ? template.DefaultDurationMs : asset.DurationMs;
            EditorGUILayout.LabelField("默认时长", duration + " ms");
            if (prefab != null)
            {
                var particles = prefab.GetComponentsInChildren<ParticleSystem>(true).Length;
                var directors = prefab.GetComponentsInChildren<PlayableDirector>(true).Length;
                if (policy.Stop == PresentationStopPolicy.Drain && particles == 0)
                    EditorGUILayout.HelpBox("默认 Unity 适配器不支持此资源排空，停止时立即回收。", MessageType.Warning);
                if (policy.Completion == PresentationCompletionPolicy.ResourceOrDuration && particles == 0 && directors == 0)
                    EditorGUILayout.HelpBox("默认适配器无法检测资源完成，使用配置时长。", MessageType.Warning);
            }
            if (policy.Completion != PresentationCompletionPolicy.Manual && duration <= 0)
                EditorGUILayout.HelpBox("无正时长兜底；若资源无法完成，需要显式停止。", MessageType.Warning);
        }
    }
}
#endif
