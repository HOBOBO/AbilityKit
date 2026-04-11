using System;
using System.Collections.Generic;

namespace AbilityKit.Dataflow
{
    /// <summary>
    /// 管线执行结果
    /// </summary>
    public struct DataflowResult<TOutput>
    {
        /// <summary>
        /// 是否成功执行完成
        /// </summary>
        public bool IsSuccess => !IsAborted && !HasError;

        /// <summary>
        /// 是否被中断
        /// </summary>
        public bool IsAborted { get; }

        /// <summary>
        /// 是否有错误
        /// </summary>
        public bool HasError => Error != null;

        /// <summary>
        /// 执行过程中发生的错误
        /// </summary>
        public Exception Error { get; }

        /// <summary>
        /// 管线输出结果
        /// </summary>
        public TOutput Output { get; }

        /// <summary>
        /// 执行的处理器数量
        /// </summary>
        public int ProcessedCount { get; }

        public DataflowResult(TOutput output, int processedCount, bool aborted = false, Exception error = null)
        {
            Output = output;
            ProcessedCount = processedCount;
            IsAborted = aborted;
            Error = error;
        }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static DataflowResult<TOutput> Success(TOutput output, int processedCount)
        {
            return new DataflowResult<TOutput>(output, processedCount);
        }

        /// <summary>
        /// 创建中断结果
        /// </summary>
        public static DataflowResult<TOutput> Aborted(TOutput output, int processedCount)
        {
            return new DataflowResult<TOutput>(output, processedCount, aborted: true);
        }

        /// <summary>
        /// 创建错误结果
        /// </summary>
        public static DataflowResult<TOutput> Failure(Exception ex, TOutput output, int processedCount)
        {
            return new DataflowResult<TOutput>(output, processedCount, error: ex);
        }
    }
}
