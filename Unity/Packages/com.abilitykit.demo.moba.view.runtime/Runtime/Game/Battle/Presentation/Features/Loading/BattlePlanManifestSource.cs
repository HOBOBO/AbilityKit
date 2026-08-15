using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Flow;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Game.Battle.Presentation.Features.Loading
{
    internal sealed class BattlePlanManifestSource : IBattleAssetManifestSource
    {
        private readonly BattleStartPlan _plan;
        private readonly IReadOnlyList<IBattleAssetManifestPlayer> _players;

        public BattlePlanManifestSource(BattleStartPlan plan)
        {
            _plan = plan;
            var loadouts = plan.LaunchSpec.Players;
            if (loadouts == null || loadouts.Length == 0)
            {
                _players = Array.Empty<IBattleAssetManifestPlayer>();
                return;
            }

            var players = new IBattleAssetManifestPlayer[loadouts.Length];
            for (var i = 0; i < loadouts.Length; i++)
            {
                players[i] = new BattlePlanManifestPlayer(loadouts[i]);
            }
            _players = players;
        }

        public IReadOnlyList<IBattleAssetManifestPlayer> Players => _players;
        public int LaunchManifestVersion => Math.Max(1, _plan.LaunchSpec.ConfigVersion);
        public string LaunchManifestHash =>
            "plan:" + (_plan.LaunchSpec.MatchId ?? _plan.World.WorldId ?? string.Empty) +
            ":" + LaunchManifestVersion;
        public long LaunchGeneration => 1L;
    }

    internal sealed class BattlePlanManifestPlayer : IBattleAssetManifestPlayer
    {
        private readonly MobaPlayerLoadout _loadout;

        public BattlePlanManifestPlayer(MobaPlayerLoadout loadout)
        {
            _loadout = loadout;
        }

        public int HeroId => _loadout.HeroId;
        public int BasicAttackSkillId => _loadout.BasicAttackSkillId;
        public IReadOnlyList<int> SkillIds => _loadout.SkillIds ?? Array.Empty<int>();
    }

    internal sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _report;

        public InlineProgress(Action<T> report)
        {
            _report = report ?? throw new ArgumentNullException(nameof(report));
        }

        public void Report(T value)
        {
            _report(value);
        }
    }
}
