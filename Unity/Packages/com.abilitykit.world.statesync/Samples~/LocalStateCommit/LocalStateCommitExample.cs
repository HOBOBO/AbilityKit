using System;

namespace AbilityKit.Ability.StateSync.Samples
{
    /// <summary>
    /// 演示单线程、单权威作用域下，宿主如何在导入成功后一起发布状态值和提交游标。
    /// </summary>
    public sealed class LocalStateCommitExample
    {
        private StateSample? _committed;
        private bool _importInProgress;

        /// <summary>
        /// 候选或已提交的离线状态值；只有从本宿主读出的已提交结果才能作为消费依据。
        /// </summary>
        public readonly struct StateSample
        {
            /// <summary>
            /// 构造待校验的状态值，不代表已导入或已提交。
            /// </summary>
            public StateSample(int frame, long revision, int value)
            {
                Frame = frame;
                Revision = revision;
                Value = value;
            }

            /// <summary>
            /// 权威状态帧，允许从零开始。
            /// </summary>
            public int Frame { get; }

            /// <summary>
            /// 当前作用域的提交修订号，不表示输入确认。
            /// </summary>
            public long Revision { get; }

            /// <summary>
            /// 用于演示候选隔离的状态值，不引用实际逻辑世界。
            /// </summary>
            public int Value { get; }
        }

        /// <summary>
        /// 将候选值导入独立结果；返回失败时丢弃输出，不得修改已发布世界或触发外部副作用。
        /// </summary>
        public delegate bool TryImportValue(int candidateValue, out int importedValue);

        /// <summary>
        /// 拒绝非法、重复或迟到的候选而不调用导入；同帧可提交更高修订。
        /// 仅导入返回成功才推进游标；导入返回失败或抛异常时保留原提交，异常原样传给调用方。
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// 未提供导入函数。
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// 在导入回调中重入提交。
        /// </exception>
        public bool TryCommit(StateSample candidate, TryImportValue import)
        {
            if (_importInProgress)
                throw new InvalidOperationException("导入回调不能重入提交。");
            if (import == null)
                throw new ArgumentNullException(nameof(import));
            if (candidate.Frame < 0 || candidate.Revision <= 0)
                return false;
            if (_committed is StateSample previous &&
                (candidate.Frame < previous.Frame || candidate.Revision <= previous.Revision))
                return false;

            _importInProgress = true;
            try
            {
                if (!import(candidate.Value, out var importedValue))
                    return false;

                _committed = new StateSample(candidate.Frame, candidate.Revision, importedValue);
                return true;
            }
            finally
            {
                _importInProgress = false;
            }
        }

        /// <summary>
        /// 读取已提交的值和游标；没有提交时返回失败，输出值不可用于消费。
        /// 导入过程中仍只能看到上一次成功提交。
        /// </summary>
        public bool TryGetCommitted(out StateSample committed)
        {
            committed = _committed.GetValueOrDefault();
            return _committed.HasValue;
        }
    }
}
