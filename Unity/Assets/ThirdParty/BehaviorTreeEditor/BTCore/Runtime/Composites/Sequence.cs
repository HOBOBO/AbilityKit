//------------------------------------------------------------
//        File:  Sequence.cs
//       Brief:  顺序节点: 从左到右挨个顺序执行，当所有子节点都返回Success时，它才返回Success。
//              当某个子节点返回Failure时，顺序节点就会立刻返回Failure
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-09-29
//============================================================

using System;

namespace BTCore.Runtime.Composites
{
    public class Sequence : Composite
    {
        protected override void OnStart() {
            base.OnStart();
            Index = 0;
        }

        protected override void OnStop() {
            
        }

        public override void OnChildExecute(int childIndex, NodeState nodeState) {
            Index++;
            State = nodeState switch {
                NodeState.Success => Index >= Children.Count ? NodeState.Success : NodeState.Running,
                _ => nodeState
            };
        }

        public override bool CanExecute() {
            return Index < Children.Count && State != NodeState.Failure;
        }

        public override void OnConditionalAbort(int index) {
            Index = index;
            State = NodeState.Running;
        }
    }
}
