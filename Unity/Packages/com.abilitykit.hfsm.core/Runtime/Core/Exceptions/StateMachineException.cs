using System;

namespace AbilityKit.HFSM.Exceptions
{
	[Serializable]
	public class StateMachineException : Exception
	{
		public StateMachineException(string message) : base(message) { }
	}
}
