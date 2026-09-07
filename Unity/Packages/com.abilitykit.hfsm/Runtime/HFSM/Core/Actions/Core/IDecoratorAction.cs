using System.Collections;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Actions
{

    /// <summary>
    /// A behavior node that wraps exactly one child behavior.
    /// </summary>
    public interface IDecoratorAction : IAction
    {
        void SetChild(IAction child);
    }
}
