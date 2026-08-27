#nullable enable

using System;
using System.Diagnostics;

namespace AbilityKit.Demo.Shooter.Runtime
{
    internal interface IShooterRvoSolver
    {
        Action<string, double>? StageTimingSink { get; set; }

        void Solve(ShooterRvoWorldWorkspace workspace, ShooterRvoOptions options, float deltaTime);
    }

    internal sealed class ShooterManagedRvoSolver : IShooterRvoSolver
    {
        internal const float Epsilon = 0.00001f;
        internal const float EpsilonSquared = Epsilon * Epsilon;

        private readonly IShooterRvoNeighborAccelerationService _neighborAcceleration;
        private readonly IShooterRvoAgentSolveAccelerationService? _agentSolveAcceleration;
        private readonly Action<int> _solveAgentAction;
        private ShooterRvoWorldWorkspace? _activeWorkspace;
        private ShooterRvoOptions? _activeOptions;
        private float _activeDeltaTime;
        private int _acceleratedCollectionCount;

        public ShooterManagedRvoSolver(IShooterRvoNeighborAccelerationService? neighborAcceleration = null)
        {
            _neighborAcceleration = neighborAcceleration ?? ShooterNullRvoNeighborAccelerationService.Instance;
            _agentSolveAcceleration = neighborAcceleration as IShooterRvoAgentSolveAccelerationService;
            _solveAgentAction = SolveActiveAgent;
        }

        public Action<string, double>? StageTimingSink { get; set; }

        public void Solve(ShooterRvoWorldWorkspace workspace, ShooterRvoOptions options, float deltaTime)
        {
            if (workspace == null) throw new ArgumentNullException(nameof(workspace));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (workspace.Count == 0 || deltaTime <= 0f)
            {
                return;
            }

            var sink = StageTimingSink;
            var neighborCollectionMilliseconds = 0d;
            var acceleratedNeighborsValid = false;
            if (options.PreferAcceleration && _neighborAcceleration.IsAvailable)
            {
                Array.Clear(workspace.NeighborCounts, 0, workspace.Count);
                var batch = new ShooterRvoNeighborBatch(
                    workspace.Count,
                    options.MaxNeighbors,
                    options.NeighborDistance,
                    workspace.EntityIds,
                    workspace.PositionX,
                    workspace.PositionY,
                    workspace.NeighborCounts,
                    workspace.NeighborIndices,
                    workspace.NeighborDistanceSquared);
                var collectionStartedAt = sink != null ? Stopwatch.GetTimestamp() : 0L;
                var acceleratedNeighborsCollected = false;
                try
                {
                    acceleratedNeighborsCollected = _neighborAcceleration.TryCollectNeighbors(in batch);
                }
                catch
                {
                    acceleratedNeighborsCollected = false;
                }

                if (sink != null)
                {
                    neighborCollectionMilliseconds += ElapsedMilliseconds(collectionStartedAt);
                }

                if (acceleratedNeighborsCollected)
                {
                    // 采样校验：全量 O(n×K) 校验每 N 次加速收集执行一次（首次必校验）。
                    // 加速实现与托管路径按状态哈希逐字节对齐（见服务端对照测试），
                    // 采样的作用是漂移保险丝而非逐帧正确性来源；检测到失效即回退托管收集。
                    _acceleratedCollectionCount++;
                    var interval = Math.Max(1, options.AcceleratedValidationInterval);
                    var shouldValidate = _acceleratedCollectionCount % interval == 0;
                    if (shouldValidate)
                    {
                        var validationStartedAt = sink != null ? Stopwatch.GetTimestamp() : 0L;
                        acceleratedNeighborsValid = ValidateAcceleratedNeighbors(
                            workspace,
                            options.MaxNeighbors,
                            options.NeighborDistance * options.NeighborDistance);
                        if (sink != null)
                        {
                            sink(
                                ShooterEnemyRvoSolveBattleSystem.AcceleratedValidationStageName,
                                ElapsedMilliseconds(validationStartedAt));
                        }
                    }
                    else
                    {
                        acceleratedNeighborsValid = true;
                    }
                }
            }

            if (!acceleratedNeighborsValid)
            {
                var collectionStartedAt = sink != null ? Stopwatch.GetTimestamp() : 0L;
                CollectManagedNeighbors(workspace, options);
                if (sink != null)
                {
                    neighborCollectionMilliseconds += ElapsedMilliseconds(collectionStartedAt);
                }
            }

            if (sink != null)
            {
                sink(ShooterEnemyRvoSolveBattleSystem.NeighborCollectionStageName, neighborCollectionMilliseconds);
            }

            var solveStartedAt = sink != null ? Stopwatch.GetTimestamp() : 0L;
            var solvedInParallel = false;
            if (options.PreferAcceleration && _agentSolveAcceleration != null)
            {
                _activeWorkspace = workspace;
                _activeOptions = options;
                _activeDeltaTime = deltaTime;
                try
                {
                    solvedInParallel = _agentSolveAcceleration.TryForEachAgent(workspace.Count, _solveAgentAction);
                }
                finally
                {
                    _activeWorkspace = null;
                    _activeOptions = null;
                    _activeDeltaTime = 0f;
                }
            }

            if (!solvedInParallel)
            {
                for (var agentIndex = 0; agentIndex < workspace.Count; agentIndex++)
                {
                    SolveAgent(workspace, options, agentIndex, deltaTime);
                }
            }

            if (sink != null)
            {
                sink(ShooterEnemyRvoSolveBattleSystem.OrcaSolveStageName, ElapsedMilliseconds(solveStartedAt));
            }
        }

