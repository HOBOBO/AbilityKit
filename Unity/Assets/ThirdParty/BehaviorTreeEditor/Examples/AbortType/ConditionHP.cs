//------------------------------------------------------------
//        File:  ConditionHP.cs
//       Brief:  ConditionHP
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2024-06-21
//============================================================

using BTCore.Runtime.Conditions;

namespace Examples.AbortType
{
    public class ConditionHP : Condition
    {
        protected override bool Validate() {
            var haveHP = Blackboard.GetValue<int>("HP");
            return haveHP > 100;
        }
    }
}