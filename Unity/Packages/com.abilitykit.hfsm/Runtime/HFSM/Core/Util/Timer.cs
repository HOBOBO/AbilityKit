namespace UnityHFSM
{
	/// <summary>
	/// Default timer that calculates the elapsed time using system time.
	/// This is the core implementation without Unity dependencies.
	/// For Unity integration, use UnityHFSM.Timer in the Unity layer.
	/// </summary>
	public class Timer : ITimer
	{
		private System.Diagnostics.Stopwatch _sw;
		
		public float Elapsed => _sw == null ? 0f : (float)_sw.Elapsed.TotalSeconds;

		public void Reset()
		{
			_sw ??= new System.Diagnostics.Stopwatch();
			_sw.Restart();
		}

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
