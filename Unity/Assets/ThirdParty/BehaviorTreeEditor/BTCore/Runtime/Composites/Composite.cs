//------------------------------------------------------------
//        File:  Composite.cs
//       Brief:  复合节点基类
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-09-29
//============================================================

using System;
using System.Collections.Generic;

namespace BTCore.Runtime.Composites
{
    public abstract class Composite : ParentNode
    {
        public AbortType AbortType = AbortType.None;
    }
}
