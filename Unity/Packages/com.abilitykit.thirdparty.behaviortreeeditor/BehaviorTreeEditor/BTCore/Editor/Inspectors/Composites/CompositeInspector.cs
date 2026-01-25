//------------------------------------------------------------
//        File:  CompositeInspector.cs
//       Brief:  CompositeInspector
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2024-06-19
//============================================================

using BTCore.Runtime;
using Sirenix.OdinInspector;

namespace BTCore.Editor.Inspectors.Composites
{
    public abstract class CompositeInspector<T> : BTNodeInspector<T> where T : BTNode
    {
        [ShowInInspector]
        [LabelText("中断类型")]
        [LabelWidth(100)]
        [OnValueChanged("OnFieldValueChanged")]
        protected AbortType AbortType = AbortType.None;
    }
}