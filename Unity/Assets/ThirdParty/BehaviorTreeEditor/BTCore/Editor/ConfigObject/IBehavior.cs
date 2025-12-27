using BTCore.Runtime;
using UnityEngine;

namespace BTCore.Editor
{
    public interface IBehavior
    {
        int instanceID { get; }

        Object GetObject(bool local = false);
        
        BTree GetSource(bool local = false);
    }
}