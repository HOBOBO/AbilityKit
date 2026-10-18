namespace AbilityKit.Game.Flow
{
    public interface IBattleEntityRuntime
    {
        bool TryGetWorld<TWorld>(out TWorld world);
        bool TryCreateNode<TNode>(string debugName, out TNode node);
        void DestroyTree<TNode>(TNode root);
    }
}
