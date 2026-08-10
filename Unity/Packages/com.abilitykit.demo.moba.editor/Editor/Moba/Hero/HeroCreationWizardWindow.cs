#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.Demo.Moba.Share.Config;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor.Hero
{
    /// <summary>
    /// 一键创建新英雄所需 SO 资产 + 同步到 JSON。
    /// 菜单：AbilityKit / Moba / Hero / Create New Hero…
    /// </summary>
    public sealed class HeroCreationWizardWindow : EditorWindow
    {
        private const string DefaultAssetFolder = "Assets/AbilityKit/Moba/Heroes";
        private const string DefaultJsonPath =
            "Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/characters.json";

        // ===================== 状态字段（持久化到 EditorPrefs） =====================
        [SerializeField] private string _heroName = "NewHero";
        [SerializeField] private int _heroId;
        [SerializeField] private int _attributeTemplateId;
        [SerializeField] private string _assetFolder = DefaultAssetFolder;
        [SerializeField] private string _charactersJsonPath = DefaultJsonPath;
        [SerializeField] private bool _autoExportJson = true;
        [SerializeField] private bool _useSkillButtons = true;

        // 3 个主动技能槽（槽 2/3/4，对应技能 1/2/3）
        [SerializeField] private SkillSlotState _slot1 = new SkillSlotState { SlotName = "Skill1" };
        [SerializeField] private SkillSlotState _slot2 = new SkillSlotState { SlotName = "Skill2" };
        [SerializeField] private SkillSlotState _slot3 = new SkillSlotState { SlotName = "Skill3" };

        [Serializable]
        public sealed class SkillSlotState
        {
            public string SlotName;
            public string DisplayName = "Skill";
            public int IconId;
            public int CooldownMs = 5000;
            public int Range = 8000;
            public int Category;
            public int SkillType;
            public int CastFlowId;
            public int LevelTableId;
            public int ButtonTemplateId;
            public int VfxId;
            public int ProjectileId;
            public int AoeId;
            public int BuffId;
            public int IndicatorShape; // 0 = Hidden
        }

        private Vector2 _scroll;
        private readonly List<string> _report = new List<string>();
        private bool _autoProposed;

        [MenuItem("Tools/AbilityKit/Demos/Moba/Hero/Create New Hero…")]
        public static void Open()
        {
            var window = GetWindow<HeroCreationWizardWindow>(true, "Hero Creation Wizard", true);
            window.minSize = new Vector2(520f, 640f);
            window.Show();
        }

        private void OnEnable()
        {
            if (!_autoProposed)
            {
                _heroId = HeroIdAllocator.ProposeNextHeroId(_charactersJsonPath);
                _attributeTemplateId = _heroId;
                _autoProposed = true;
            }
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("Create New Hero", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawHeroSection();
            EditorGUILayout.Space();
            DrawSlotsSection();
            EditorGUILayout.Space();
            DrawActionsSection();
            EditorGUILayout.Space();
            DrawReportSection();
            EditorGUILayout.EndScrollView();
        }

        // =============================== GUI =========================================

        private void DrawHeroSection()
        {
            EditorGUILayout.LabelField("Hero", EditorStyles.boldLabel);
            _heroName = EditorGUILayout.TextField("Hero Name", _heroName);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Proposed HeroId", _heroId);
                EditorGUILayout.IntField("AttributeTemplateId", _attributeTemplateId);
            }
            if (GUILayout.Button("Re-propose HeroId from characters.json", GUILayout.Width(280f)))
            {
                _heroId = HeroIdAllocator.ProposeNextHeroId(_charactersJsonPath);
                _attributeTemplateId = _heroId;
            }
            _assetFolder = EditorGUILayout.TextField("Asset Output Folder", _assetFolder);
            _charactersJsonPath = EditorGUILayout.TextField("characters.json Path", _charactersJsonPath);
            _autoExportJson = EditorGUILayout.Toggle("Auto Export JSON after creation", _autoExportJson);
            _useSkillButtons = EditorGUILayout.Toggle("Create SkillButtonTemplateSO (1 per skill)", _useSkillButtons);
        }

        private void DrawSlotsSection()
        {
            EditorGUILayout.LabelField("Active Skills", EditorStyles.boldLabel);
            DrawSlotRow("Skill 1", _slot1);
            DrawSlotRow("Skill 2", _slot2);
            DrawSlotRow("Skill 3", _slot3);
        }

        private void DrawSlotRow(string label, SkillSlotState slot)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            slot.SlotName = label;
            slot.DisplayName = EditorGUILayout.TextField(" DisplayName", slot.DisplayName);
            slot.IconId = EditorGUILayout.IntField(" IconId", slot.IconId);
            slot.CooldownMs = EditorGUILayout.IntField(" CooldownMs", slot.CooldownMs);
            slot.Range = EditorGUILayout.IntField(" Range", slot.Range);
            slot.Category = EditorGUILayout.IntField(" Category", slot.Category);
            slot.SkillType = EditorGUILayout.IntField(" SkillType", slot.SkillType);
            slot.CastFlowId = EditorGUILayout.IntField(" CastFlowId", slot.CastFlowId);
            slot.LevelTableId = EditorGUILayout.IntField(" LevelTableId", slot.LevelTableId);
            slot.ButtonTemplateId = EditorGUILayout.IntField(" ButtonTemplateId", slot.ButtonTemplateId);
            slot.IndicatorShape = EditorGUILayout.IntField(" IndicatorShape (0..8)", slot.IndicatorShape);
            EditorGUILayout.Space(2f);
        }

        private void DrawActionsSection()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Assets", GUILayout.Height(32f)))
                {
                    TryCreateAll();
                }
                if (GUILayout.Button("Export JSON Only", GUILayout.Height(32f)))
                {
                    TryExportJsonOnly();
                }
            }
        }

        private void DrawReportSection()
        {
            EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);
            if (_report.Count == 0)
            {
                EditorGUILayout.LabelField("(empty)", EditorStyles.miniLabel);
                return;
            }
            for (var i = 0; i < _report.Count; i++)
            {
                EditorGUILayout.LabelField(_report[i], EditorStyles.wordWrappedMiniLabel);
            }
        }

        // =============================== 操作 =========================================

        private void TryCreateAll()
        {
            _report.Clear();
            try
            {
                if (string.IsNullOrWhiteSpace(_heroName)) throw new InvalidOperationException("HeroName 不能为空");
                if (_heroId <= 0) throw new InvalidOperationException("HeroId 必须 > 0");
                EnsureFolder(_assetFolder);

                var allocation = HeroIdAllocator.Allocate(_heroId, activeSkillCount: 3);
                var slotStates = new[] { _slot1, _slot2, _slot3 };

                var characterDto = new CharacterDTO
                {
                    Id = _heroId,
                    Name = _heroName,
                    ModelId = _heroId,
                    AttributeTemplateId = _attributeTemplateId,
                    SkillIds = new[] { allocation.BasicAttackSkillId }
                        .Concat(allocation.ActiveSkillIds).ToArray(),
                    PassiveSkillIds = Array.Empty<int>(),
                };

                // CharacterSO
                var characterSO = CreateOrUpdate<CharacterSO>(
                    Path.Combine(_assetFolder, _heroName + "_CharacterCO.asset"),
                    () => new CharacterSO { dataList = new[] { characterDto } });
                _report.Add($"✓ CharacterSO: {AssetPath(characterSO)}");

                // BattleAttributeTemplateSO
                var attrDto = new BattleAttributeTemplateDTO { Id = _attributeTemplateId };
                var attrSO = CreateOrUpdate<BattleAttributeTemplateSO>(
                    Path.Combine(_assetFolder, _heroName + "_AttrCO.asset"),
                    () => new BattleAttributeTemplateSO { dataList = new[] { attrDto } });
                _report.Add($"✓ BattleAttributeTemplateSO: {AssetPath(attrSO)}");

                // Skills（基础攻击 + 3 主动）
                var basicSkillDto = MakeBasicAttackSkill(allocation.BasicAttackSkillId);
                var basicSO = CreateOrUpdate<SkillSO>(
                    Path.Combine(_assetFolder, _heroName + "_Skill_BasicAttackCO.asset"),
                    () => new SkillSO { dataList = new[] { basicSkillDto } });
                _report.Add($"✓ SkillSO(basic): id={allocation.BasicAttackSkillId} {AssetPath(basicSO)}");

                for (var i = 0; i < slotStates.Length; i++)
                {
                    var slot = slotStates[i];
                    var skillId = allocation.ActiveSkillIds[i];
                    var skillDto = new SkillDTO
                    {
                        Id = skillId,
                        Name = string.IsNullOrWhiteSpace(slot.DisplayName) ? $"{_heroName}_{slot.SlotName}" : slot.DisplayName,
                        CooldownMs = Mathf.Max(0, slot.CooldownMs),
                        Range = Mathf.Max(0, slot.Range),
                        IconId = slot.IconId,
                        Category = slot.Category,
                        SkillType = slot.SkillType,
                        Tags = Array.Empty<int>(),
                        SkillButtonTemplateId = slot.ButtonTemplateId,
                        LevelTableId = slot.LevelTableId,
                        PreCastFlowId = 0,
                        CastFlowId = slot.CastFlowId,
                    };
                    var skillSO = CreateOrUpdate<SkillSO>(
                        Path.Combine(_assetFolder, _heroName + $"_Skill{i + 1}CO.asset"),
                        () => new SkillSO { dataList = new[] { skillDto } });
                    _report.Add($"✓ SkillSO({slot.SlotName}): id={skillId} {AssetPath(skillSO)}");

                    // SkillLevelTableSO（默认空等级）
                    var levelTableDto = new SkillLevelTableDTO { Id = slot.LevelTableId, Levels = Array.Empty<SkillLevelDTO>() };
                    var ltSO = CreateOrUpdate<SkillLevelTableSO>(
                        Path.Combine(_assetFolder, _heroName + $"_Skill{i + 1}_LevelTableCO.asset"),
                        () => new SkillLevelTableSO { dataList = new[] { levelTableDto } });
                    _report.Add($"✓ SkillLevelTableSO: id={slot.LevelTableId} {AssetPath(ltSO)}");

                    // SkillButtonTemplateSO（每个技能一个）
                    if (_useSkillButtons)
                    {
                        var btnDto = new SkillButtonTemplateDTO
                        {
                            Id = slot.ButtonTemplateId,
                            Name = $"{_heroName}_{slot.SlotName}_Button",
                            LongPressSeconds = 0.35f,
                            DragThreshold = 12f,
                            EnableAim = slot.IndicatorShape != 0,
                            AimMode = slot.IndicatorShape == 2 || slot.IndicatorShape == 7 ? 1 : 0,
                            AimMaxRadius = 200f,
                            IndicatorShape = Mathf.Clamp(slot.IndicatorShape, 0, 8),
                            IndicatorWorldWidth = slot.IndicatorShape == 4 || slot.IndicatorShape == 7 || slot.IndicatorShape == 8 ? 0.6f : 1.5f,
                            UsePointMode = slot.IndicatorShape == 2 || slot.IndicatorShape == 7 ? 2 : 1,
                            SelectRange = 4f,
                            FaceToAim = true,
                        };
                        var btnSO = CreateOrUpdate<SkillButtonTemplateSO>(
                            Path.Combine(_assetFolder, _heroName + $"_Skill{i + 1}_ButtonCO.asset"),
                            () => new SkillButtonTemplateSO { dataList = new[] { btnDto } });
                        _report.Add($"✓ SkillButtonTemplateSO: id={slot.ButtonTemplateId} {AssetPath(btnSO)}");
                    }
                }

                // 写入 characters.json
                if (_autoExportJson)
                {
                    TryExportJsonOnly(allocation, characterDto);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Hero Wizard",
                    "已创建所有 SO 资产。\nReport 已显示在 Wizard 中。", "OK");
            }
            catch (Exception ex)
            {
                _report.Add($"✗ Error: {ex.Message}");
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Hero Wizard Error", ex.Message, "OK");
            }
        }

        private void TryExportJsonOnly(HeroIdAllocator.AllocatedIds? allocationArg = null, CharacterDTO heroDtoArg = null)
        {
            try
            {
                if (string.IsNullOrEmpty(_charactersJsonPath))
                    throw new InvalidOperationException("charactersJsonPath 为空");
                var allocation = allocationArg ?? HeroIdAllocator.Allocate(_heroId, 3);
                var heroDto = heroDtoArg ?? new CharacterDTO
                {
                    Id = _heroId,
                    Name = _heroName,
                    ModelId = _heroId,
                    AttributeTemplateId = _attributeTemplateId,
                    SkillIds = new[] { allocation.BasicAttackSkillId }
                        .Concat(allocation.ActiveSkillIds).ToArray(),
                    PassiveSkillIds = Array.Empty<int>(),
                };
                var patcher = new HeroJsonPatcher(_charactersJsonPath);
                patcher.Upsert(heroDto, heroDto.SkillIds);
                _report.Add($"✓ characters.json updated: {_charactersJsonPath}");
                AssetDatabase.ImportAsset(_charactersJsonPath);
            }
            catch (Exception ex)
            {
                _report.Add($"✗ JSON export failed: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        private static SkillDTO MakeBasicAttackSkill(int id)
        {
            return new SkillDTO
            {
                Id = id,
                Name = "BasicAttack",
                CooldownMs = 0,
                Range = 0,
                IconId = 0,
                Category = 0,
                SkillType = 0,
                Tags = Array.Empty<int>(),
                SkillButtonTemplateId = 0,
                LevelTableId = 0,
                PreCastFlowId = 0,
                CastFlowId = 0,
            };
        }

        private static T CreateOrUpdate<T>(string assetPath, Func<T> factory) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (existing != null) return existing;

            var dir = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var asset = factory();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;
            if (AssetDatabase.IsValidFolder(folder)) return;
            // 创建不存在的多层文件夹
            var parts = folder.Split('/');
            var cur = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(cur, parts[i]);
                }
                cur = next;
            }
        }

        private static string AssetPath(UnityEngine.Object obj)
        {
            return obj == null ? "(null)" : AssetDatabase.GetAssetPath(obj);
        }
    }
}
#endif
