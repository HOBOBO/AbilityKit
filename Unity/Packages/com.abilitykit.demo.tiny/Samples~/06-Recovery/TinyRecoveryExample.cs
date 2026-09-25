using System;
using AbilityKit.Demo.Tiny.FrameSync;

namespace AbilityKit.Demo.Tiny.Samples
{
    public static class TinyRecoveryExample
    {
        public static uint Run()
        {
            var localBattle = NewBattle();
            var session = new TinyFrameSyncSession(localBattle, historyCapacity: 2);
            for (var frame = 0; frame < 3; frame++)
                session.Predict(Array.Empty<TinyFrameInput>());

            var authoritativeBattle = NewBattle();
            var attack = new TinyFrameInput(1, new TinyInput(1, 0, true));
            authoritativeBattle.Submit(attack.PlayerId, attack.Input);
            authoritativeBattle.Tick();
            if (session.ApplyAuthoritative(1, new[] { attack },
                    authoritativeBattle.ComputeHash()) != TinyReconcileResult.NeedsFullSnapshot ||
                !session.RequiresFullSnapshot)
                throw new InvalidOperationException("Lost history did not request recovery.");

            authoritativeBattle.Tick();
            authoritativeBattle.Tick();
            session.RestoreAuthoritativeFullState(authoritativeBattle.CaptureState());
            if (session.RequiresFullSnapshot || session.StateHash != authoritativeBattle.ComputeHash())
                throw new InvalidOperationException("Full snapshot did not restore the baseline.");
            session.Predict(Array.Empty<TinyFrameInput>());
            authoritativeBattle.Tick();
            if (session.StateHash != authoritativeBattle.ComputeHash())
                throw new InvalidOperationException("Battle diverged after recovery.");
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