        private static double ElapsedMilliseconds(long startedAt) =>
            (Stopwatch.GetTimestamp() - startedAt) * 1000d / Stopwatch.Frequency;

        private void SolveActiveAgent(int agentIndex)
        {
            var workspace = _activeWorkspace ?? throw new InvalidOperationException("RVO workspace is not active.");
            var options = _activeOptions ?? throw new InvalidOperationException("RVO options are not active.");
            SolveAgent(workspace, options, agentIndex, _activeDeltaTime);
        }

        private static void CollectManagedNeighbors(ShooterRvoWorldWorkspace workspace, ShooterRvoOptions options)
        {
            Array.Clear(workspace.NeighborCounts, 0, workspace.Count);
            workspace.BuildGrid(options.NeighborDistance);
            for (var agentIndex = 0; agentIndex < workspace.Count; agentIndex++)
            {
                workspace.CollectNeighbors(agentIndex, options.NeighborDistance);
            }
        }

        private static bool ValidateAcceleratedNeighbors(
            ShooterRvoWorldWorkspace workspace,
            int maxNeighbors,
            float rangeSquared)
        {
            for (var agentIndex = 0; agentIndex < workspace.Count; agentIndex++)
            {
                var count = workspace.NeighborCounts[agentIndex];
                if (count < 0 || count > maxNeighbors)
                {
                    return false;
                }

                var offset = agentIndex * maxNeighbors;
                var previousDistance = -1f;
                var previousEntityId = 0u;
                var previousNeighborIndex = -1;
                for (var slot = 0; slot < count; slot++)
                {
                    var neighborIndex = workspace.NeighborIndices[offset + slot];
                    var distance = workspace.NeighborDistanceSquared[offset + slot];
                    if (neighborIndex < 0 || neighborIndex >= workspace.Count || neighborIndex == agentIndex ||
                        !IsFinite(distance) || distance < 0f || distance > rangeSquared ||
                        distance < previousDistance)
                    {
                        return false;
                    }

                    var entityId = workspace.EntityIds[neighborIndex];
                    if (slot > 0 && distance == previousDistance &&
                        (entityId < previousEntityId ||
                         (entityId == previousEntityId && neighborIndex <= previousNeighborIndex)))
                    {
                        return false;
                    }

                    var deltaX = workspace.PositionX[neighborIndex] - workspace.PositionX[agentIndex];
                    var deltaY = workspace.PositionY[neighborIndex] - workspace.PositionY[agentIndex];
                    var expectedDistance = deltaX * deltaX + deltaY * deltaY;
                    if (!IsFinite(expectedDistance) || distance != expectedDistance)
                    {
                        return false;
                    }

                    for (var previousSlot = 0; previousSlot < slot; previousSlot++)
                    {
                        if (workspace.NeighborIndices[offset + previousSlot] == neighborIndex)
                        {
                            return false;
                        }
                    }

                    previousDistance = distance;
                    previousEntityId = entityId;
                    previousNeighborIndex = neighborIndex;
                }
            }

            return true;
        }

