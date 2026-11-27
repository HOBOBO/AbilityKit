using System.Collections;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Actions
{

    /// <summary>
    /// A behavior node that owns an ordered list of child behaviors.
    /// </summary>
    public interface ICompositeAction : IAction
    {
        void AddChild(IAction child);
    }
}
