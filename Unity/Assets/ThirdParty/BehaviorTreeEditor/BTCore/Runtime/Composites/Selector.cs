//------------------------------------------------------------
//        File:  Selector.cs
//       Brief:  选择节点：从左到右执行，当有子节点返回Success，选择节点就会立刻返回Success，
//               不会执行下一个子节点，所有子节点都返回Failure，选择节点返回Failure
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-09-30
//============================================================

using System;

namespace BTCore.Runtime.Composites
{
    public class Selector : Composite
    {
        protected override void OnStart() {
            base.OnStart();
            Index = 0;
        }

        protected override void OnStop() {
            
        }

        public override void OnChildExecute(int childIndex, NodeState nodeState) {
            State = nodeState switch {
                NodeState.Failure => ++Index >= Children.Count ? NodeState.Failure : NodeState.Running,
                _ => nodeState
            };
        }

        public override bool CanExecute() {
            return Index < Children.Count && State != NodeState.Success;
        }
        
        public override void OnConditionalAbort(int index) {
            Index = index;
            State = NodeState.Running;
        }
    }
}
