namespace AbilityKit.Ability.Battle.EntityManager
{
    public static class BattleEntityManagerExample
    {
        private enum ExampleFaction
        {
            Neutral = 0,
            A = 1,
            B = 2
        }

        private enum ExampleKind
        {
            Unknown = 0,
            Hero = 1,
            Monster = 2
        }

        public static void Run()
        {
            var mgr = new BattleEntityManager<int>();

            var byFaction = mgr.CreateKeyedIndex<ExampleFaction>();
            var byKind = mgr.CreateKeyedIndex<ExampleKind>();
            var byTag = mgr.CreateMultiKeyIndex<int>();

            var e1 = 1;
            var e2 = 2;
            var e3 = 3;

            mgr.Add(e1);
            mgr.Add(e2);
            mgr.Add(e3);

            byFaction.SetKey(e1, ExampleFaction.A);
            byFaction.SetKey(e2, ExampleFaction.B);
            byFaction.SetKey(e3, ExampleFaction.A);

            byKind.SetKey(e1, ExampleKind.Hero);
            byKind.SetKey(e2, ExampleKind.Monster);
            byKind.SetKey(e3, ExampleKind.Monster);

            byTag.AddKey(e1, 100);
            byTag.AddKey(e1, 200);
            byTag.AddKey(e2, 100);

            var factionA = byFaction.Get(ExampleFaction.A);
            var monsters = byKind.Get(ExampleKind.Monster);
            var tag100 = byTag.Get(100);

            _ = factionA;
            _ = monsters;
            _ = tag100;

            mgr.Remove(e2);
        }
    }
}
