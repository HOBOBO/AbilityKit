namespace AbilityKit.Ability.Share.Battle.SearchTarget
{
    public interface IVisitedSet
    {
        void Next();

        bool Mark(int actorId);

        bool IsMarked(int actorId);
    }
}
