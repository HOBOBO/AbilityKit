//------------------------------------------------------------
//        File:  IsKeyDown.cs
//       Brief:  IsKeyDown
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2024-06-25
//============================================================

using BTCore.Runtime.Conditions;
using UnityEngine;

namespace BTCore.Runtime.Unity.Conditions
{
    public class IsKeyDown : Condition
    {
        public KeyCode KeyCode = KeyCode.None;
        
        protected override bool Validate() {
            return Input.GetKeyDown(KeyCode);
        }
    }
}