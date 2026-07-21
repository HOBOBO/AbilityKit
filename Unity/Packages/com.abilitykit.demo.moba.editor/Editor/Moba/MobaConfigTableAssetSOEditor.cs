#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Flow;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CustomEditor(typeof(MobaConfigTableAssetSO), true)]
    public sealed class MobaConfigTableAssetSOEditor : EditorBase
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Export Config Json"))
            {
                var assetPath = AssetDatabase.GetAssetPath(target);
                var folder = AssetDatabase.IsValidFolder(assetPath)
                    ? assetPath
                    : Path.GetDirectoryName(assetPath)?.Replace('\\', '/');

                MobaConfigJsonExporter.ExportFromFolder(folder);
            }

            DrawInspectorBody();
        }
    }

    [CustomEditor(typeof(BattlePlayersConfigSO))]
    public sealed class BattlePlayersConfigSOEditor : EditorBase
    {
        private string _status;
        private MessageType _statusType = MessageType.Info;

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "修改各玩家的英雄 ID 后，点击按钮同步属性模板、普通攻击和全部主动技能。玩家、队伍、等级及出生配置保持不变。",
                MessageType.Info);

            if (GUILayout.Button("一键同步全部玩家英雄配置", GUILayout.Height(30f)))
            {
                SynchronizeAllPlayers();
            }

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.HelpBox(_status, _statusType);
            }

            DrawInspectorBody();
        }

        private void SynchronizeAllPlayers()
        {
            var configAsset = (BattlePlayersConfigSO)target;
            var errors = new List<string>();
            var changes = new List<PlayerHeroConfigChange>();

            try
            {
                var config = MobaConfigLoader.LoadDefault();
                CollectPlayerChanges(configAsset.Team1Players, config, changes, errors);
                CollectPlayerChanges(configAsset.Team2Players, config, changes, errors);
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }

            if (errors.Count > 0)
            {
                _status = $"同步已取消，失败 {errors.Count} 项：\n" +
                          string.Join("\n", errors);
                _statusType = MessageType.Error;
                return;
            }

            Undo.RecordObject(configAsset, "同步全部玩家英雄配置");
            for (var i = 0; i < changes.Count; i++)
            {
                changes[i].Apply();
            }

            EditorUtility.SetDirty(configAsset);
            AssetDatabase.SaveAssetIfDirty(configAsset);
            _status = $"已同步 {changes.Count} 名玩家的完整英雄配置。";
            _statusType = MessageType.Info;
        }

        private static void CollectPlayerChanges(
            IReadOnlyList<BattlePlayersConfigSO.PlayerConfig> players,
            MobaConfigDatabase config,
            ICollection<PlayerHeroConfigChange> changes,
            ICollection<string> errors)
        {
            if (players == null) return;

            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null) continue;
                if (!TryResolveHero(config, player.HeroId, out var resolved, out var error))
                {
                    errors.Add($"玩家 {player.PlayerId ?? "<empty>"}：{error}");
                    continue;
                }

                changes.Add(new PlayerHeroConfigChange(player, resolved));
            }
        }

        private static bool TryResolveHero(
            MobaConfigDatabase config,
            int heroId,
            out ResolvedHeroConfig resolved,
            out string error)
        {
            resolved = default;
            error = null;
            if (heroId <= 0 || !config.TryGetCharacter(heroId, out var character) || character == null)
            {
                error = $"英雄配置不存在，ID={heroId}";
                return false;
            }

            if (character.AttributeTemplateId <= 0 ||
                !config.TryGetAttributeTemplate(character.AttributeTemplateId, out var attribute) ||
                attribute == null)
            {
                error = $"属性模板不存在，ID={character.AttributeTemplateId}";
                return false;
            }

            var activeSkills = new List<int>();
            var basicAttackSkillId = 0;
            var configuredSkills = character.SkillIds;
            if (configuredSkills != null)
            {
                for (var i = 0; i < configuredSkills.Count; i++)
                {
                    var skillId = configuredSkills[i];
                    if (!config.TryGetSkill(skillId, out var skill) || skill == null)
                    {
                        error = $"技能配置不存在，ID={skillId}";
                        return false;
                    }

                    if (skill.SkillType == SkillType.NormalAttack)
                    {
                        if (basicAttackSkillId <= 0) basicAttackSkillId = skillId;
                    }
                    else if (skill.SkillType != SkillType.Passive)
                    {
                        activeSkills.Add(skillId);
                    }
                }
            }

            if (basicAttackSkillId <= 0)
            {
                basicAttackSkillId = FindBasicAttack(config, heroId);
            }

            if (basicAttackSkillId <= 0)
            {
                error = $"英雄 {heroId} 缺少普通攻击技能";
                return false;
            }

            if (activeSkills.Count == 0)
            {
                error = $"英雄 {heroId} 缺少主动技能";
                return false;
            }

            resolved = new ResolvedHeroConfig(
                character.AttributeTemplateId,
                basicAttackSkillId,
                activeSkills.ToArray());
            return true;
        }

        private static int FindBasicAttack(MobaConfigDatabase config, int heroId)
        {
            var expectedPrefix = heroId * 10000;
            foreach (var skill in config.GetAllSkills())
            {
                if (skill == null ||
                    skill.Id <= expectedPrefix ||
                    skill.Id >= expectedPrefix + 10000)
                {
                    continue;
                }

                if (skill.SkillType == SkillType.NormalAttack) return skill.Id;
            }

            return 0;
        }

        private readonly struct PlayerHeroConfigChange
        {
            private readonly BattlePlayersConfigSO.PlayerConfig _player;
            private readonly ResolvedHeroConfig _resolved;

            public PlayerHeroConfigChange(
                BattlePlayersConfigSO.PlayerConfig player,
                in ResolvedHeroConfig resolved)
            {
                _player = player;
                _resolved = resolved;
            }

            public void Apply()
            {
                _player.AttributeTemplateId = _resolved.AttributeTemplateId;
                _player.BasicAttackSkillId = _resolved.BasicAttackSkillId;
                _player.SkillIds = _resolved.ActiveSkillIds;
            }
        }

        private readonly struct ResolvedHeroConfig
        {
            public readonly int AttributeTemplateId;
            public readonly int BasicAttackSkillId;
            public readonly int[] ActiveSkillIds;

            public ResolvedHeroConfig(
                int attributeTemplateId,
                int basicAttackSkillId,
                int[] activeSkillIds)
            {
                AttributeTemplateId = attributeTemplateId;
                BasicAttackSkillId = basicAttackSkillId;
                ActiveSkillIds = activeSkillIds;
            }
        }
    }

#if ODIN_INSPECTOR
    public abstract class EditorBase : Sirenix.OdinInspector.Editor.OdinEditor
    {
        protected void DrawInspectorBody()
        {
            base.OnInspectorGUI();
        }
    }
#else
    public abstract class EditorBase : UnityEditor.Editor
    {
        protected void DrawInspectorBody()
        {
            DrawDefaultInspector();
        }
    }
#endif
}
#endif
