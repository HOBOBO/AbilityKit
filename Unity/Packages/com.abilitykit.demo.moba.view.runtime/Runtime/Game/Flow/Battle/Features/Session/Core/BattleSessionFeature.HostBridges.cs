namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        void BattleSessionFeature.ISessionPlanHost.StartSession() => StartSession();

        void BattleSessionFeature.ISessionPlanHost.StopSession() => StopSession();

        void BattleSessionFeature.ISessionPlanHost.ApplyAutoPlanActions() => ApplyAutoPlanActions();

        bool BattleSessionFeature.ISessionPlanHost.InvokeModulesPlanBuilt() => InvokeModulesPlanBuilt();

        void BattleSessionFeature.ISessionReplayHost.StartSession() => StartSession();

        void BattleSessionFeature.ISessionReplayHost.StopSession() => StopSession();

        void BattleSessionFeature.ISessionReplayHost.ApplyAutoPlanActions() => ApplyAutoPlanActions();

        float BattleSessionFeature.ISessionReplayHost.GetFixedDeltaSeconds() => GetFixedDeltaSeconds();
    }
}
