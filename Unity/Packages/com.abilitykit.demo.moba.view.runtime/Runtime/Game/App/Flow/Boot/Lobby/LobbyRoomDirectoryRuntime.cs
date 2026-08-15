using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;

namespace AbilityKit.Game.Flow
{
    internal sealed class LobbyRoomDirectoryRuntime
    {
        private readonly List<DemoRoomSummary> _rooms = new List<DemoRoomSummary>();
        private int _generation;

        public IReadOnlyList<DemoRoomSummary> Rooms => _rooms;
        public bool IsLoaded { get; private set; }
        public bool IsBusy { get; private set; }
        public long LastRefreshUnixMs { get; private set; }

        public void Attach()
        {
            _generation++;
            _rooms.Clear();
            IsLoaded = false;
            IsBusy = false;
            LastRefreshUnixMs = 0L;
        }

        public void Detach()
        {
            _generation++;
            IsBusy = false;
        }

        public async Task RefreshAsync(
            IDemoRoomDirectoryClient directory,
            DemoRoomDirectoryQuery query,
            TimeSpan? timeout,
            LobbyOperationContext operationContext,
            Func<LobbyOperationContext, bool> isCurrentOperation)
        {
            if (directory == null) throw new ArgumentNullException(nameof(directory));
            if (isCurrentOperation == null) throw new ArgumentNullException(nameof(isCurrentOperation));
            if (!isCurrentOperation(operationContext) || IsBusy) return;

            var generation = _generation;
            IsBusy = true;
            try
            {
                var result = await directory.ListRoomsAsync(
                    query,
                    timeout,
                    operationContext.CancellationToken);
                if (!IsCurrent(generation, operationContext, isCurrentOperation)) return;
                if (!result.Success)
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(result.Message)
                            ? "Room directory request failed."
                            : result.Message);
                }

                var openRooms = new List<DemoRoomSummary>();
                for (var i = 0; i < result.Rooms.Count; i++)
                {
                    if (result.Rooms[i].HasOpenSlot) openRooms.Add(result.Rooms[i]);
                }

                if (!IsCurrent(generation, operationContext, isCurrentOperation)) return;
                _rooms.Clear();
                _rooms.AddRange(openRooms);
                IsLoaded = true;
                LastRefreshUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
            finally
            {
                if (IsCurrent(generation, operationContext, isCurrentOperation))
                {
                    IsBusy = false;
                }
            }
        }

        private bool IsCurrent(
            int generation,
            LobbyOperationContext operationContext,
            Func<LobbyOperationContext, bool> isCurrentOperation)
        {
            return generation == _generation && isCurrentOperation(operationContext);
        }
    }
}
