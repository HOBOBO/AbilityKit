//------------------------------------------------------------
//        File:  ConditionMP.cs
//       Brief:  ConditionMP
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2024-06-21
//============================================================

using BTCore.Runtime.Conditions;

namespace Examples.AbortType
{
    public class ConditionMP : Condition
    {
        protected override bool Validate() {
            var haveMP = Blackboard.GetValue<int>("MP");
            return haveMP > 100;
        }
    }
}