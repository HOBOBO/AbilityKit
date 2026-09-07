using System;


namespace AbilityKit.HFSM.Actions
{

    /// <summary>
    /// 条件执行：如果条件满足则执行 thenAction，否则执行 elseAction（可选）
    /// </summary>
    [System.Serializable]
    public class IfAction : ActionBase, IDecoratorAction
    {
        public Func<bool> condition;
        public IAction thenAction;
        public IAction elseAction;

        private bool evaluated;

        public IfAction() { }

        public IfAction(Func<bool> condition, IAction thenAction, IAction elseAction = null)
        {
            this.condition = condition;
            this.thenAction = thenAction;
            this.elseAction = elseAction;
        }

        public void SetChild(IAction child) => thenAction = child;

        public override void Reset()
        {
            base.Reset();
            evaluated = false;
            thenAction?.Reset();
            elseAction?.Reset();
        }

        public override void ForceEnd()
        {
            base.ForceEnd();
            thenAction?.ForceEnd();
            elseAction?.ForceEnd();
        }

        public override BehaviorStatus Execute(BehaviorContext context)
        {
            if (forceEnded)
                return BehaviorStatus.Failure;

            isActive = true;

            if (!evaluated)
            {
                evaluated = true;
                if (condition != null && condition())
                {
                    thenAction?.Reset();
                }
                else
                {
                    elseAction?.Reset();
                }
            }

            // 执行选定的分支
            if (condition != null && condition())
            {
                if (thenAction != null)
                {
                    var status = thenAction.Execute(context);
                    if (status != BehaviorStatus.Running)
                    {
                        evaluated = false;
                    }
                    return status;
                }
            }
            else
            {
                if (elseAction != null)
                {
                    var status = elseAction.Execute(context);
                    if (status != BehaviorStatus.Running)
                    {
                        evaluated = false;
                    }
                    return status;
                }
            }

            evaluated = false;
            return BehaviorStatus.Success;
        }
    }
}
