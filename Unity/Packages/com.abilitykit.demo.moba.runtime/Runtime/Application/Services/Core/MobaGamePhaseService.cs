using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaBattleRunGateCommitter : IService
    {
        void SetInGame(string reason = null);
    }

    [WorldService(typeof(IMobaBattleRunGateCommitter))]
    [WorldService(typeof(MobaLogicWorldRunGateService))]
    public sealed class MobaLogicWorldRunGateService : IService, IMobaBattleRunGateCommitter
    {
        public bool InGame { get; private set; }
        public string LastChangeReason { get; private set; }
        public long ChangeCount { get; private set; }

        public void SetInGame(string reason = null)
        {
            if (InGame) return;

            InGame = true;
            LastChangeReason = reason ?? "logic world battle loop enabled";
            ChangeCount++;
            if (MobaRuntimeLog.IsEnabled(
                    MobaRuntimeLogLevel.Info,
                    MobaRuntimeLogPurpose.Lifecycle))
            {
                MobaRuntimeLog.Info(
                    MobaRuntimeLogModule.Session,
                    MobaRuntimeLogPurpose.Lifecycle,
                    nameof(MobaLogicWorldRunGateService),
                    $"Logic world battle loop enabled. reason={LastChangeReason}, changes={ChangeCount}");
            }
        }

        public void Reset()
        {
            InGame = false;
            LastChangeReason = "reset";
            ChangeCount = 0L;
        }

        public override string ToString()
        {
            return $"inGame={InGame}, changes={ChangeCount}, reason={LastChangeReason}";
        }

        public void Dispose()
        {
            Reset();
        }
    }
}
