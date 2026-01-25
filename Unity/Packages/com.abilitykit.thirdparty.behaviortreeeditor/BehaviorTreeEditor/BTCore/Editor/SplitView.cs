//------------------------------------------------------------
//        File:  SplitView.cs
//       Brief:  SplitView
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-02
//============================================================

using UnityEngine.UIElements;

namespace BTCore.Editor
{
    public class SplitView : TwoPaneSplitView
    {
        public new class UxmlFactory : UxmlFactory<SplitView, UxmlTraits> { }
    }
}
