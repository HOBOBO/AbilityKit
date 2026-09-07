namespace AbilityKit.BehaviorTree.Nodes
{
    using AbilityKit.BehaviorTree.Definition;
    using AbilityKit.BehaviorTree.Execution;

    public abstract class CompositeNode : ParentNodeBase
    {
        public const string AbortTypeProperty = "abortType";

        public AbortType AbortType { get; protected set; }

        protected sealed override void OnInitParent(in NodeInitContext context)
        {
            var raw = context.Properties.GetInt64(AbortTypeProperty, (long)AbortType.None);
            if (raw is < 0 or > (long)AbortType.Both)
                throw new System.InvalidOperationException(
                    $"BT node '{context.Definition.Id}' has invalid abortType value {raw}.");
            AbortType = (AbortType)raw;
            OnCompositeInit(context);
        }

        protected virtual void OnCompositeInit(in NodeInitContext context) { }

    }
}
