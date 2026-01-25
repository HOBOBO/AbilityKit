//------------------------------------------------------------
//        File:  UntilSuccess.cs
//       Brief:  UntilSuccess
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-25
//============================================================

namespace BTCore.Runtime.Decorators
{
    public class UntilSuccess : Decorator
    {
        protected override void OnStop() {
            
        }

        public override void OnChildExecute(int childIndex, NodeState nodeState) {
            State = nodeState;
        }

        public override bool CanExecute() {
            return State != NodeState.Success;
        }
    }
}
