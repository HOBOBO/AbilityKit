using AbilityKit.Demo.Tiny;
using AbilityKit.Demo.Tiny.Turn;

RunTurns();
RunArena();
Console.WriteLine("Tiny battle styles passed: ordered turns and fixed-tick arena");

static void RunTurns()
{
    var battle = new TinyTurnBattle();
    var before = battle.ComputeHash();
    Require(!battle.Submit(2, new byte[] { 1 }), "Out-of-turn action accepted.");
    Require(!battle.Submit(1, new byte[] { 2 }), "Invalid turn action accepted.");
    battle.Tick();
    Require(battle.Turn == 0 && battle.ComputeHash() != before,
        "An idle network frame must advance without advancing the turn.");
    Require(battle.Submit(1, new byte[] { 1 }), "First turn failed.");
    battle.Tick();
    Require(battle.CurrentPlayerId == 2 && battle.PlayerTwoHp == 1,
        "First turn did not apply one action.");

    var restored = new TinyTurnBattle();
    restored.RestoreState(TinyTurnStateCodec.Decode(
        TinyTurnStateCodec.Encode(battle.CaptureState())));
    Require(restored.ComputeHash() == battle.ComputeHash(),
        "Turn checkpoint did not restore the same state.");
    Require(battle.Submit(2, new byte[] { 1 }) &&
        restored.Submit(2, new byte[] { 1 }), "Second turn failed.");
    battle.Tick();
    restored.Tick();
    Require(battle.ComputeHash() == restored.ComputeHash() &&
        battle.CurrentPlayerId == 1 && battle.PlayerOneHp == 1,
        "Turn checkpoint replay diverged.");
    Require(battle.Submit(1, new byte[] { 1 }), "Winning turn failed.");
    battle.Tick();
    Require(battle.Turn == 3 && battle.WinnerId == 1 &&
        !battle.Submit(1, new byte[] { 1 }), "Finished turn battle accepted an action.");
    Console.WriteLine($"Turn battle: turns={battle.Turn}, winner={battle.WinnerId}, hash={battle.ComputeHash()}");
}

static void RunArena()
{
    var battle = NewArena();
    TinyBattle? restored = null;
    for (var frame = 1; frame <= 40; frame++)
    {
        Step(battle, frame);
        if (frame == 20)
        {
            restored = NewArena();
            restored.RestoreState(TinyBattleStateCodec.Decode(
                TinyBattleStateCodec.Encode(battle.CaptureState())));
        }
        else if (frame > 20)
        {
            Step(restored!, frame);
            Require(battle.ComputeHash() == restored!.ComputeHash(),
                $"Arena checkpoint diverged at frame {frame}.");
        }
    }
    Require(battle.CaptureState().Actors.All(actor => actor.Hp == 80),
        "Arena did not apply both attacks and cooldown recovery.");
    Console.WriteLine($"Arena battle: frames={battle.Frame}, hash={battle.ComputeHash()}");

    static TinyBattle NewArena()
    {
        var arena = new TinyBattle();
        arena.AddPlayer(1, -3, 0);
        arena.AddPlayer(2, 3, 0);
        return arena;
    }

    static void Step(TinyBattle arena, int frame)
    {
        var advance = frame <= 2;
        var attack = frame is 2 or 32;
        arena.Submit(1, new TinyInput((sbyte)(advance ? 1 : 0), 0, attack));
        arena.Submit(2, new TinyInput((sbyte)(advance ? -1 : 0), 0, attack));
        arena.Tick();
    }
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
