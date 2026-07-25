using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Editor.Diagnostics
{
    /// <summary>
    /// 共享辅助：优先消费窗口显式注入的只读诊断会话；实时模式下再沿用
    /// Facade → Session → World → Services 路径解析 Local Session。
    /// </summary>
    internal enum BattleDebugDiagnosticSessionResolutionPhase
    {
        Ready = 0,
        NotPlaying = 1,
        FacadeUnavailable = 2,
        LogicSessionUnavailable = 3,
        WorldUnavailable = 4,
        ServicesUnavailable = 5,
        DiagnosticSessionUnavailable = 6,
    }

    internal readonly struct BattleDebugDiagnosticSessionResolution
    {
        public BattleDebugDiagnosticSessionResolution(
            BattleDebugDiagnosticSessionResolutionPhase phase,
            IBattleDiagnosticReadOnlySession session,
            MobaSkillCastRuntimeService skillRuntimeService,
            MobaBattleDiagnosticStateSampler stateSampler = null,
            MobaBattleDiagnosticEventCollector eventCollector = null)
        {
            Phase = phase;
            Session = session;
            SkillRuntimeService = skillRuntimeService;
            StateSampler = stateSampler;
            EventCollector = eventCollector;
        }

        public BattleDebugDiagnosticSessionResolutionPhase Phase { get; }
        public IBattleDiagnosticReadOnlySession Session { get; }
        public MobaSkillCastRuntimeService SkillRuntimeService { get; }
        public MobaBattleDiagnosticStateSampler StateSampler { get; }
        public MobaBattleDiagnosticEventCollector EventCollector { get; }
        public bool IsReady => Session != null;

        public string StatusMessage
        {
            get
            {
                switch (Phase)
                {
                    case BattleDebugDiagnosticSessionResolutionPhase.Ready:
                        return "Diagnostics 已连接。";
                    case BattleDebugDiagnosticSessionResolutionPhase.NotPlaying:
                        return "当前不在播放模式。";
                    case BattleDebugDiagnosticSessionResolutionPhase.FacadeUnavailable:
                        return "Battle Debug Facade 不可用。";
                    case BattleDebugDiagnosticSessionResolutionPhase.LogicSessionUnavailable:
                        return "没有活动中的 BattleLogicSession。";
                    case BattleDebugDiagnosticSessionResolutionPhase.WorldUnavailable:
                        return "BattleLogicSession 尚未提供 World。";
                    case BattleDebugDiagnosticSessionResolutionPhase.ServicesUnavailable:
                        return "当前 World 未提供服务解析器。";
                    default:
                        return "当前 World 未注册 IBattleDiagnosticReadOnlySession。";
                }
            }
        }
    }

    internal static class BattleDebugDiagnosticSessionResolver
    {
        public static bool TryResolve(in BattleDebugContext ctx, out IBattleDiagnosticReadOnlySession session)
        {
            session = ctx.DiagnosticSession;
            if (session != null) return true;

            var resolution = Resolve(ctx.Facade, isPlaying: true);
            session = resolution.Session;
            return resolution.IsReady;
        }

        public static BattleDebugDiagnosticSessionResolution Resolve(
            IBattleDebugFacade facade,
            bool isPlaying)
        {
            if (!isPlaying)
            {
                return new BattleDebugDiagnosticSessionResolution(
                    BattleDebugDiagnosticSessionResolutionPhase.NotPlaying,
                    null,
                    null);
            }

            if (facade == null)
            {
                return new BattleDebugDiagnosticSessionResolution(
                    BattleDebugDiagnosticSessionResolutionPhase.FacadeUnavailable,
                    null,
                    null);
            }

            if (!facade.TryGetSession(out var logicSession) || logicSession == null)
            {
                return new BattleDebugDiagnosticSessionResolution(
                    BattleDebugDiagnosticSessionResolutionPhase.LogicSessionUnavailable,
                    null,
                    null);
            }

            if (!logicSession.TryGetWorld(out var world) || world == null)
            {
                return new BattleDebugDiagnosticSessionResolution(
                    BattleDebugDiagnosticSessionResolutionPhase.WorldUnavailable,
                    null,
                    null);
            }

            var services = world.Services;
            if (services == null)
            {
                return new BattleDebugDiagnosticSessionResolution(
                    BattleDebugDiagnosticSessionResolutionPhase.ServicesUnavailable,
                    null,
                    null);
            }

            services.TryResolve(out IBattleDiagnosticReadOnlySession session);
            services.TryResolve(out MobaSkillCastRuntimeService skillRuntimeService);
            services.TryResolve(out MobaBattleDiagnosticStateSampler stateSampler);
            services.TryResolve(out MobaBattleDiagnosticEventCollector eventCollector);
            return new BattleDebugDiagnosticSessionResolution(
                session != null
                    ? BattleDebugDiagnosticSessionResolutionPhase.Ready
                    : BattleDebugDiagnosticSessionResolutionPhase.DiagnosticSessionUnavailable,
                session,
                skillRuntimeService,
                stateSampler,
                eventCollector);
        }
    }
}
