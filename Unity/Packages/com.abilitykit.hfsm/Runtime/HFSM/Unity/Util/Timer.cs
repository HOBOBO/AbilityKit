namespace UnityHFSM
{
	/// <summary>
	/// Default timer that calculates the elapsed time.
	/// </summary>
	public class Timer : ITimer
	{
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS || UNITY_SERVER || UNITY_SERVER
		public float startTime;
		public float Elapsed => UnityEngine.Time.time - startTime;

		public void Reset()
		{
			startTime = UnityEngine.Time.time;
		}
#else
		private System.Diagnostics.Stopwatch _sw;
		public float Elapsed => _sw == null ? 0f : (float)_sw.Elapsed.TotalSeconds;

		public void Reset()
		{
			_sw ??= new System.Diagnostics.Stopwatch();
			_sw.Restart();
		}
#endif

		public static bool operator >(Timer timer, float duration)
			=> timer.Elapsed > duration;

		public static bool operator <(Timer timer, float duration)
			=> timer.Elapsed < duration;

		public static bool operator >=(Timer timer, float duration)
			=> timer.Elapsed >= duration;

		public static bool operator <=(Timer timer, float duration)
			=> timer.Elapsed <= duration;
	}
}
