using System;

namespace AbilityKit.Game.Flow.Battle.Modules
{
    public readonly struct SessionFailedEvent
    {
        public readonly Exception Exception;

        public SessionFailedEvent(Exception exception)
        {
            Exception = exception;
        }
    }
}
