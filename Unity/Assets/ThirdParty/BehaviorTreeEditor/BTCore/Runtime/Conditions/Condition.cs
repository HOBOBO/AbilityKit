//------------------------------------------------------------
//        File:  Condition.cs
//       Brief:  条件节点基类
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-01
//============================================================

namespace BTCore.Runtime.Conditions
{
    public abstract class Condition : BTNode
    {
        protected abstract bool Validate();

        protected override NodeState OnUpdate() {
            return Validate() ? NodeState.Success : NodeState.Failure;
        }
    }
}
