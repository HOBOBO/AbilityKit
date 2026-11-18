using System;

namespace AbilityKit.Core.Utilities
{
    /// <summary>
    /// 保留至下一主版本移除窗口的引用释放兼容入口；新代码由资源所有者决定释放策略。
    /// </summary>
    /// <remarks>
    /// 非空对象同步释放后在 finally 中清空传入引用；清空发生在回调之后，不提供重入或并发防护。
    /// 清空引用不证明资源释放成功，也不影响其他别名；回调期间写入该引用的新对象仍会被最终清空。
    /// </remarks>
    [Obsolete("Disposal policy belongs to the resource owner; use an owner-local lifecycle helper before the next major version.")]
    public static class DisposeUtils
    {
        /// <summary>
        /// 释放接口引用并最终清空该引用；释放异常直接传播，空引用无动作。
        /// </summary>
        /// <remarks>
        /// 名称中的尝试不表示吞掉异常；调用期间仍可通过原引用重入，不能据此保证最多释放一次。
        /// </remarks>
        /// <param name="disposable">需要释放并在调用结束时清空的引用。</param>
        public static void TryDispose(ref IDisposable? disposable)
        {
            if (disposable == null) return;
            try
            {
                disposable.Dispose();
            }
            finally
            {
                disposable = null;
            }
        }

        /// <summary>
        /// 释放接口引用，将释放异常交给可选回调，并在最终清理中清空引用。
        /// </summary>
        /// <remarks>
        /// 空引用不触发回调；回调为空时释放异常被隔离，回调抛出的异常则传播并替代原释放异常。
        /// 回调执行时引用尚未清空，即使回调失败或写入新值，最终仍会清空。
        /// </remarks>
        /// <param name="disposable">需要释放并在调用结束时清空的引用。</param>
        /// <param name="onException">接收原释放异常的同步回调；兼容实现允许空值。</param>
        public static void TryDispose(ref IDisposable? disposable, Action<Exception> onException)
        {
            if (disposable == null) return;
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                onException?.Invoke(ex);
            }
            finally
            {
                disposable = null;
            }
        }

        /// <summary>
        /// 释放受 IDisposable 约束的类引用并最终清空；释放异常直接传播，空引用无动作。
        /// </summary>
        /// <remarks>
        /// 清空发生在释放返回之后，不阻止重入，也不使其他别名失效。
        /// </remarks>
        /// <typeparam name="T">实现 IDisposable 的引用类型。</typeparam>
        /// <param name="disposable">需要释放并在调用结束时清空的引用。</param>
        public static void TryDispose<T>(ref T? disposable) where T : class, IDisposable
        {
            if (disposable == null) return;
            try
            {
                disposable.Dispose();
            }
            finally
            {
                disposable = null;
            }
        }

        /// <summary>
        /// 释放受 IDisposable 约束的类引用，将释放异常交给可选回调并最终清空引用。
        /// </summary>
        /// <remarks>
        /// 回调接收原异常；空回调隔离释放异常，回调自身异常传播。最终清空不受上述失败影响。
        /// 回调期间引用仍可见，调用方负责重入、别名和失败后的资源责任。
        /// </remarks>
        /// <typeparam name="T">实现 IDisposable 的引用类型。</typeparam>
        /// <param name="disposable">需要释放并在调用结束时清空的引用。</param>
        /// <param name="onException">接收原释放异常的同步回调；兼容实现允许空值。</param>
        public static void TryDispose<T>(ref T? disposable, Action<Exception> onException) where T : class, IDisposable
        {
            if (disposable == null) return;
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                onException?.Invoke(ex);
            }
            finally
            {
                disposable = null;
            }
        }
    }
}
