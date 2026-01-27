#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace Emilia.Kit.Editor
{
    public static class ReflectUtility
    {
        private static Dictionary<string, Type> typeCache = new Dictionary<string, Type>();
        private static Dictionary<Type, Attribute[]> typeAttributeCache = new Dictionary<Type, Attribute[]>();

        private static Dictionary<Type, Dictionary<string, MemberGetter<object, object>>> getValueCache = new Dictionary<Type, Dictionary<string, MemberGetter<object, object>>>();
        private static Dictionary<Type, Dictionary<string, MemberSetter<object, object>>> setValueCache = new Dictionary<Type, Dictionary<string, MemberSetter<object, object>>>();
        private static Dictionary<Type, Dictionary<string, MethodCaller<object, object>>> methodCache = new Dictionary<Type, Dictionary<string, MethodCaller<object, object>>>();
        private static Dictionary<MethodInfo, MethodCaller<object, object>> methodCacheByMethodInfo = new Dictionary<MethodInfo, MethodCaller<object, object>>();
        private static Dictionary<Type, CtorInvoker<object>> ctorCache = new Dictionary<Type, CtorInvoker<object>>();

        public static Type GetType(string typeString)
        {
            if (typeCache.TryGetValue(typeString, out var type)) return type;
            type = Type.GetType(typeString);
            typeCache[typeString] = type;
            return type;
        }
        
        public static Type[] GetDirectInterfaces(Type type)
        {
            Type[] currentInterfaces = type.GetInterfaces();
            Type[] baseInterfaces = type.BaseType?.GetInterfaces() ?? Array.Empty<Type>();
            return currentInterfaces.Except(baseInterfaces).ToArray();
        }

        public static T GetAttribute<T>(Type type) where T : Attribute
        {
            Attribute[] customAttributes = GetAttributes(type);
            return customAttributes.FirstOrDefault(x => x is T) as T;
        }

        public static T[] GetAttributes<T>(Type type) where T : Attribute
        {
            Attribute[] customAttributes = GetAttributes(type);
            return customAttributes.OfType<T>().ToArray();
        }

        public static Attribute[] GetAttributes(Type type)
        {
            if (type == null) return null;
            Attribute[] customAttributes = type.GetCustomAttributes().ToArray();
            typeAttributeCache[type] = customAttributes;
            return customAttributes;
        }

        public static object CreateInstance(string typeString, params object[] args)
        {
            Type type = GetType(typeString);
            return CreateInstance(type, args);
        }

        public static T CreateInstance<T>(params object[] args)
        {
            return (T) CreateInstance(typeof(T), args);
        }

        public static object CreateInstance(Type type, params object[] args)
        {
            try
            {
                if (type == null) return null;
                CtorInvoker<object> ctorInvoker = null;
                if (ctorCache.TryGetValue(type, out var findCtor)) return findCtor.Invoke(args);
                ctorInvoker = FastReflection.DelegateForCtor(type, args.Select(x => x.GetType()).ToArray());
                ctorCache.Add(type, ctorInvoker);
                return ctorInvoker.Invoke(args);
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
                return null;
            }
        }

        public static T GetValue<T>(object target, string name)
        {
            return (T) GetValue(target.GetType(), target, name);
        }

        public static T GetValue<T>(Type type, object target, string name)
        {
            return (T) GetValue(type, target, name);
        }

        public static object GetValue(object target, string name)
        {
            return GetValue(target.GetType(), target, name);
        }

        public static object GetValue(string typeName, object target, string name)
        {
            Type type = Type.GetType(typeName);
            return GetValue(type, target, name);
        }

        public static object GetValue(Type type, object target, string name)
        {
            try
            {
                MemberGetter<object, object> getter = null;
                if (getValueCache.TryGetValue(type, out var nameDict) == false)
                {
                    getValueCache.Add(type, new Dictionary<string, MemberGetter<object, object>>());
                }
                else if (nameDict.TryGetValue(name, out getter) && getter != null) return getter(target);

                try
                {
                    FieldInfo fieldInfo = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (fieldInfo != null)
                    {
                        getter = FastReflection.DelegateForGet(fieldInfo);
                    }
                    else
                    {
                        PropertyInfo propertyInfo = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        if (propertyInfo != null) getter = FastReflection.DelegateForGet(propertyInfo);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(e.ToString());
                }

                if (getter == null) return null;

                getValueCache[type].Add(name, getter);
                return getter(target);
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
                return null;
            }
        }

        public static void SetValue(object target, string name, object value)
        {
            SetValue(target.GetType(), target, name, value);
        }

        public static void SetValue(string typeName, object target, string name, object value)
        {
            Type type = Type.GetType(typeName);
            SetValue(type, target, name, value);
        }

        public static void SetValue(Type type, object target, string name, object value)
        {
            try
            {
                MemberSetter<object, object> setter = null;
                if (setValueCache.TryGetValue(type, out var nameDict) == false)
                {
                    setValueCache.Add(type, new Dictionary<string, MemberSetter<object, object>>());
                }
                else if (nameDict.TryGetValue(name, out setter) && setter != null)
                {
                    setter(ref target, value);
                    return;
                }

                try
                {
                    FieldInfo fieldInfo = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (fieldInfo != null)
                    {
                        setter = FastReflection.DelegateForSet(fieldInfo);
                    }
                    else
                    {
                        PropertyInfo propertyInfo = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        if (propertyInfo != null) setter = FastReflection.DelegateForSet(propertyInfo);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(e.ToString());
                }

                if (setter == null) return;

                setValueCache[type].Add(name, setter);
                setter(ref target, value);
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
            }
        }

        public static T Invoke<T>(object target, string name, object[] args = null)
        {
            return (T) Invoke(target, target.GetType(), name, true, args, null);
        }

        public static T Invoke<T>(object target, string name, bool isUseArgType, object[] args = null)
        {
            return (T) Invoke(target, target.GetType(), name, isUseArgType, args, null);
        }

        public static T Invoke<T>(object target, Type type, string name, object[] args = null)
        {
            return (T) Invoke(target, type, name, true, args, null);
        }

        public static T Invoke<T>(object target, Type type, string name, bool isUseArgType, object[] args = null)
        {
            return (T) Invoke(target, type, name, isUseArgType, args, null);
        }

        public static object Invoke(object target, string name, object[] args = null)
        {
            return Invoke(target, target.GetType(), name, true, args, null);
        }

        public static object Invoke(object target, string name, bool isUseArgType, object[] args = null)
        {
            return Invoke(target, target.GetType(), name, isUseArgType, args, null);
        }

        public static object Invoke(object target, string typeName, string name, object[] args = null)
        {
            Type type = Type.GetType(typeName);
            return Invoke(target, type, name, true, args, null);
        }

        public static object Invoke(object target, string typeName, string name, bool isUseArgType, object[] args = null)
        {
            Type type = Type.GetType(typeName);
            return Invoke(target, type, name, isUseArgType, args, null);
        }

        public static object Invoke(object target, Type type, string name, object[] args = null)
        {
            return Invoke(target, type, name, true, args, null);
        }

        public static object Invoke(object target, Type type, string name, object[] args, Type[] argTypes)
        {
            return Invoke(target, type, name, true, args, argTypes);
        }

        public static object Invoke(object target, Type type, string name, bool isUseArgType, object[] args, Type[] argTypes)
        {
            try
            {
                if (type == null || string.IsNullOrEmpty(name)) return null;
                MethodCaller<object, object> caller = null;
                if (methodCache.TryGetValue(type, out var nameDict) == false)
                {
                    methodCache.Add(type, new Dictionary<string, MethodCaller<object, object>>());
                }
                else if (nameDict.TryGetValue(name, out caller)) return caller.Invoke(target, args);

                try
                {
                    BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                    MethodInfo methodInfo = null;
                    if (isUseArgType && args != null)
                    {
                        if (argTypes == null) argTypes = args.Select(x => x.GetType()).ToArray();
                        methodInfo = type.GetMethod(name, flags, null, argTypes, null);
                    }
                    else
                    {
                        methodInfo = type.GetMethod(name, flags);
                    }
                    if (methodInfo != null) caller = FastReflection.DelegateForCall(methodInfo);
                }
                catch (Exception e)
                {
                    Debug.LogError(e.ToString());
                }

                if (caller == null) return null;

                methodCache[type][name] = caller;
                return caller(target, args);
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
                return null;
            }
        }

        public static object Invoke(object target, MethodInfo method, object[] args)
        {
            try
            {
                if (method == null) return null;
                MethodCaller<object, object> caller;
                if (methodCacheByMethodInfo.TryGetValue(method, out caller)) return caller.Invoke(null, args);
                caller = FastReflection.DelegateForCall(method);
                if (caller == null) return null;

                methodCacheByMethodInfo[method] = caller;
                return caller.Invoke(target, args);
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
                return null;
            }
        }

        public static void ClearCache()
        {
            typeCache.Clear();
            typeAttributeCache.Clear();
            getValueCache.Clear();
            setValueCache.Clear();
            methodCache.Clear();
            methodCacheByMethodInfo.Clear();
            ctorCache.Clear();
        }

        public static void ClearCache(Type type)
        {
            if (getValueCache.ContainsKey(type)) getValueCache.Remove(type);
            if (setValueCache.ContainsKey(type)) setValueCache.Remove(type);
            if (methodCache.ContainsKey(type)) methodCache.Remove(type);
        }

        private delegate void MemberSetter<TTarget, in TValue>(ref TTarget target, TValue value);

        private delegate TReturn MemberGetter<in TTarget, out TReturn>(TTarget target);

        private delegate TReturn MethodCaller<in TTarget, out TReturn>(TTarget target, object[] args);

        private delegate T CtorInvoker<out T>(object[] parameters);

        /// <summary>
        /// A dynamic reflection extensions library that emits IL to set/get fields/properties, call methods and invoke constructors
        /// Once the delegate is created, it can be stored and reused resulting in much faster access times than using regular reflection
        /// The results are cached. Once a delegate is generated, any subsequent call to generate the same delegate on the same field/property/method will return the previously generated delegate
        /// Note: Since this generates IL, it won't work on AOT platforms such as iOS an Android. But is useful and works very well in editor codes and standalone targets
        /// </summary>
        private static class FastReflection
        {
            static ILEmitter emit = new ILEmitter();
            static Dictionary<int, Delegate> cache = new Dictionary<int, Delegate>();

            const string kCtorInvokerName = "CI<>";
            const string kMethodCallerName = "MC<>";
            const string kFieldSetterName = "FS<>";
            const string kFieldGetterName = "FG<>";
            const string kPropertySetterName = "PS<>";
            const string kPropertyGetterName = "PG<>";

            /// <summary>
            /// Generates or gets a strongly-typed open-instance delegate to the specified type constructor that takes the specified type params
            /// </summary>
            public static CtorInvoker<T> DelegateForCtor<T>(Type type, params Type[] paramTypes)
            {
                int key = kCtorInvokerName.GetHashCode() ^ type.GetHashCode();
                for (int i = 0; i < paramTypes.Length; i++) key ^= paramTypes[i].GetHashCode();

                if (cache.TryGetValue(key, out Delegate result)) return (CtorInvoker<T>) result;

                DynamicMethod dynMethod = new DynamicMethod(kCtorInvokerName,
                    typeof(T), new[] {typeof(object[])});

                emit.il = dynMethod.GetILGenerator();
                GenCtor<T>(type, paramTypes);

                result = dynMethod.CreateDelegate(typeof(CtorInvoker<T>));
                cache[key] = result;
                return (CtorInvoker<T>) result;
            }

            /// <summary>
            /// Generates or gets a weakly-typed open-instance delegate to the specified type constructor that takes the specified type params
            /// </summary>
            public static CtorInvoker<object> DelegateForCtor(Type type, params Type[] ctorParamTypes)
            {
                return DelegateForCtor<object>(type, ctorParamTypes);
            }

            /// <summary>
            /// Generates or gets a strongly-typed open-instance delegate to get the value of the specified property from a given target
            /// </summary>
            public static MemberGetter<TTarget, TReturn> DelegateForGet<TTarget, TReturn>(PropertyInfo property)
            {
                if (! property.CanRead) throw new InvalidOperationException("Property is not readable " + property.Name);

                int key = GetKey<TTarget, TReturn>(property, kPropertyGetterName);
                if (cache.TryGetValue(key, out Delegate result)) return (MemberGetter<TTarget, TReturn>) result;

                return GenDelegateForMember<MemberGetter<TTarget, TReturn>, PropertyInfo>(
                    property, key, kPropertyGetterName, GenPropertyGetter<TTarget>,
                    typeof(TReturn), typeof(TTarget));
            }

            /// <summary>
            /// Generates or gets a weakly-typed open-instance delegate to get the value of the specified property from a given target
            /// </summary>
            public static MemberGetter<object, object> DelegateForGet(PropertyInfo property)
            {
                return DelegateForGet<object, object>(property);
            }

            /// <summary>
            /// Generates or gets a strongly-typed open-instance delegate to set the value of the specified property on a given target
            /// </summary>
            public static MemberSetter<TTarget, TValue> DelegateForSet<TTarget, TValue>(PropertyInfo property)
            {
                if (! property.CanWrite) throw new InvalidOperationException("Property is not writable " + property.Name);

                int key = GetKey<TTarget, TValue>(property, kPropertySetterName);
                if (cache.TryGetValue(key, out Delegate result)) return (MemberSetter<TTarget, TValue>) result;

                return GenDelegateForMember<MemberSetter<TTarget, TValue>, PropertyInfo>(
                    property, key, kPropertySetterName, GenPropertySetter<TTarget>,
                    typeof(void), typeof(TTarget).MakeByRefType(), typeof(TValue));
            }

            /// <summary>
            /// Generates or gets a weakly-typed open-instance delegate to set the value of the specified property on a given target
            /// </summary>
            public static MemberSetter<object, object> DelegateForSet(PropertyInfo property)
            {
                return DelegateForSet<object, object>(property);
            }

            /// <summary>
            /// Generates an open-instance delegate to get the value of the property from a given target
            /// </summary>
            public static MemberGetter<TTarget, TReturn> DelegateForGet<TTarget, TReturn>(FieldInfo field)
            {
                int key = GetKey<TTarget, TReturn>(field, kFieldGetterName);
                if (cache.TryGetValue(key, out Delegate result)) return (MemberGetter<TTarget, TReturn>) result;

                return GenDelegateForMember<MemberGetter<TTarget, TReturn>, FieldInfo>(
                    field, key, kFieldGetterName, GenFieldGetter<TTarget>,
                    typeof(TReturn), typeof(TTarget));
            }

            /// <summary>
            /// Generates a weakly-typed open-instance delegate to set the value of the field in a given target
            /// </summary>
            public static MemberGetter<object, object> DelegateForGet(FieldInfo field)
            {
                return DelegateForGet<object, object>(field);
            }

            /// <summary>
            /// Generates a strongly-typed open-instance delegate to set the value of the field in a given target
            /// </summary>
            public static MemberSetter<TTarget, TValue> DelegateForSet<TTarget, TValue>(FieldInfo field)
            {
                int key = GetKey<TTarget, TValue>(field, kFieldSetterName);
                if (cache.TryGetValue(key, out Delegate result)) return (MemberSetter<TTarget, TValue>) result;

                return GenDelegateForMember<MemberSetter<TTarget, TValue>, FieldInfo>(
                    field, key, kFieldSetterName, GenFieldSetter<TTarget>,
                    typeof(void), typeof(TTarget).MakeByRefType(), typeof(TValue));
            }

            /// <summary>
            /// Generates a weakly-typed open-instance delegate to set the value of the field in a given target
            /// </summary>
            public static MemberSetter<object, object> DelegateForSet(FieldInfo field)
            {
                return DelegateForSet<object, object>(field);
            }

            /// <summary>
            /// Generates a strongly-typed open-instance delegate to invoke the specified method
            /// </summary>
            public static MethodCaller<TTarget, TReturn> DelegateForCall<TTarget, TReturn>(MethodInfo method)
            {
                int key = GetKey<TTarget, TReturn>(method, kMethodCallerName);
                if (cache.TryGetValue(key, out Delegate result)) return (MethodCaller<TTarget, TReturn>) result;

                return GenDelegateForMember<MethodCaller<TTarget, TReturn>, MethodInfo>(
                    method, key, kMethodCallerName, GenMethodInvocation<TTarget>,
                    typeof(TReturn), typeof(TTarget), typeof(object[]));
            }

            /// <summary>
            /// Generates a weakly-typed open-instance delegate to invoke the specified method
            /// </summary>
            public static MethodCaller<object, object> DelegateForCall(MethodInfo method)
            {
                return DelegateForCall<object, object>(method);
            }

            /// <summary>
            /// Executes the delegate on the specified target and arguments but only if it's not null
            /// </summary>
            public static void SafeInvoke<TTarget, TValue>(MethodCaller<TTarget, TValue> caller, TTarget target, params object[] args)
            {
                if (caller != null) caller(target, args);
            }

            /// <summary>
            /// Executes the delegate on the specified target and value but only if it's not null
            /// </summary>
            public static void SafeInvoke<TTarget, TValue>(MemberSetter<TTarget, TValue> setter, ref TTarget target, TValue value)
            {
                if (setter != null) setter(ref target, value);
            }

            /// <summary>
            /// Executes the delegate on the specified target only if it's not null, returns default(TReturn) otherwise
            /// </summary>
            public static TReturn SafeInvoke<TTarget, TReturn>(MemberGetter<TTarget, TReturn> getter, TTarget target)
            {
                if (getter != null) return getter(target);
                return default(TReturn);
            }

            static int GetKey<T, R>(MemberInfo member, string dynMethodName)
            {
                return member.GetHashCode() ^ dynMethodName.GetHashCode() ^ typeof(T).GetHashCode() ^ typeof(R).GetHashCode();
            }

            static TDelegate GenDelegateForMember<TDelegate, TMember>(TMember member, int key, string dynMethodName,
                Action<TMember> generator, Type returnType, params Type[] paramTypes)
                where TMember : MemberInfo
                where TDelegate : class
            {
                DynamicMethod dynMethod = new DynamicMethod(dynMethodName, returnType, paramTypes, true);

                emit.il = dynMethod.GetILGenerator();
                generator(member);

                Delegate result = dynMethod.CreateDelegate(typeof(TDelegate));
                cache[key] = result;
                return (TDelegate) (object) result;
            }

            static void GenCtor<T>(Type type, Type[] paramTypes)
            {
                // arg0: object[] arguments
                // goal: return new T(arguments)
                Type targetType = typeof(T) == typeof(object) ? type : typeof(T);

                ConstructorInfo ctor = targetType.GetConstructor(paramTypes);
                if (ctor == null)
                {
                    throw new Exception("Generating constructor: " +
                                        (paramTypes.Length == 0
                                            ? "No empty constructor found!"
                                            : "No constructor found that matches the following parameter types: " +
                                              string.Join(",", paramTypes.Select(x => x.Name).ToArray())));
                }

                if (targetType.IsValueType && paramTypes.Length == 0)
                {
                    LocalBuilder tmp = emit.declocal(targetType);
                    emit.ldloca(tmp)
                        .initobj(targetType)
                        .ldloc(0);
                }
                else
                {
                    // push parameters in order to then call ctor
                    for (int i = 0, imax = paramTypes.Length; i < imax; i++)
                    {
                        emit.ldarg0() // push args array
                            .ldc_i4(i) // push index
                            .ldelem_ref() // push array[index]
                            .unbox_any(paramTypes[i]); // cast
                    }

                    emit.newobj(ctor);
                }

                if (typeof(T) == typeof(object) && targetType.IsValueType) emit.box(targetType);

                emit.ret();
            }

            static void GenMethodInvocation<TTarget>(MethodInfo method)
            {
                var weaklyTyped = typeof(TTarget) == typeof(object);

                // push target if not static (instance-method. in that case first arg is always 'this')
                if (! method.IsStatic)
                {
                    Type targetType = weaklyTyped ? method.DeclaringType : typeof(TTarget);
                    emit.declocal(targetType);
                    emit.ldarg0();
                    if (weaklyTyped) emit.unbox_any(targetType);
                    emit.stloc0()
                        .ifclass_ldloc_else_ldloca(0, targetType);
                }

                // push arguments in order to call method
                ParameterInfo[] prams = method.GetParameters();
                for (int i = 0, imax = prams.Length; i < imax; i++)
                {
                    emit.ldarg1() // push array
                        .ldc_i4(i) // push index
                        .ldelem_ref(); // pop array, index and push array[index]

                    ParameterInfo param = prams[i];
                    Type dataType = param.ParameterType;

                    if (dataType.IsByRef) dataType = dataType.GetElementType();

                    LocalBuilder tmp = emit.declocal(dataType);
                    emit.unbox_any(dataType)
                        .stloc(tmp)
                        .ifbyref_ldloca_else_ldloc(tmp, param.ParameterType);
                }

                // perform the correct call (pushes the result)
                emit.callorvirt(method);

                // if method wasn't static that means we declared a temp local to load the target
                // that means our local variables index for the arguments start from 1
                int localVarStart = method.IsStatic ? 0 : 1;
                for (int i = 0; i < prams.Length; i++)
                {
                    Type paramType = prams[i].ParameterType;
                    if (! paramType.IsByRef) continue;
                    Type byRefType = paramType.GetElementType();
                    emit.ldarg1()
                        .ldc_i4(i)
                        .ldloc(i + localVarStart);
                    if (byRefType.IsValueType) emit.box(byRefType);
                    emit.stelem_ref();
                }

                if (method.ReturnType == typeof(void))
                {
                    emit.ldnull();
                }
                else if (weaklyTyped) emit.ifvaluetype_box(method.ReturnType);

                emit.ret();
            }

            static void GenFieldGetter<TTarget>(FieldInfo field)
            {
                GenMemberGetter<TTarget>(field, field.FieldType, field.IsStatic,
                    (e, f) => e.lodfld((FieldInfo) f)
                );
            }

            static void GenPropertyGetter<TTarget>(PropertyInfo property)
            {
                GenMemberGetter<TTarget>(property, property.PropertyType,
                    property.GetGetMethod(true).IsStatic,
                    (e, p) => e.callorvirt(((PropertyInfo) p).GetGetMethod(true))
                );
            }

            static void GenMemberGetter<TTarget>(MemberInfo member, Type memberType, bool isStatic, Action<ILEmitter, MemberInfo> get)
            {
                if (typeof(TTarget) == typeof(object)) // weakly-typed?
                {
                    // if we're static immediately load member and return value
                    // otherwise load and cast target, get the member value and box it if neccessary:
                    // return ((DeclaringType)target).member;
                    if (! isStatic)
                    {
                        emit.ldarg0()
                            .unboxorcast(member.DeclaringType);
                    }
                    emit.perform(get, member)
                        .ifvaluetype_box(memberType);
                }
                else // we're strongly-typed, don't need any casting or boxing
                {
                    // if we're static return member value immediately
                    // otherwise load target and get member value immeidately
                    // return target.member;
                    if (! isStatic) emit.ifclass_ldarg_else_ldarga(0, typeof(TTarget));
                    emit.perform(get, member);
                }

                emit.ret();
            }

            static void GenFieldSetter<TTarget>(FieldInfo field)
            {
                GenMemberSetter<TTarget>(field, field.FieldType, field.IsStatic,
                    (e, f) => e.setfld((FieldInfo) f)
                );
            }

            static void GenPropertySetter<TTarget>(PropertyInfo property)
            {
                GenMemberSetter<TTarget>(property, property.PropertyType,
                    property.GetSetMethod(true).IsStatic, (e, p) =>
                        e.callorvirt(((PropertyInfo) p).GetSetMethod(true))
                );
            }

            static void GenMemberSetter<TTarget>(MemberInfo member, Type memberType, bool isStatic, Action<ILEmitter, MemberInfo> set)
            {
                Type targetType = typeof(TTarget);
                var stronglyTyped = targetType != typeof(object);

                // if we're static set member immediately
                if (isStatic)
                {
                    emit.ldarg1();
                    if (! stronglyTyped) emit.unbox_any(memberType);
                    emit.perform(set, member)
                        .ret();
                    return;
                }

                if (stronglyTyped)
                {
                    // push target and value argument, set member immediately
                    // target.member = value;
                    emit.ldarg0()
                        .ifclass_ldind_ref(targetType)
                        .ldarg1()
                        .perform(set, member)
                        .ret();
                    return;
                }

                // we're weakly-typed
                targetType = member.DeclaringType;
                if (! targetType.IsValueType) // are we a reference-type?
                {
                    // load and cast target, load and cast value and set
                    // ((TargetType)target).member = (MemberType)value;
                    emit.ldarg0()
                        .ldind_ref()
                        .cast(targetType)
                        .ldarg1()
                        .unbox_any(memberType)
                        .perform(set, member)
                        .ret();
                    return;
                }

                // we're a value-type
                // handle boxing/unboxing for the user so he doesn't have to do it himself
                // here's what we're basically generating (remember, we're weakly typed, so
                // the target argument is of type object here):
                // TargetType tmp = (TargetType)target; // unbox
                // tmp.member = (MemberField)value;		// set member value
                // target = tmp;						// box back

                emit.declocal(targetType);
                emit.ldarg0()
                    .ldind_ref()
                    .unbox_any(targetType)
                    .stloc0()
                    .ldloca(0)
                    .ldarg1()
                    .unbox_any(memberType)
                    .perform(set, member)
                    .ldarg0()
                    .ldloc0()
                    .box(targetType)
                    .stind_ref()
                    .ret();
            }

            private class ILEmitter
            {
                public ILGenerator il;

                public ILEmitter ret()
                {
                    this.il.Emit(OpCodes.Ret);
                    return this;
                }

                public ILEmitter cast(Type type)
                {
                    this.il.Emit(OpCodes.Castclass, type);
                    return this;
                }

                public ILEmitter box(Type type)
                {
                    this.il.Emit(OpCodes.Box, type);
                    return this;
                }

                public ILEmitter unbox_any(Type type)
                {
                    this.il.Emit(OpCodes.Unbox_Any, type);
                    return this;
                }

                public ILEmitter unbox(Type type)
                {
                    this.il.Emit(OpCodes.Unbox, type);
                    return this;
                }

                public ILEmitter call(MethodInfo method)
                {
                    this.il.Emit(OpCodes.Call, method);
                    return this;
                }

                public ILEmitter callvirt(MethodInfo method)
                {
                    this.il.Emit(OpCodes.Callvirt, method);
                    return this;
                }

                public ILEmitter ldnull()
                {
                    this.il.Emit(OpCodes.Ldnull);
                    return this;
                }

                public ILEmitter bne_un(Label target)
                {
                    this.il.Emit(OpCodes.Bne_Un, target);
                    return this;
                }

                public ILEmitter beq(Label target)
                {
                    this.il.Emit(OpCodes.Beq, target);
                    return this;
                }

                public ILEmitter ldc_i4_0()
                {
                    this.il.Emit(OpCodes.Ldc_I4_0);
                    return this;
                }

                public ILEmitter ldc_i4_1()
                {
                    this.il.Emit(OpCodes.Ldc_I4_1);
                    return this;
                }

                public ILEmitter ldc_i4(int c)
                {
                    this.il.Emit(OpCodes.Ldc_I4, c);
                    return this;
                }

                public ILEmitter ldarg0()
                {
                    this.il.Emit(OpCodes.Ldarg_0);
                    return this;
                }

                public ILEmitter ldarg1()
                {
                    this.il.Emit(OpCodes.Ldarg_1);
                    return this;
                }

                public ILEmitter ldarg2()
                {
                    this.il.Emit(OpCodes.Ldarg_2);
                    return this;
                }

                public ILEmitter ldarga(int idx)
                {
                    this.il.Emit(OpCodes.Ldarga, idx);
                    return this;
                }

                public ILEmitter ldarga_s(int idx)
                {
                    this.il.Emit(OpCodes.Ldarga_S, idx);
                    return this;
                }

                public ILEmitter ldarg(int idx)
                {
                    this.il.Emit(OpCodes.Ldarg, idx);
                    return this;
                }

                public ILEmitter ldarg_s(int idx)
                {
                    this.il.Emit(OpCodes.Ldarg_S, idx);
                    return this;
                }

                public ILEmitter ifclass_ldind_ref(Type type)
                {
                    if (! type.IsValueType) this.il.Emit(OpCodes.Ldind_Ref);
                    return this;
                }

                public ILEmitter ldloc0()
                {
                    this.il.Emit(OpCodes.Ldloc_0);
                    return this;
                }

                public ILEmitter ldloc1()
                {
                    this.il.Emit(OpCodes.Ldloc_1);
                    return this;
                }

                public ILEmitter ldloc2()
                {
                    this.il.Emit(OpCodes.Ldloc_2);
                    return this;
                }

                public ILEmitter ldloca_s(int idx)
                {
                    this.il.Emit(OpCodes.Ldloca_S, idx);
                    return this;
                }

                public ILEmitter ldloca_s(LocalBuilder local)
                {
                    this.il.Emit(OpCodes.Ldloca_S, local);
                    return this;
                }

                public ILEmitter ldloc_s(int idx)
                {
                    this.il.Emit(OpCodes.Ldloc_S, idx);
                    return this;
                }

                public ILEmitter ldloc_s(LocalBuilder local)
                {
                    this.il.Emit(OpCodes.Ldloc_S, local);
                    return this;
                }

                public ILEmitter ldloca(int idx)
                {
                    this.il.Emit(OpCodes.Ldloca, idx);
                    return this;
                }

                public ILEmitter ldloca(LocalBuilder local)
                {
                    this.il.Emit(OpCodes.Ldloca, local);
                    return this;
                }

                public ILEmitter ldloc(int idx)
                {
                    this.il.Emit(OpCodes.Ldloc, idx);
                    return this;
                }

                public ILEmitter ldloc(LocalBuilder local)
                {
                    this.il.Emit(OpCodes.Ldloc, local);
                    return this;
                }

                public ILEmitter initobj(Type type)
                {
                    this.il.Emit(OpCodes.Initobj, type);
                    return this;
                }

                public ILEmitter newobj(ConstructorInfo ctor)
                {
                    this.il.Emit(OpCodes.Newobj, ctor);
                    return this;
                }

                public ILEmitter Throw()
                {
                    this.il.Emit(OpCodes.Throw);
                    return this;
                }

                public ILEmitter throw_new(Type type)
                {
                    ConstructorInfo exp = type.GetConstructor(Type.EmptyTypes);
                    newobj(exp).Throw();
                    return this;
                }

                public ILEmitter stelem_ref()
                {
                    this.il.Emit(OpCodes.Stelem_Ref);
                    return this;
                }

                public ILEmitter ldelem_ref()
                {
                    this.il.Emit(OpCodes.Ldelem_Ref);
                    return this;
                }

                public ILEmitter ldlen()
                {
                    this.il.Emit(OpCodes.Ldlen);
                    return this;
                }

                public ILEmitter stloc(int idx)
                {
                    this.il.Emit(OpCodes.Stloc, idx);
                    return this;
                }

                public ILEmitter stloc_s(int idx)
                {
                    this.il.Emit(OpCodes.Stloc_S, idx);
                    return this;
                }

                public ILEmitter stloc(LocalBuilder local)
                {
                    this.il.Emit(OpCodes.Stloc, local);
                    return this;
                }

                public ILEmitter stloc_s(LocalBuilder local)
                {
                    this.il.Emit(OpCodes.Stloc_S, local);
                    return this;
                }

                public ILEmitter stloc0()
                {
                    this.il.Emit(OpCodes.Stloc_0);
                    return this;
                }

                public ILEmitter stloc1()
                {
                    this.il.Emit(OpCodes.Stloc_1);
                    return this;
                }

                public ILEmitter mark(Label label)
                {
                    this.il.MarkLabel(label);
                    return this;
                }

                public ILEmitter ldfld(FieldInfo field)
                {
                    this.il.Emit(OpCodes.Ldfld, field);
                    return this;
                }

                public ILEmitter ldsfld(FieldInfo field)
                {
                    this.il.Emit(OpCodes.Ldsfld, field);
                    return this;
                }

                public ILEmitter lodfld(FieldInfo field)
                {
                    if (field.IsStatic)
                    {
                        ldsfld(field);
                    }
                    else
                    {
                        ldfld(field);
                    }
                    return this;
                }

                public ILEmitter ifvaluetype_box(Type type)
                {
                    if (type.IsValueType) this.il.Emit(OpCodes.Box, type);
                    return this;
                }

                public ILEmitter stfld(FieldInfo field)
                {
                    this.il.Emit(OpCodes.Stfld, field);
                    return this;
                }

                public ILEmitter stsfld(FieldInfo field)
                {
                    this.il.Emit(OpCodes.Stsfld, field);
                    return this;
                }

                public ILEmitter setfld(FieldInfo field)
                {
                    if (field.IsStatic)
                    {
                        stsfld(field);
                    }
                    else
                    {
                        stfld(field);
                    }
                    return this;
                }

                public ILEmitter unboxorcast(Type type)
                {
                    if (type.IsValueType)
                    {
                        unbox(type);
                    }
                    else
                    {
                        cast(type);
                    }
                    return this;
                }

                public ILEmitter callorvirt(MethodInfo method)
                {
                    if (method.IsVirtual)
                    {
                        this.il.Emit(OpCodes.Callvirt, method);
                    }
                    else
                    {
                        this.il.Emit(OpCodes.Call, method);
                    }
                    return this;
                }

                public ILEmitter stind_ref()
                {
                    this.il.Emit(OpCodes.Stind_Ref);
                    return this;
                }

                public ILEmitter ldind_ref()
                {
                    this.il.Emit(OpCodes.Ldind_Ref);
                    return this;
                }

                public LocalBuilder declocal(Type type)
                {
                    return this.il.DeclareLocal(type);
                }

                public Label deflabel()
                {
                    return this.il.DefineLabel();
                }

                public ILEmitter ifclass_ldarg_else_ldarga(int idx, Type type)
                {
                    if (type.IsValueType)
                    {
                        emit.ldarga(idx);
                    }
                    else
                    {
                        emit.ldarg(idx);
                    }
                    return this;
                }

                public ILEmitter ifclass_ldloc_else_ldloca(int idx, Type type)
                {
                    if (type.IsValueType)
                    {
                        emit.ldloca(idx);
                    }
                    else
                    {
                        emit.ldloc(idx);
                    }
                    return this;
                }

                public ILEmitter perform(Action<ILEmitter, MemberInfo> action, MemberInfo member)
                {
                    action(this, member);
                    return this;
                }

                public ILEmitter ifbyref_ldloca_else_ldloc(LocalBuilder local, Type type)
                {
                    if (type.IsByRef)
                    {
                        ldloca(local);
                    }
                    else
                    {
                        ldloc(local);
                    }
                    return this;
                }
            }
        }
    }
}
#endif