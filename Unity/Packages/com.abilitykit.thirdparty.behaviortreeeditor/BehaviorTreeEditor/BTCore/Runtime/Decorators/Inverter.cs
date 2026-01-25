//------------------------------------------------------------
//        File:  Inverter.cs
//       Brief:  反转节点
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-01
//============================================================

using System;

namespace BTCore.Runtime.Decorators
{
    public class Inverter : Decorator
    {
        protected override void OnStart() {
            base.OnStart();
        }

        protected override void OnStop() {
        }

        public override void OnChildExecute(int childIndex, NodeState nodeState) {
            State = nodeState;
        }

        public override bool CanExecute() {
            return State is NodeState.Inactive or NodeState.Running;
        }

        public override NodeState Decorate(NodeState state) {
            return state switch {
                NodeState.Failure => NodeState.Success,
                NodeState.Success => NodeState.Failure,
                _ => state
            };
        }
    }
}
