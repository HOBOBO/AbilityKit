#if UNITY_EDITOR

using System;
using System.Collections.Generic;

namespace UnityHFSM.Visualization
{
	public static class HfsmLiveRegistry
	{
		public static bool AutoRegisterEnabled = true;
		public static Predicate<object> AutoRegisterFilter;
		public static Func<object, string> AutoRegisterNameProvider;

		public sealed class Entry
		{
			public readonly string Name;
			public readonly WeakReference Fsm;
			public readonly Type FsmType;

			public Entry(string name, object fsm)
			{
				Name = name ?? string.Empty;
				Fsm = new WeakReference(fsm);
				FsmType = fsm?.GetType();
			}
		}

		static readonly List<Entry> _entries = new List<Entry>();

		public static event Action Changed;

		public static void AutoRegister(object fsm)
		{
			if (!AutoRegisterEnabled) return;
			if (fsm == null) return;

			if (AutoRegisterFilter != null && !AutoRegisterFilter(fsm))
				return;

			var name = AutoRegisterNameProvider != null
				? AutoRegisterNameProvider(fsm)
				: fsm.GetType().Name;

			Register(name, fsm);
		}

		public static void Register(string name, object fsm)
		{
			if (fsm == null) return;

			CleanupDeadEntries();

			for (var i = 0; i < _entries.Count; i++)
			{
				var e = _entries[i];
				if (ReferenceEquals(e.Fsm.Target, fsm))
				{
					_entries[i] = new Entry(name, fsm);
					Changed?.Invoke();
					return;
				}
			}

			_entries.Add(new Entry(name, fsm));
			Changed?.Invoke();
		}

		public static void Unregister(object fsm)
		{
			if (fsm == null) return;

			var removed = false;
			for (var i = _entries.Count - 1; i >= 0; i--)
			{
				var target = _entries[i].Fsm.Target;
				if (target == null || ReferenceEquals(target, fsm))
				{
					_entries.RemoveAt(i);
					removed = true;
				}
			}

			if (removed)
				Changed?.Invoke();
		}

		public static IReadOnlyList<Entry> GetEntries()
		{
			CleanupDeadEntries();
			return _entries;
		}

		static void CleanupDeadEntries()
		{
			var removed = false;
			for (var i = _entries.Count - 1; i >= 0; i--)
			{
				if (_entries[i].Fsm.Target == null)
				{
					_entries.RemoveAt(i);
					removed = true;
				}
			}

			if (removed)
				Changed?.Invoke();
		}
	}
}

#endif
