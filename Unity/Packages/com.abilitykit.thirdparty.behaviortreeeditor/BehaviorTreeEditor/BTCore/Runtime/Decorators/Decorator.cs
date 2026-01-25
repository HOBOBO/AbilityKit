//------------------------------------------------------------
//        File:  Decorator.cs
//       Brief:  装饰节点基类
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-01
//============================================================

using System;

namespace BTCore.Runtime.Decorators
{
    public abstract class Decorator : ParentNode
    {
        public virtual NodeState Decorate(NodeState state) {
            return state;
        }
    }
}

