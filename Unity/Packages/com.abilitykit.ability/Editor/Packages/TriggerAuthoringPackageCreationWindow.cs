#if UNITY_EDITOR
using System;
using System.IO;
using AbilityKit.Ability.Config.Authoring;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Packages
{
    internal sealed class TriggerAuthoringPackageCreationWindow : EditorWindow
    {
        private static readonly TriggerModuleKind[] KindValues =
        {
            TriggerModuleKind.Ability,
            TriggerModuleKind.Buff,
            TriggerModuleKind.Passive,
            TriggerModuleKind.Projectile,
            TriggerModuleKind.Summon,
            TriggerModuleKind.Custom
        };

        private static readonly string[] KindLabels =
        {
            "技能",
            "增益效果",
            "被动效果",
            "投射物",
            "召唤物",
            "自定义"
        };

        private TriggerAuthoringProjectAsset _project;
        private TriggerModuleKind _kind = TriggerModuleKind.Ability;
        private string _domainId = "ability";
        private string _contentKey = "hero.new";
        private string _displayName = "新技能内容包";
        private string _moduleId = string.Empty;
        private string _owner = string.Empty;
        private string _tags = string.Empty;
        private string _assetDirectory = string.Empty;
        private Action<TriggerAuthoringModuleAsset> _created;

        internal static void Open(
            TriggerAuthoringProjectAsset project,
            string domainId = null,
            Action<TriggerAuthoringModuleAsset> created = null)
        {
            if (project == null) return;
            var window = CreateInstance<TriggerAuthoringPackageCreationWindow>();
            window.titleContent = new GUIContent("创建内容包");
            window.minSize = new Vector2(520f, 330f);
            window.maxSize = new Vector2(760f, 430f);
            window._project = project;
            window._created = created;
            window.ApplyInitialDomain(domainId);
            window._assetDirectory = TriggerAuthoringPackageService.GetDefaultAssetDirectory(
                project,
                window._domainId,
                window._contentKey);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            if (_project == null)
            {
                EditorGUILayout.HelpBox("触发器项目已失效。", MessageType.Error);
                return;
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("所属项目", _project.name);
            var previousKind = _kind;
            var selectedKindIndex = Array.IndexOf(KindValues, _kind);
            selectedKindIndex = EditorGUILayout.Popup(
                "运行时类型",
                Mathf.Max(0, selectedKindIndex),
                KindLabels);
            _kind = KindValues[selectedKindIndex];
            if (_kind != previousKind)
            {
                var previousDefault = TriggerAuthoringPackageCatalog.DomainIdForKind(previousKind);
                if (string.IsNullOrWhiteSpace(_domainId) ||
                    string.Equals(_domainId, previousDefault, StringComparison.OrdinalIgnoreCase))
                    _domainId = TriggerAuthoringPackageCatalog.DomainIdForKind(_kind);
                RefreshDefaultDirectory();
            }

            var previousDomainId = _domainId;
            var previousContentKey = _contentKey;
            _domainId = EditorGUILayout.TextField(
                new GUIContent("业务域 ID", "用于项目树归类，不改变运行时逻辑"),
                _domainId);
            _contentKey = EditorGUILayout.TextField(
                new GUIContent("内容标识", "例如 hero.zhaoyun，同一业务域内必须唯一"),
                _contentKey);
            _displayName = EditorGUILayout.TextField("内容包名称", _displayName);
            _moduleId = EditorGUILayout.TextField(
                new GUIContent("包 ID", "留空时根据业务域和内容标识自动生成"),
                _moduleId);
            if (!string.Equals(previousDomainId, _domainId, StringComparison.Ordinal) ||
                !string.Equals(previousContentKey, _contentKey, StringComparison.Ordinal))
                RefreshDefaultDirectory();

            _owner = EditorGUILayout.TextField("负责人", _owner);
            _tags = EditorGUILayout.TextField(new GUIContent("标签", "使用英文逗号分隔"), _tags);
            DrawDirectory();

            var request = BuildRequest();
            var plan = TriggerAuthoringPackageService.BuildPlan(request);
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("最终包 ID", plan.ModuleId ?? string.Empty);
            if (!plan.IsValid) EditorGUILayout.HelpBox(plan.Error, MessageType.Error);

            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("取消", GUILayout.Width(80f))) Close();
            EditorGUI.BeginDisabledGroup(!plan.IsValid);
            if (GUILayout.Button("创建并打开", GUILayout.Width(110f))) CreatePackage(request);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(8f);
        }

        private void DrawDirectory()
        {
            EditorGUILayout.BeginHorizontal();
            _assetDirectory = EditorGUILayout.TextField("保存目录", _assetDirectory);
            if (GUILayout.Button("选择", EditorStyles.miniButton, GUILayout.Width(52f)))
            {
                var selected = EditorUtility.OpenFolderPanel(
                    "选择内容包目录",
                    ToAbsolutePath(_assetDirectory),
                    string.Empty);
                var assetPath = ToAssetPath(selected);
                if (!string.IsNullOrEmpty(assetPath)) _assetDirectory = assetPath;
            }
            EditorGUILayout.EndHorizontal();
        }

        private TriggerAuthoringPackageCreateRequest BuildRequest()
        {
            return new TriggerAuthoringPackageCreateRequest
            {
                Project = _project,
                Kind = _kind,
                DomainId = _domainId,
                ContentKey = _contentKey,
                DisplayName = _displayName,
                ModuleId = _moduleId,
                Owner = _owner,
                Tags = (_tags ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries),
                AssetDirectory = _assetDirectory
            };
        }

        private void CreatePackage(TriggerAuthoringPackageCreateRequest request)
        {
            var result = TriggerAuthoringPackageService.Create(request);
            if (!result.Success)
            {
                EditorUtility.DisplayDialog("创建内容包失败", result.Error, "确定");
                return;
            }

            Selection.activeObject = result.Package;
            EditorGUIUtility.PingObject(result.Package);
            _created?.Invoke(result.Package);
            Close();
        }

        private void ApplyInitialDomain(string domainId)
        {
            if (string.IsNullOrWhiteSpace(domainId)) return;
            _domainId = domainId;
            foreach (TriggerModuleKind kind in Enum.GetValues(typeof(TriggerModuleKind)))
            {
                if (!string.Equals(
                        TriggerAuthoringPackageCatalog.DomainIdForKind(kind),
                        domainId,
                        StringComparison.OrdinalIgnoreCase)) continue;
                _kind = kind;
                _displayName = "新" + TriggerAuthoringPackageCatalog.DomainDisplayNameForKind(kind) + "内容包";
                break;
            }
        }

        private void RefreshDefaultDirectory()
        {
            _assetDirectory = TriggerAuthoringPackageService.GetDefaultAssetDirectory(
                _project,
                _domainId,
                _contentKey);
        }

        private static string ToAbsolutePath(string assetPath)
        {
            var root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return string.IsNullOrWhiteSpace(assetPath)
                ? Application.dataPath
                : Path.GetFullPath(Path.Combine(root, assetPath));
        }

        private static string ToAssetPath(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath)) return string.Empty;
            var root = (Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath)
                .Replace('\\', '/')
                .TrimEnd('/');
            var normalized = Path.GetFullPath(fullPath).Replace('\\', '/').TrimEnd('/');
            var assetsRoot = root + "/Assets";
            if (!string.Equals(normalized, assetsRoot, StringComparison.OrdinalIgnoreCase) &&
                !normalized.StartsWith(assetsRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("目录无效", "内容包必须保存在当前工程的 Assets 目录中。", "确定");
                return string.Empty;
            }
            return normalized.Substring(root.Length + 1);
        }
    }
}
#endif
