namespace AbilityKit.Game.Battle.Entity
{
    public readonly struct BattleNetId
    {
        public readonly int Value;

        public BattleNetId(int value)
        {
            Value = value;
        }

        public override string ToString() => Value.ToString();
    }
}
