//------------------------------------------------------------
//        File:  MyAction.cs
//       Brief:  MyAction
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-18
//============================================================

using BTCore.Runtime;
using BTCore.Runtime.Externals;

namespace Examples.ExternalNode
{
    /// <summary>
    /// 测试类型，实际项目中需要替换为外部工程中的Action类型
    /// </summary>
    public class MyAction : ExternalAction
    {
        private string _name;
        
        protected override void OnStart() {
            base.OnStart();
            _name = Properties["Name"];
        }

        protected override NodeState OnUpdate() {
            BTLogger.Debug(_name);
            return NodeState.Success;
        }
    }
}
