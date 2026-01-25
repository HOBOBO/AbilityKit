//------------------------------------------------------------
//        File:  Failure.cs
//       Brief:  失败节点
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-01
//============================================================

namespace BTCore.Runtime.Decorators
{
    public class Failure : Decorator
    {
        protected override void OnStop() {
            base.OnStop();
        }

        public override bool CanExecute() {
            return State is NodeState.Inactive or NodeState.Running;
        }

        public override void OnChildExecute(int childIndex, NodeState nodeState) {
            State = nodeState;
        }

        public override NodeState Decorate(NodeState state) {
            return state == NodeState.Success ? NodeState.Failure : state;
        }
    }
}
