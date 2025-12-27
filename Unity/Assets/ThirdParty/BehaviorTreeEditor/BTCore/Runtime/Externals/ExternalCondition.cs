//------------------------------------------------------------
//        File:  ExternalCondition.cs
//       Brief:  ExternalCondition
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-18
//============================================================

using System.Collections.Generic;
using System.Linq;
using BTCore.Runtime.Conditions;

namespace BTCore.Runtime.Externals
{
    public class ExternalCondition : Condition, IExternalNode
    {
        public string TypeName { get; set; }

        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
        
        protected override void OnStart() {
            base.OnStart();
        }

        protected override void OnStop() {
            
        }

        /// <summary>
        /// 外部Condition节点需重写此方法
        /// </summary>
        /// <returns></returns>
        protected override bool Validate() {
            return false;
        }

        public override string ToString() {
            var propertiesDump = Properties.Aggregate("Properties: ", (current, pair) => current + $"key: {pair.Key} value: {pair.Value}");
            return $"External condition typeName: {TypeName} {propertiesDump}";
        }
    }
}