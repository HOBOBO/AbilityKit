//------------------------------------------------------------
//        File:  ExternalAction.cs
//       Brief:  ExternalAction
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-18
//============================================================

using System.Collections.Generic;
using System.Linq;
using BTCore.Runtime.Actions;

namespace BTCore.Runtime.Externals
{
    public class ExternalAction : Action, IExternalNode
    {
        public string TypeName { get; set; }

        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
        
        protected override void OnStart() {
            base.OnStart();
        }

        /// <summary>
        /// 外部Action节点需重写此方法
        /// </summary>
        /// <returns></returns>
        protected override NodeState OnUpdate() {
            return NodeState.Failure;
        }

        protected override void OnStop() {
            
        }

        public override string ToString() {
            var propertiesDump = Properties.Aggregate("Properties: ", (current, pair) => current + $"key: {pair.Key} value: {pair.Value}");
            return $"External condition typeName: {TypeName} {propertiesDump}";
        }
    }
}