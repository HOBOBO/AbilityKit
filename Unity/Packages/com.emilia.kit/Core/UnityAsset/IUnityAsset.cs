#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Emilia.Kit.Editor
{
    public interface IUnityAsset
    {
        /// <summary>
        /// 设置子资源
        /// </summary>
        void SetChildren(List<Object> childAssets);

        /// <summary>
        /// 获取子资源
        /// </summary>
        List<Object> GetChildren();
    }
}
#endif