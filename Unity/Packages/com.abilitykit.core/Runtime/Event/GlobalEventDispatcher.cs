namespace AbilityKit.Core.Eventing
{
    /// <summary>Provides process-wide access to a shared <see cref="EventDispatcher"/>.</summary>
    public static class GlobalEventDispatcher
    {
        /// <summary>Gets the shared dispatcher instance.</summary>
        public static readonly EventDispatcher Instance = new EventDispatcher();

        /// <summary>Gets or registers the deterministic identifier for a string event name.</summary>
        /// <param name="eventId">The non-null event name.</param>
        /// <returns>The stable event identifier.</returns>
        public static int GetOrRegisterEventId(string eventId)
        {
            return Instance.GetOrRegisterEventId(eventId);
        }

        /// <summary>
        /// Clears all subscriptions and event identifiers from the global dispatcher.
        /// </summary>
        public static void Clear()
        {
            Instance.Clear();
        }

        /// <summary>Subscribes a typed handler to a global string event name.</summary>
        /// <typeparam name="TArgs">The event argument type.</typeparam>
        /// <param name="eventId">The non-null event name.</param>
        /// <param name="handler">The handler to invoke.</param>
        /// <param name="priority">The priority; higher values run first.</param>
        /// <param name="once">Whether to remove the handler before its first invocation.</param>
        /// <returns>An idempotent subscription handle.</returns>
        public static IEventSubscription Subscribe<TArgs>(string eventId, System.Action<TArgs> handler, int priority = 0, bool once = false)
        {
            return Instance.Subscribe(eventId, handler, priority, once);
        }

        /// <summary>Subscribes a typed handler to a global integer event identifier.</summary>
        /// <typeparam name="TArgs">The event argument type.</typeparam>
        /// <param name="eventId">The event identifier.</param>
        /// <param name="handler">The handler to invoke.</param>
        /// <param name="priority">The priority; higher values run first.</param>
        /// <param name="once">Whether to remove the handler before its first invocation.</param>
        /// <returns>An idempotent subscription handle.</returns>
        public static IEventSubscription Subscribe<TArgs>(int eventId, System.Action<TArgs> handler, int priority = 0, bool once = false)
        {
            return Instance.Subscribe(eventId, handler, priority, once);
        }

        /// <summary>Publishes typed arguments to a global string event name.</summary>
        /// <typeparam name="TArgs">The event argument type.</typeparam>
        /// <param name="eventId">The event name.</param>
        /// <param name="args">The arguments passed to each handler.</param>
        /// <param name="autoReleaseArgs">Whether to release the arguments after dispatch.</param>
        public static void Publish<TArgs>(string eventId, in TArgs args, bool autoReleaseArgs = true)
        {
            Instance.Publish(eventId, in args, autoReleaseArgs);
        }

        /// <summary>Publishes typed arguments to a global integer event identifier.</summary>
        /// <typeparam name="TArgs">The event argument type.</typeparam>
        /// <param name="eventId">The event identifier.</param>
        /// <param name="args">The arguments passed to each handler.</param>
        /// <param name="autoReleaseArgs">Whether to release the arguments after dispatch.</param>
        public static void Publish<TArgs>(int eventId, in TArgs args, bool autoReleaseArgs = true)
        {
            Instance.Publish(eventId, in args, autoReleaseArgs);
        }
    }
}
