using System;

namespace AbilityKit.Core.Common
{
    public static class DisposeUtils
    {
        public static void TryDispose(ref IDisposable disposable)
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

        public static void TryDispose(ref IDisposable disposable, Action<Exception> onException)
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

        public static void TryDispose<T>(ref T disposable) where T : class, IDisposable
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

        public static void TryDispose<T>(ref T disposable, Action<Exception> onException) where T : class, IDisposable
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
