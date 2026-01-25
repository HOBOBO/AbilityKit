//------------------------------------------------------------
//        File:  ParentNode.cs
//       Brief:  ParentNode
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2024-04-16
//============================================================

using System.Collections.Generic;

namespace BTCore.Runtime
{
    public abstract class ParentNode : BTNode
    {
        public virtual int Index { get; protected set; }
        public List<string> ChildrenGuids { get; set; } = new();
        
        protected List<BTNode> Children { get; } = new();

        public List<BTNode> GetChildren() => Children;
        
        public virtual void OnChildStart() { }
        public virtual void OnConditionalAbort(int index) { }

        public abstract void OnChildExecute(int childIndex, NodeState nodeState);

        public abstract bool CanExecute();

        public virtual bool CanRunParallel() { return false; }

        public virtual NodeState OverrideState(NodeState state) { return state; }

        public void AddChild(BTNode child) {
            Children.Add(child);
        }
    }
}