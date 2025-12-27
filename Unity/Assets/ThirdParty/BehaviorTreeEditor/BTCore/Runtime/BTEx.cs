//------------------------------------------------------------
//        File:  BTEx.cs
//       Brief:  BTEx
//
//      Author:  Saroce, Saroce233@163.com
//
//    Modified:  2023-10-05
//============================================================

using System.Collections.Generic;
using BTCore.Runtime.Composites;
using BTCore.Runtime.Decorators;

namespace BTCore.Runtime
{
    public static class BTEx
    {
        public static List<string> GetChildrenGuids(this BTNode parent) {
            var children = new List<string>();
            switch (parent) {
                case ParentNode parentNode: {
                    children.AddRange(parentNode.ChildrenGuids);
                    break;
                }
                case EntryNode entryNode: {
                    if (!string.IsNullOrEmpty(entryNode.ChildGuid)) {
                        children.Add(entryNode.ChildGuid);   
                    }
                    break;
                }
            }

            return children;
        }
    }
}
