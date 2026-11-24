using System;
using System.Collections.Generic;

namespace AbilityKit.Coordinator
{
    /// <summary>
    /// Shared state sampling entry point for coordinator consumers.
    /// New hosts can write directly into the caller-owned buffer; legacy hosts
    /// remain usable through the array bridge without changing their contract.
    /// </summary>
    public static class LogicWorldDriverStateBufferExtensions
    {
        /// <summary>
        /// Appends current driver states to <paramref name="buffer"/>.
        /// The buffer is caller-owned and is not cleared by this method.
        /// </summary>
        public static int FillAllEntityStates(
            this ILogicWorldDriverBridge driver,
            IList<SnapshotEntityState> buffer)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            if (driver is ILogicWorldDriverStateBufferBridge stateBufferBridge)
            {
                return stateBufferBridge.FillAllEntityStates(buffer);
            }

            var states = driver.GetAllEntityStates();
            if (states == null || states.Length == 0)
            {
                return 0;
            }

            for (var i = 0; i < states.Length; i++)
            {
                buffer.Add(states[i]);
            }

            return states.Length;
        }
    }
}
