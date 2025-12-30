using System;

namespace AbilityKit.Ability
{
    /// <summary>
    /// 通用的枚举
    /// </summary>
    public class GenericEnumId<T> : IEquatable<GenericEnumId<T>> where T : IEquatable<T>
    {
        private readonly T _id;

        public T Value => _id;

        public GenericEnumId(T id)
        {
            _id = id;
        }

        public bool Equals(GenericEnumId<T> other) => other != null && _id.Equals(other._id);
        public override bool Equals(object obj) => obj is GenericEnumId<T> other && Equals(other);
        public override int GetHashCode() => _id.GetHashCode();

        public static bool operator ==(GenericEnumId<T> a, GenericEnumId<T> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            return a.Equals(b);
        }

        public static bool operator !=(GenericEnumId<T> a, GenericEnumId<T> b) => !(a == b);
    }
}