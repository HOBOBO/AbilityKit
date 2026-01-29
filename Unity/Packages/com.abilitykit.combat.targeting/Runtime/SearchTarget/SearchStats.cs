namespace AbilityKit.Ability.Share.Battle.SearchTarget
{
    public sealed class SearchStats : ISearchStats
    {
        public int Candidates { get; private set; }
        public int Hits { get; private set; }
        public int Results { get; private set; }

        public void Reset()
        {
            Candidates = 0;
            Hits = 0;
            Results = 0;
        }

        public void OnCandidate() => Candidates++;
        public void OnHit() => Hits++;
        public void OnResult(int count) => Results = count;
    }
}