        private static void SolveAgent(
            ShooterRvoWorldWorkspace workspace,
            ShooterRvoOptions options,
            int agentIndex,
            float deltaTime)
        {
            var lineOffset = agentIndex * options.MaxNeighbors;
            var projectedLineOffset = agentIndex * options.MaxNeighbors;
            var lineCount = 0;
            var velocity = new ShooterRvoVector(
                workspace.VelocityX[agentIndex],
                workspace.VelocityY[agentIndex]);
            var position = new ShooterRvoVector(
                workspace.PositionX[agentIndex],
                workspace.PositionY[agentIndex]);
            var inverseTimeHorizon = 1f / options.TimeHorizon;
            var inverseTimeStep = 1f / Math.Max(deltaTime, Epsilon);
            var neighborOffset = agentIndex * options.MaxNeighbors;

            for (var neighborSlot = 0; neighborSlot < workspace.NeighborCounts[agentIndex]; neighborSlot++)
            {
                var otherIndex = workspace.NeighborIndices[neighborOffset + neighborSlot];
                var otherPosition = new ShooterRvoVector(
                    workspace.PositionX[otherIndex],
                    workspace.PositionY[otherIndex]);
                var otherVelocity = new ShooterRvoVector(
                    workspace.VelocityX[otherIndex],
                    workspace.VelocityY[otherIndex]);
                var relativePosition = otherPosition - position;
                var relativeVelocity = velocity - otherVelocity;
                var distanceSquared = relativePosition.LengthSquared;
                var combinedRadius = workspace.Radius[agentIndex] + workspace.Radius[otherIndex];
                var combinedRadiusSquared = combinedRadius * combinedRadius;

                ShooterRvoVector direction;
                ShooterRvoVector correction;
                if (distanceSquared > combinedRadiusSquared)
                {
                    var w = relativeVelocity - inverseTimeHorizon * relativePosition;
                    var wLengthSquared = w.LengthSquared;
                    var dot = ShooterRvoVector.Dot(w, relativePosition);
                    if (dot < 0f && dot * dot > combinedRadiusSquared * wLengthSquared)
                    {
                        var unitW = NormalizeForPair(w, workspace.EntityIds[agentIndex], workspace.EntityIds[otherIndex]);
                        var wLength = MathF.Sqrt(Math.Max(wLengthSquared, EpsilonSquared));
                        direction = new ShooterRvoVector(unitW.Y, -unitW.X);
                        correction = (combinedRadius * inverseTimeHorizon - wLength) * unitW;
                    }
                    else
                    {
                        var leg = MathF.Sqrt(Math.Max(0f, distanceSquared - combinedRadiusSquared));
                        if (ShooterRvoVector.Det(relativePosition, w) > 0f)
                        {
                            direction = new ShooterRvoVector(
                                relativePosition.X * leg - relativePosition.Y * combinedRadius,
                                relativePosition.X * combinedRadius + relativePosition.Y * leg) / distanceSquared;
                        }
                        else
                        {
                            direction = -new ShooterRvoVector(
                                relativePosition.X * leg + relativePosition.Y * combinedRadius,
                                -relativePosition.X * combinedRadius + relativePosition.Y * leg) / distanceSquared;
                        }

                        correction = ShooterRvoVector.Dot(relativeVelocity, direction) * direction - relativeVelocity;
                    }
                }
                else
                {
                    var w = relativeVelocity - inverseTimeStep * relativePosition;
                    var unitW = NormalizeForPair(w, workspace.EntityIds[agentIndex], workspace.EntityIds[otherIndex]);
                    var wLength = MathF.Sqrt(Math.Max(w.LengthSquared, EpsilonSquared));
                    direction = new ShooterRvoVector(unitW.Y, -unitW.X);
                    correction = (combinedRadius * inverseTimeStep - wLength) * unitW;
                }

                workspace.Lines[lineOffset + lineCount] = new ShooterRvoLine
                {
                    Point = velocity + 0.5f * correction,
                    Direction = direction
                };
                lineCount++;
            }

            var preferredVelocity = new ShooterRvoVector(
                workspace.PreferredVelocityX[agentIndex],
                workspace.PreferredVelocityY[agentIndex]);
            var result = default(ShooterRvoVector);
            var failedLine = LinearProgram2(
                workspace.Lines,
                lineOffset,
                lineCount,
                workspace.MaxSpeed[agentIndex],
                preferredVelocity,
                directionOpt: false,
                ref result);
            if (failedLine < lineCount)
            {
                LinearProgram3(
                    workspace.Lines,
                    lineOffset,
                    lineCount,
                    failedLine,
                    workspace.ProjectedLines,
                    projectedLineOffset,
                    workspace.MaxSpeed[agentIndex],
                    ref result);
            }

            if (!IsFinite(result.X) || !IsFinite(result.Y))
            {
                result = Limit(preferredVelocity, workspace.MaxSpeed[agentIndex]);
            }

            workspace.OutputVelocityX[agentIndex] = result.X;
            workspace.OutputVelocityY[agentIndex] = result.Y;
        }

