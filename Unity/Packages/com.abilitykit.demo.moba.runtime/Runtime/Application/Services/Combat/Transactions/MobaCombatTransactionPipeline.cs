using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services.Combat.Transactions
{
    public enum MobaCombatTransactionStage : byte
    {
        Prepare = 0,
        Modify = 1,
        Validate = 2,
        Commit = 3,
        Complete = 4,
        Rollback = 5,
    }

    public readonly struct MobaCombatTransactionContext
    {
        public MobaCombatTransactionContext(long transactionId, long rootContextId, int depth)
        {
            TransactionId = transactionId;
            RootContextId = rootContextId;
            Depth = depth;
        }

        public long TransactionId { get; }
        public long RootContextId { get; }
        public int Depth { get; }
    }

    public interface IMobaCombatTransaction
    {
        long RootContextId { get; }
        bool IsCancelled { get; }
        string FailureReason { get; }
        void Cancel(string reason);
    }

    public interface IMobaRevertibleCombatTransaction : IMobaCombatTransaction
    {
        void Rollback();
    }

    public interface IMobaCombatTransactionInterceptor<in TTransaction>
        where TTransaction : class, IMobaCombatTransaction
    {
        void OnStage(TTransaction transaction, MobaCombatTransactionStage stage, in MobaCombatTransactionContext context);
    }

    public abstract class MobaCombatTransactionBase : IMobaCombatTransaction
    {
        protected MobaCombatTransactionBase(long rootContextId)
        {
            RootContextId = rootContextId;
        }

        public long RootContextId { get; }
        public bool IsCancelled { get; private set; }
        public string FailureReason { get; private set; }

        public void Cancel(string reason)
        {
            IsCancelled = true;
            FailureReason = string.IsNullOrWhiteSpace(reason) ? "transaction cancelled" : reason;
        }
    }

    [WorldService(typeof(MobaCombatTransactionPipeline))]
    public sealed class MobaCombatTransactionPipeline : IService
    {
        private const int MaxReentryDepth = 16;
        private readonly Dictionary<Type, List<object>> _interceptors = new Dictionary<Type, List<object>>();
        private readonly Stack<long> _roots = new Stack<long>();
        private long _nextTransactionId = 1L;

        public void Register<TTransaction>(IMobaCombatTransactionInterceptor<TTransaction> interceptor)
            where TTransaction : class, IMobaCombatTransaction
        {
            if (interceptor == null) throw new ArgumentNullException(nameof(interceptor));
            var type = typeof(TTransaction);
            if (!_interceptors.TryGetValue(type, out var list))
            {
                list = new List<object>();
                _interceptors.Add(type, list);
            }
            if (!list.Contains(interceptor)) list.Add(interceptor);
        }

        public bool Unregister<TTransaction>(IMobaCombatTransactionInterceptor<TTransaction> interceptor)
            where TTransaction : class, IMobaCombatTransaction
        {
            return interceptor != null && _interceptors.TryGetValue(typeof(TTransaction), out var list) && list.Remove(interceptor);
        }

        public bool TryExecute<TTransaction>(TTransaction transaction, Func<TTransaction, bool> validate, Func<TTransaction, bool> commit)
            where TTransaction : class, IMobaCombatTransaction
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (commit == null) throw new ArgumentNullException(nameof(commit));
            if (_roots.Count >= MaxReentryDepth)
            {
                transaction.Cancel($"combat transaction reentry depth exceeded {MaxReentryDepth}");
                return false;
            }

            var transactionId = _nextTransactionId++;
            if (_nextTransactionId <= 0L) _nextTransactionId = 1L;
            var rootId = transaction.RootContextId != 0L
                ? transaction.RootContextId
                : _roots.Count > 0 ? _roots.Peek() : transactionId;
            var context = new MobaCombatTransactionContext(transactionId, rootId, _roots.Count);
            _roots.Push(rootId);
            var succeeded = false;
            try
            {
                Invoke(transaction, MobaCombatTransactionStage.Prepare, in context);
                if (transaction.IsCancelled) return false;
                Invoke(transaction, MobaCombatTransactionStage.Modify, in context);
                if (transaction.IsCancelled) return false;
                Invoke(transaction, MobaCombatTransactionStage.Validate, in context);
                if (transaction.IsCancelled || (validate != null && !validate(transaction))) return false;
                Invoke(transaction, MobaCombatTransactionStage.Commit, in context);
                if (transaction.IsCancelled) return false;
                if (!commit(transaction)) return false;
                succeeded = true;

                // Complete is a post-commit notification. Once Commit succeeds the transaction
                // cannot be cancelled retroactively; compensation belongs in the commit body.
                Invoke(transaction, MobaCombatTransactionStage.Complete, in context);
                return true;
            }
            finally
            {
                if (!succeeded)
                {
                    if (transaction is IMobaRevertibleCombatTransaction revertible) revertible.Rollback();
                    Invoke(transaction, MobaCombatTransactionStage.Rollback, in context);
                }
                _roots.Pop();
            }
        }

        private void Invoke<TTransaction>(TTransaction transaction, MobaCombatTransactionStage stage, in MobaCombatTransactionContext context)
            where TTransaction : class, IMobaCombatTransaction
        {
            if (!_interceptors.TryGetValue(typeof(TTransaction), out var list)) return;
            for (var i = 0; i < list.Count; i++)
                ((IMobaCombatTransactionInterceptor<TTransaction>)list[i]).OnStage(transaction, stage, in context);
        }

        public void Dispose()
        {
            _interceptors.Clear();
            _roots.Clear();
        }
    }
}
