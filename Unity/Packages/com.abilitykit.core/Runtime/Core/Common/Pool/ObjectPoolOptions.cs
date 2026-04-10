using System;

namespace AbilityKit.Core.Common.Pool
{
    public sealed class ObjectPoolOptions<T> where T : class
    {
        public Func<T> CreateFunc;
        public Action<T> OnGet;
        public Action<T> OnRelease;
        public Action<T> OnDestroy;

        public int DefaultCapacity = 0;
        public int MaxSize = 1024;

        public bool CollectionCheck = true;

        public ObjectPoolOptions(Func<T> createFunc)
        {
            CreateFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
        }
    }
}
