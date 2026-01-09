namespace AbilityKit.Ability.Share.Battle.SearchTarget
{
    public interface ISearchStats
    {
        void Reset();
        void OnCandidate();
        void OnHit();
        void OnResult(int count);
    }
}
