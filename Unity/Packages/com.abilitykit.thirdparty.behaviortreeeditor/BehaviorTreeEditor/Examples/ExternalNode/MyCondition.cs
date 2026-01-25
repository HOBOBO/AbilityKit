//------------------------------------------------------------
//        File:  MyCondition.cs
//       Brief:  MyCondition
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-18
//============================================================

using System;
using BTCore.Runtime.Externals;

namespace Examples.ExternalNode
{
    /// <summary>
    /// 测试类型，实际项目中需要替换为外部工程中的Condition类型
    /// </summary>
    public class MyCondition : ExternalCondition
    {
        private bool _canPass;
        
        protected override void OnStart() {
            // 读取节点配置数据
            _canPass = Convert.ToBoolean(Properties["CanPass"]);
        }

        protected override bool Validate() {
            return _canPass;
        }
    }
}