        private static bool LinearProgram1(
            ShooterRvoLine[] lines,
            int offset,
            int lineNumber,
            float radius,
            ShooterRvoVector optimalVelocity,
            bool directionOpt,
            ref ShooterRvoVector result)
        {
            var line = lines[offset + lineNumber];
            var dot = ShooterRvoVector.Dot(line.Point, line.Direction);
            var discriminant = dot * dot + radius * radius - line.Point.LengthSquared;
            if (discriminant < 0f)
            {
                return false;
            }

            var sqrtDiscriminant = MathF.Sqrt(discriminant);
            var left = -dot - sqrtDiscriminant;
            var right = -dot + sqrtDiscriminant;
            for (var i = 0; i < lineNumber; i++)
            {
                var previous = lines[offset + i];
                var denominator = ShooterRvoVector.Det(line.Direction, previous.Direction);
                var numerator = ShooterRvoVector.Det(previous.Direction, line.Point - previous.Point);
                if (MathF.Abs(denominator) <= Epsilon)
                {
                    if (numerator < 0f)
                    {
                        return false;
                    }

                    continue;
                }

                var value = numerator / denominator;
                if (denominator >= 0f)
                {
                    right = Math.Min(right, value);
                }
                else
                {
                    left = Math.Max(left, value);
                }

                if (left > right)
                {
                    return false;
                }
            }

            if (directionOpt)
            {
                result = line.Point + (ShooterRvoVector.Dot(optimalVelocity, line.Direction) > 0f ? right : left) * line.Direction;
                return true;
            }

            var projection = ShooterRvoVector.Dot(line.Direction, optimalVelocity - line.Point);
            projection = Math.Max(left, Math.Min(right, projection));
            result = line.Point + projection * line.Direction;
            return true;
        }

        private static int LinearProgram2(
            ShooterRvoLine[] lines,
            int offset,
            int count,
            float radius,
            ShooterRvoVector optimalVelocity,
            bool directionOpt,
            ref ShooterRvoVector result)
        {
            result = directionOpt
                ? optimalVelocity * radius
                : Limit(optimalVelocity, radius);

            for (var i = 0; i < count; i++)
            {
                var line = lines[offset + i];
                if (ShooterRvoVector.Det(line.Direction, line.Point - result) <= 0f)
                {
                    continue;
                }

                var previousResult = result;
                if (!LinearProgram1(lines, offset, i, radius, optimalVelocity, directionOpt, ref result))
                {
                    result = previousResult;
                    return i;
                }
            }

            return count;
        }

        private static void LinearProgram3(
            ShooterRvoLine[] lines,
            int offset,
            int count,
            int beginLine,
            ShooterRvoLine[] projectedLines,
            int projectedOffset,
            float radius,
            ref ShooterRvoVector result)
        {
            var distance = 0f;
            for (var i = beginLine; i < count; i++)
            {
                var current = lines[offset + i];
                if (ShooterRvoVector.Det(current.Direction, current.Point - result) <= distance)
                {
                    continue;
                }

                var projectedCount = 0;
                for (var j = 0; j < i; j++)
                {
                    var previous = lines[offset + j];
                    var determinant = ShooterRvoVector.Det(current.Direction, previous.Direction);
                    ShooterRvoVector point;
                    if (MathF.Abs(determinant) <= Epsilon)
                    {
                        if (ShooterRvoVector.Dot(current.Direction, previous.Direction) > 0f)
                        {
                            continue;
                        }

                        point = 0.5f * (current.Point + previous.Point);
                    }
                    else
                    {
                        point = current.Point +
                            (ShooterRvoVector.Det(previous.Direction, current.Point - previous.Point) / determinant) *
                            current.Direction;
                    }

                    projectedLines[projectedOffset + projectedCount++] = new ShooterRvoLine
                    {
                        Point = point,
                        Direction = (previous.Direction - current.Direction).NormalizedOr(new ShooterRvoVector(1f, 0f))
                    };
                }

                var previousResult = result;
                var optimalDirection = new ShooterRvoVector(-current.Direction.Y, current.Direction.X);
                if (LinearProgram2(projectedLines, projectedOffset, projectedCount, radius, optimalDirection, directionOpt: true, ref result) < projectedCount)
                {
                    result = previousResult;
                }

                distance = ShooterRvoVector.Det(current.Direction, current.Point - result);
            }
        }

        private static ShooterRvoVector NormalizeForPair(ShooterRvoVector value, uint entityId, uint otherEntityId)
        {
            if (value.LengthSquared > EpsilonSquared)
            {
                return value / MathF.Sqrt(value.LengthSquared);
            }

            return entityId < otherEntityId
                ? new ShooterRvoVector(1f, 0f)
                : new ShooterRvoVector(-1f, 0f);
        }

        private static ShooterRvoVector Limit(ShooterRvoVector value, float radius)
        {
            var lengthSquared = value.LengthSquared;
            if (lengthSquared <= radius * radius)
            {
                return value;
            }

            if (lengthSquared <= EpsilonSquared)
            {
                return default;
            }

            return value * (radius / MathF.Sqrt(lengthSquared));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
