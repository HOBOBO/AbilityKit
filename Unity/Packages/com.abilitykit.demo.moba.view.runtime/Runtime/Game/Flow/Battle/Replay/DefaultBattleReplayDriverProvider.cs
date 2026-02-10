using System;
using System.IO;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Common.Record.Lockstep;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Game.Flow.Battle.Replay
{
    internal sealed class DefaultBattleReplayDriverProvider : BattleSessionFeature.IBattleReplayDriverProvider
    {
        public bool TryCreate(in BattleStartPlan plan, out LockstepReplayDriver driver)
        {
            driver = null;

            try
            {
                if (string.IsNullOrEmpty(plan.WorldId)) return false;

                var path = plan.InputReplayPath;
                if (string.IsNullOrEmpty(path)) return false;

                LockstepInputRecordFile file;
                var ext = Path.GetExtension(path);
                if (string.Equals(ext, ".bin", StringComparison.OrdinalIgnoreCase))
                {
                    file = LockstepBinaryInputRecordReader.Load(path);
                }
                else
                {
                    file = LockstepJsonInputRecordReader.Load(path);
                }
                if (file == null) return false;

                driver = new LockstepReplayDriver(new WorldId(plan.WorldId), file);
                return true;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[DefaultBattleReplayDriverProvider] TryCreate failed");
                driver = null;
                return false;
            }
        }
    }
}
