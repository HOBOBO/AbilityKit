using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow.Dungeons
{
    /// <summary>
    /// 入场前全量 dry-run 校验。副本↔玩法↔地图↔扩展点↔端口组↔流程 Profile↔宿主模式↔人数
    /// 的交叉校验集中在这里，一次跑完；失败时不创建 battle scope、不建立连接。
    /// </summary>
    public sealed class MobaDungeonValidator
    {
        private readonly MobaDungeonRegistry _registry;
        private readonly Func<string, bool> _profileExists;
        private readonly Func<int, bool> _gameplayExists;
        private readonly Func<int, bool> _mapExists;

        public MobaDungeonValidator(
            MobaDungeonRegistry registry,
            Func<string, bool> profileExists = null,
            Func<int, bool> gameplayExists = null,
            Func<int, bool> mapExists = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _profileExists = profileExists ?? (_ => true);
            _gameplayExists = gameplayExists ?? (_ => true);
            _mapExists = mapExists ?? (_ => true);
        }

        /// <summary>
        /// 校验一次副本入场。返回失败原因列表（空表示通过），不抛异常，
        /// 以便大厅把全部问题一次性展示给玩家。
        /// </summary>
        public IReadOnlyList<string> Validate(MobaDungeonDef dungeon, string hostMode, int playerCount)
        {
            var errors = new List<string>();
            if (dungeon == null)
            {
                errors.Add("副本定义为空。");
                return errors;
            }

            if (dungeon.GameplayId <= 0 || !_gameplayExists(dungeon.GameplayId))
                errors.Add($"副本 {dungeon.Id} 引用了不存在的玩法 {dungeon.GameplayId}。");
            if (!_profileExists(dungeon.FlowProfileId))
                errors.Add($"副本 {dungeon.Id} 引用了不存在的战斗流程 Profile「{dungeon.FlowProfileId}」。");

            if (!_registry.ContainsGameplayHandler(dungeon.GameplayHandlerId))
                errors.Add($"副本 {dungeon.Id} 的玩法处理器「{dungeon.GameplayHandlerId}」未注册。");
            if (!_registry.ContainsOutcomeRule(dungeon.OutcomeRuleId))
                errors.Add($"副本 {dungeon.Id} 的胜负规则「{dungeon.OutcomeRuleId}」未注册。");

            // SceneProviderId 允许为空（等价于 none），但填了就必须注册。
            if (!string.IsNullOrWhiteSpace(dungeon.SceneProviderId) &&
                !_registry.ContainsSceneProvider(dungeon.SceneProviderId))
                errors.Add($"副本 {dungeon.Id} 的场景装配器「{dungeon.SceneProviderId}」未注册。");

            foreach (var portGroup in dungeon.RequiredPortGroups)
                if (!_registry.ContainsPortGroup(portGroup))
                    errors.Add($"副本 {dungeon.Id} 引用了未声明的 Feature 端口组「{portGroup}」。");

            if (_registry.ContainsGameplayHandler(dungeon.GameplayHandlerId))
            {
                var handler = _registry.GetGameplayHandler(dungeon.GameplayHandlerId);
                foreach (var portGroup in dungeon.RequiredPortGroups)
                    if (!handler.RequiresPortGroup(portGroup))
                        errors.Add($"副本 {dungeon.Id} 需要端口组「{portGroup}」，但玩法处理器「{handler.Id}」未声明该依赖。");
            }

            if (!dungeon.AllowsHostMode(hostMode))
                errors.Add($"副本 {dungeon.Id} 不支持宿主模式 {hostMode ?? "unknown"}。");
            if (!dungeon.AcceptsPlayerCount(playerCount))
                errors.Add($"副本 {dungeon.Id} 不接受 {playerCount} 名玩家。");

            return errors;
        }

        /// <summary>校验通过则返回 true；失败时抛出包含全部原因的异常。</summary>
        public bool ValidateOrThrow(MobaDungeonDef dungeon, string hostMode, int playerCount)
        {
            var errors = Validate(dungeon, hostMode, playerCount);
            if (errors.Count == 0) return true;
            throw new InvalidOperationException(
                "副本入场校验失败：\n - " + string.Join("\n - ", errors));
        }
    }
}
