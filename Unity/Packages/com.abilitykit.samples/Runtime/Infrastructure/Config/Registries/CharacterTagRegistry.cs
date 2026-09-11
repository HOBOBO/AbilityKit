#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Core.Markers;
using AbilityKit.Samples.Logic.Infrastructure.Config.Attributes;

namespace AbilityKit.Samples.Logic.Infrastructure.Config
{
    /// <summary>
    /// 瑙掕壊鏍囩娉ㄥ唽琛?
    /// 閫氳繃 CharacterTagAttribute 鑷姩鍙戠幇鍜屾敞鍐岃鑹叉爣绛?
    /// 一个角色可以有多个标签
    /// </summary>
    public sealed class CharacterTagRegistry : IMarkerRegistry
    {
        private static readonly Lazy<CharacterTagRegistry> _instance = new(() => new CharacterTagRegistry());
        public static CharacterTagRegistry Instance => _instance.Value;

        private readonly Dictionary<string, List<Type>> _tagToTypes = new();
        private readonly List<Type> _allTypes = new();

        private CharacterTagRegistry()
        {
            ScanCurrentAssembly();
        }

        private void ScanCurrentAssembly()
        {
            var assembly = typeof(CharacterTagRegistry).Assembly;
            MarkerScanner<CharacterTagAttribute>.Scan(new[] { assembly }, this);
        }

        internal void RegisterByAttribute(CharacterTagAttribute attr, Type implType)
        {
            if (attr == null || implType == null) return;

            if (!_tagToTypes.ContainsKey(attr.Tag))
                _tagToTypes[attr.Tag] = new List<Type>();

            if (!_tagToTypes[attr.Tag].Contains(implType))
                _tagToTypes[attr.Tag].Add(implType);

            if (!_allTypes.Contains(implType))
                _allTypes.Add(implType);
        }

        /// <summary>
        /// 鏍规嵁鏍囩鏌ユ壘鎵€鏈夊叿鏈夎鏍囩鐨勮鑹茬被鍨?
        /// </summary>
        public IEnumerable<Type> GetTypesByTag(string tag)
        {
            return _tagToTypes.TryGetValue(tag, out var types) ? types : Enumerable.Empty<Type>();
        }

        /// <summary>
        /// 妫€鏌ユ煇涓被鍨嬫槸鍚﹀叿鏈夋寚瀹氭爣绛?
        /// </summary>
        public bool HasTag(Type type, string tag)
        {
            return _tagToTypes.TryGetValue(tag, out var types) && types.Contains(type);
        }

        /// <summary>
        /// 鑾峰彇鎵€鏈夊凡娉ㄥ唽鐨勬爣绛?
        /// </summary>
        public IEnumerable<string> AllTags => _tagToTypes.Keys;

        #region IMarkerRegistry 瀹炵幇

        public int Count => _allTypes.Count;
        public IReadOnlyList<Type> Types => _allTypes;

        public void Register(Type implType)
        {
            if (implType == null) return;
            if (implType.IsAbstract) return;
            if (implType.IsInterface) return;
            if (!_allTypes.Contains(implType))
                _allTypes.Add(implType);
        }

        public void ForEach(Action<Type> action)
        {
            foreach (var type in _allTypes)
                action(type);
        }

        public IEnumerable<Type> Where(Func<Type, bool> predicate)
        {
            foreach (var type in _allTypes)
                if (predicate(type))
                    yield return type;
        }

        public Type? Find(Func<Type, bool> predicate)
        {
            foreach (var type in _allTypes)
                if (predicate(type))
                    return type;
            return null;
        }

        #endregion
    }
}
