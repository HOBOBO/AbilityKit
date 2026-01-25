//------------------------------------------------------------
//        File:  Parallel.cs
//       Brief:  并行节点：并行执行所有的子节点，所有的子节点都返回Success，并行节点才会返回Success。
//               只要有一个子节点返回Failure，并行节点就会立刻返回Failure
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-09-30
//============================================================

using System;
using System.Collections.Generic;

namespace BTCore.Runtime.Composites
{
    public class Parallel : Composite
    {
        private List<NodeState> _executionState = new();

        protected override void OnStart() {
            base.OnStart();
            
            Index = 0;
            _executionState = Children.ConvertAll(x => NodeState.Inactive);
        }

        protected override void OnStop() {

        }

        public override void OnChildStart() {
            _executionState[Index++] = NodeState.Running;
        }

        public override void OnChildExecute(int childIndex, NodeState nodeState) {
            _executionState[childIndex] = nodeState;
        }

        public override bool CanExecute() {
            return Index < Children.Count;
        }

        public override void OnConditionalAbort(int index) {
            Index = 0;
            _executionState.ForEach(x => x = NodeState.Inactive);
        }

        /// <summary>
        /// 并行节点状态是由多个子节点状态决定的
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public override NodeState OverrideState(NodeState state) {
            var childRenComplete = true;
            for (var i = 0; i < _executionState.Count; i++) {
                if (_executionState[i] == NodeState.Running) {
                    childRenComplete = false;
                }
                else if (_executionState[i] == NodeState.Failure) {
                    return State = NodeState.Failure;
                }
            }

            return State = childRenComplete ? NodeState.Success : NodeState.Running;
        }

        public override bool CanRunParallel() {
            return true;
        }
    }
}
