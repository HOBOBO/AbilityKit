using System;
using System.Collections.Generic;

namespace Emilia.Reference
{
    public interface IReference
    {
        void Clear();
    }

    public static class ReferencePool
    {
        private static Dictionary<Type, Queue<IReference>> referencePools = new Dictionary<Type, Queue<IReference>>();

        public static T Acquire<T>() where T : class, IReference, new()
        {
            Queue<IReference> pool = GetPool(typeof(T));
            lock (pool)
            {
                if (pool.Count > 0) return (T) pool.Dequeue();
            }

            return new T();
        }

        public static void Release(IReference reference)
        {
            if (reference == null) return;

            reference.Clear();

            Type referenceType = reference.GetType();
            Queue<IReference> pool = GetPool(referenceType);

            lock (pool)
            {
                pool.Enqueue(reference);
            }
        }

        public static void Clear<T>() where T : class, IReference
        {
            GetPool(typeof(T)).Clear();
        }

        private static Queue<IReference> GetPool(Type referenceType)
        {
            Queue<IReference> referenceCollection;

            lock (referencePools)
            {
                if (referencePools.TryGetValue(referenceType, out referenceCollection)) return referenceCollection;
                referenceCollection = new Queue<IReference>();
                referencePools.Add(referenceType, referenceCollection);
            }

            return referenceCollection;
        }

        public static void Clear()
        {
            lock (referencePools)
            {
                foreach (var pair in referencePools) pair.Value.Clear();
                referencePools.Clear();
            }
        }
    }
}