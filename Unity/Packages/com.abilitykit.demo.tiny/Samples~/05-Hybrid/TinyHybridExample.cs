using System;
using AbilityKit.Demo.Tiny.FrameSync;

namespace AbilityKit.Demo.Tiny.Samples
{
    public static class TinyHybridExample
    {
        public static uint Run()
        {
            var localBattle = NewBattle();
            var session = new TinyFrameSyncSession(localBattle);
            var localAttack = new TinyFrameInput(1, new TinyInput(1, 0, true));
            session.Predict(new[] { localAttack });
            if (localBattle.CaptureState().Actors[1].Hp != 90)
                throw new InvalidOperationException("Local attack was not predicted.");

            var authoritativeBattle = NewBattle();
            authoritativeBattle.Submit(localAttack.PlayerId, localAttack.Input);
            authoritativeBattle.Tick();
            if (session.ApplyAuthoritative(1, new[] { localAttack },
                    authoritativeBattle.ComputeHash()) != TinyReconcileResult.Matched)
                throw new InvalidOperationException("Local prediction was not confirmed.");

            session.Predict(Array.Empty<TinyFrameInput>());
            var remoteAttack = new TinyFrameInput(2, new TinyInput(0, 0, true));
            authoritativeBattle.Submit(remoteAttack.PlayerId, remoteAttack.Input);
            authoritativeBattle.Tick();
            if (session.ApplyAuthoritative(2, new[] { remoteAttack },
                    authoritativeBattle.ComputeHash()) != TinyReconcileResult.Replayed ||
                session.StateHash != authoritativeBattle.ComputeHash())
                throw new InvalidOperationException("Remote input did not reconcile.");
            return session.StateHash;
        }

        private static TinyBattle NewBattle()
        {
            var battle = new TinyBattle();
            battle.AddPlayer(1, -1, 0);
            battle.AddPlayer(2, 1, 0);
            return battle;
        }
    }
}
