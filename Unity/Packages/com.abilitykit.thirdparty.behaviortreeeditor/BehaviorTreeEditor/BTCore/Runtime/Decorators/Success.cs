//------------------------------------------------------------
//        File:  Success.cs
//       Brief:  成功节点
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-01
//============================================================

namespace BTCore.Runtime.Decorators
{
    public class Success : Decorator
    {
        
        protected override void OnStop() {
        }

        public override void OnChildExecute(int childIndex, NodeState nodeState) {
            State = nodeState;
        }

        public override bool CanExecute() {
            return State is NodeState.Inactive or NodeState.Running;
        }

        public override NodeState Decorate(NodeState state) {
            return state == NodeState.Failure ? NodeState.Success : state;
        }
    }
}
