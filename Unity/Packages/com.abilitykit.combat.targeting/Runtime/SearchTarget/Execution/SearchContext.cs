using System;
using System.Collections.Generic;
using System.Threading;

namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 搜索上下文中的强类型数据键。键按实例身份区分，名称仅用于诊断。
    /// </summary>
    public sealed class SearchContextKey<T>
    {
        public string Name { get; }

        public SearchContextKey(string name = null)
        {
            Name = name ?? typeof(T).Name;
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// 搜索上下文。框架能力使用显式属性，包外扩展数据使用强类型键。
    /// </summary>
    public sealed class SearchContext : IDisposable
    {
        private readonly Dictionary<object, object> _typedData = new Dictionary<object, object>();
        private bool _poolOwned;
        private int _poolLeaseState;
        private IPositionProvider _positionProvider;
        private IEntityKeyProvider _entityKeyProvider;
        private ISearchStats _searchStats;

        public IPositionProvider PositionProvider
        {
            get
            {
                ThrowIfReleased();
                return _positionProvider;
            }
            set
            {
                ThrowIfReleased();
                _positionProvider = value;
            }
        }

        public IEntityKeyProvider EntityKeyProvider
        {
            get
            {
                ThrowIfReleased();
                return _entityKeyProvider;
            }
            set
            {
                ThrowIfReleased();
                _entityKeyProvider = value;
            }
        }

        public ISearchStats SearchStats
        {
            get
            {
                ThrowIfReleased();
                return _searchStats;
            }
            set
            {
                ThrowIfReleased();
                _searchStats = value;
            }
        }

        public void SetData<T>(SearchContextKey<T> key, T value)
        {
            ThrowIfReleased();
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (ReferenceEquals(value, null))
            {
                _typedData.Remove(key);
                return;
            }
            _typedData[key] = value;
        }

        public bool TryGetData<T>(SearchContextKey<T> key, out T value)
        {
            ThrowIfReleased();
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (_typedData.TryGetValue(key, out var stored) && stored is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }

        public void ClearData()
        {
            ThrowIfReleased();
            _typedData.Clear();
        }

        public void Clear()
        {
            ThrowIfReleased();
            ClearCore();
        }

        public void Dispose()
        {
            if (_poolOwned)
            {
                TargetingPool.Release(this);
                return;
            }

            ClearCore();
        }

        internal void ResetForRent()
        {
            _poolOwned = true;
            Volatile.Write(ref _poolLeaseState, 1);
            ClearCore();
        }

        internal bool TryBeginPoolRelease()
        {
            return _poolOwned && Interlocked.Exchange(ref _poolLeaseState, 0) == 1;
        }

        internal void ResetForRelease()
        {
            ClearCore();
        }

        private void ThrowIfReleased()
        {
            if (_poolOwned && Volatile.Read(ref _poolLeaseState) != 1)
            {
                throw new ObjectDisposedException(nameof(SearchContext));
            }
        }

        private void ClearCore()
        {
            _positionProvider = null;
            _entityKeyProvider = null;
            _searchStats = null;
            _typedData.Clear();
        }
    }
}
