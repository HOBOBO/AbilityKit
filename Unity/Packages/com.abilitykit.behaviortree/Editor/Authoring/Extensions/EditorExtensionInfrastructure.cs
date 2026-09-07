#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.BehaviorTree.Editor.Authoring.Extensions
{
    /// <summary>
    /// 确定性优先级注册表：仅保存贡献方，产出枚举按「优先级降序 + 注册序号升序」排序。
    /// 注册返回独立 <see cref="IDisposable"/>，释放只移除自身这一次注册；重复 Dispose 幂等。
    /// 产出的异常隔离由 <see cref="ExtensionSafeEnumerate"/> 负责。
    /// </summary>
    internal sealed class PriorityRegistration<T>
    {
        private readonly List<Entry> _entries = new();
        private long _nextId;

        public IDisposable Register(T value, int priority)
        {
            var entry = new Entry(++_nextId, priority, value);
            _entries.Add(entry);
            return new Registration(this, entry);
        }

        public IEnumerable<T> Enumerate()
        {
            var ordered = new List<Entry>(_entries);
            ordered.Sort(static (left, right) =>
            {
                var byPriority = right.Priority.CompareTo(left.Priority);
                return byPriority != 0 ? byPriority : left.Id.CompareTo(right.Id);
            });
            foreach (var entry in ordered)
            {
                yield return entry.Value;
            }
        }

        public void Reset()
        {
            _entries.Clear();
            _nextId = 0;
        }

        private void Remove(Entry entry) => _entries.Remove(entry);

        private sealed class Entry
        {
            public Entry(long id, int priority, T value)
            {
                Id = id;
                Priority = priority;
                Value = value;
            }

            public long Id { get; }
            public int Priority { get; }
            public T Value { get; }
        }

        private sealed class Registration : IDisposable
        {
            private PriorityRegistration<T>? _owner;
            private Entry? _entry;

            public Registration(PriorityRegistration<T> owner, Entry entry)
            {
                _owner = owner;
                _entry = entry;
            }

            public void Dispose()
            {
                var owner = _owner;
                var entry = _entry;
                _owner = null;
                _entry = null;
                if (owner == null || entry == null) return;
                owner.Remove(entry);
            }
        }
    }

    /// <summary>
    /// 异常隔离枚举：贡献方自身方法抛异常、返回 null、或枚举器中途抛异常，都只跳过该贡献方 /
    /// 中断该序列，不影响其它贡献方。用于编辑器扩展这类「第三方代码不可信」的调用边界。
    /// </summary>
    internal static class ExtensionSafeEnumerate
    {
        public static IEnumerable<TItem> Enumerate<TContributor, TItem>(
            IEnumerable<TContributor> contributors,
            Func<TContributor, IEnumerable<TItem>> produce)
        {
            foreach (var contributor in contributors)
            {
                if (contributor == null) continue;

                IEnumerable<TItem> items;
                try
                {
                    items = produce(contributor) ?? Array.Empty<TItem>();
                }
                catch (Exception ex)
                {
                    LogFailure(contributor, ex);
                    continue;
                }

                IEnumerator<TItem> enumerator;
                try
                {
                    enumerator = items.GetEnumerator();
                }
                catch (Exception ex)
                {
                    LogFailure(contributor, ex);
                    continue;
                }

                using (enumerator)
                {
                    while (true)
                    {
                        TItem current;
                        try
                        {
                            if (!enumerator.MoveNext()) break;
                            current = enumerator.Current;
                        }
                        catch (Exception ex)
                        {
                            LogFailure(contributor, ex);
                            break;
                        }

                        yield return current;
                    }
                }
            }
        }

        private static void LogFailure<T>(T contributor, Exception exception)
        {
            var name = SafeName(contributor);
            Debug.LogWarning($"[BtEditor] 扩展贡献方 '{name}' 调用失败并已隔离: {exception.Message}");
        }

        private static string SafeName<T>(T contributor)
        {
            try
            {
                return contributor?.GetType().Name ?? "<null>";
            }
            catch
            {
                return "<unknown>";
            }
        }
    }
}
