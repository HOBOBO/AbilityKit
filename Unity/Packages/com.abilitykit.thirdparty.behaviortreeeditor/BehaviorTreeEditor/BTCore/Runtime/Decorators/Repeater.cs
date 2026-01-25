//------------------------------------------------------------
//        File:  Repeater.cs
//       Brief:  重复节点
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-01
//============================================================

namespace BTCore.Runtime.Decorators
{
    public class Repeater : Decorator
    {
        public int RepeatCount { get; set; } = 1; // 设定为负数，一直循环执行
        
        private int _counter = 0;
        
        protected override void OnStart() {
            base.OnStart();
            _counter = 0;
        }

        protected override void OnStop() {
            
        }

        public override void OnChildExecute(int childIndex, NodeState nodeState) {
            // 一直运行状态
            if (RepeatCount == -1) {
                State = NodeState.Running;
                return;
            }

            if (_counter >= RepeatCount) {
                State = NodeState.Success;
            }

            _counter++;
            State = nodeState != NodeState.Success ? nodeState : NodeState.Running;
        }

        public override bool CanExecute() {
            return RepeatCount == -1 || _counter < RepeatCount;
        }
    }
}
