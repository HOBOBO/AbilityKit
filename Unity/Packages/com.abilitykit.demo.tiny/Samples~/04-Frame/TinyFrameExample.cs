using System;
using AbilityKit.Demo.Tiny.FrameSync;

namespace AbilityKit.Demo.Tiny.Samples
{
    public static class TinyFrameExample
    {
        public static uint Run()
        {
            var predictedBattle = NewBattle();
            var session = new TinyFrameSyncSession(predictedBattle);
            session.Predict(Array.Empty<TinyFrameInput>());

            var authoritativeBattle = NewBattle();
            var attack = new TinyFrameInput(1, new TinyInput(1, 0, true));
            authoritativeBattle.Submit(attack.PlayerId, attack.Input);
            authoritativeBattle.Tick();

            var result = session.ApplyAuthoritative(1,
                new[] { attack }, authoritativeBattle.ComputeHash());
            if (result != TinyReconcileResult.Replayed ||
                session.StateHash != authoritativeBattle.ComputeHash())
                throw new InvalidOperationException("Late authoritative input did not replay.");
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
