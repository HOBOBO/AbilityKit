using System;
using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Execution
{
    public interface RuntimeSnapshotMigrator
    {
        int FromVersion { get; }
        int ToVersion { get; }
        TreeRuntimeSnapshot Migrate(TreeRuntimeSnapshot snapshot);
    }

    public sealed class RuntimeSnapshotMigrationRegistry
    {
        private readonly Dictionary<int, RuntimeSnapshotMigrator> _migrators = new();

        public static RuntimeSnapshotMigrationRegistry Global { get; } = new();

        public void Register(RuntimeSnapshotMigrator migrator)
        {
            if (migrator == null) throw new ArgumentNullException(nameof(migrator));
            if (migrator.ToVersion <= migrator.FromVersion)
                throw new ArgumentException("BT snapshot migrators must move to a newer version.", nameof(migrator));
            _migrators[migrator.FromVersion] = migrator;
        }

        public TreeRuntimeSnapshot MigrateToCurrent(TreeRuntimeSnapshot snapshot)
            => Migrate(snapshot, TreeRuntimeSnapshot.CurrentSnapshotVersion);

        public TreeRuntimeSnapshot Migrate(TreeRuntimeSnapshot snapshot, int targetVersion)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var current = snapshot;
            while (current.SnapshotVersion < targetVersion)
            {
                if (!_migrators.TryGetValue(current.SnapshotVersion, out var migrator)
                    || migrator.ToVersion > targetVersion)
                {
                    throw new InvalidOperationException(
                        $"Unsupported BT runtime snapshot version '{current.SnapshotVersion}'.");
                }
                current = migrator.Migrate(current)
                    ?? throw new InvalidOperationException(
                        $"BT runtime snapshot migrator from version {migrator.FromVersion} returned null.");
                if (current.SnapshotVersion != migrator.ToVersion)
                {
                    throw new InvalidOperationException(
                        $"BT runtime snapshot migrator from version {migrator.FromVersion} returned version {current.SnapshotVersion}, expected {migrator.ToVersion}.");
                }
            }

            if (current.SnapshotVersion != targetVersion)
                throw new InvalidOperationException(
                    $"Unsupported BT runtime snapshot version '{current.SnapshotVersion}'.");
            return current;
        }
    }
}
