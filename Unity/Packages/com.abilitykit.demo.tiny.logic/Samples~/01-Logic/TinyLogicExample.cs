using System;

namespace AbilityKit.Demo.Tiny.Samples
{
    public static class TinyLogicExample
    {
        public static uint Run()
        {
            var battle = new TinyBattle();
            battle.AddPlayer(1, -1, 0);
            battle.AddPlayer(2, 1, 0);
            battle.Submit(1, new TinyInput(1, 0, true));
            battle.Tick();

            var checkpoint = TinyBattleStateCodec.Encode(battle.CaptureState());
            var restored = new TinyBattle();
            restored.RestoreState(TinyBattleStateCodec.Decode(checkpoint));
            for (var frame = 0; frame < 40; frame++)
            {
                var input = new TinyInput((sbyte)(frame % 3 - 1), 0, frame % 7 == 0);
                battle.Submit(2, input);
                restored.Submit(2, input);
                battle.Tick();
                restored.Tick();
                if (battle.ComputeHash() != restored.ComputeHash())
                    throw new InvalidOperationException("Tiny state diverged at frame " + battle.Frame);
            }

            return restored.ComputeHash();
        }
    }
}
