using System;
using System.Collections.Generic;

namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 游戏标签静态工具类，提供便捷的标签访问方法。
    /// </summary>
    public static class GameplayTags
    {
        /// <summary>
        /// 请求或获取一个标签
        /// </summary>
        public static GameplayTag Tag(string name)
        {
            return GameplayTagManager.Instance.RequestTag(name);
        }

        /// <summary>
        /// 尝试获取已注册的标签
        /// </summary>
        public static bool TryGet(string name, out GameplayTag tag)
        {
            return GameplayTagManager.Instance.TryGetTag(name, out tag);
        }

        /// <summary>
        /// 获取标签名称
        /// </summary>
        public static string GetName(GameplayTag tag)
        {
            return GameplayTagManager.Instance.GetName(tag);
        }

        /// <summary>
        /// 创建空容器
        /// </summary>
        public static GameplayTagContainer EmptyContainer()
        {
            return new GameplayTagContainer();
        }

        /// <summary>
        /// 从标签创建容器
        /// </summary>
        public static GameplayTagContainer MakeContainer(params GameplayTag[] tags)
        {
            var container = new GameplayTagContainer();
            foreach (var tag in tags)
            {
                container.Add(tag);
            }
            return container;
        }

        /// <summary>
        /// 创建标签集合资产对应的容器
        /// </summary>
        public static GameplayTagContainer FromCollection(GameplayTagCollection collection)
        {
            if (collection == null) return new GameplayTagContainer();
            return collection.ToContainer();
        }
    }
}
