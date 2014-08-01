using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;

namespace Serilog.Reflection
{
    static class TypePropertyCache
    {
        static ReaderWriterLockSlim _locker = new ReaderWriterLockSlim();
        static Dictionary<RuntimeTypeHandle, List<PropertyAccessor>> _dictionary = new Dictionary<RuntimeTypeHandle, List<PropertyAccessor>>();

        public static List<PropertyAccessor> GetCachedProperties(this Type type)
        {
            try
            {
                _locker.EnterUpgradeableReadLock();
                List<PropertyAccessor> value;
                if (_dictionary.TryGetValue(type.TypeHandle, out value))
                {
                    return value;
                }

                value = type.GetPropertiesRecursive()
                    .Select(GetPropertyAccessor)
                    .ToList();
                _locker.EnterWriteLock();
                try
                {
                    _dictionary.Add(type.TypeHandle, value);
                }
                finally
                {
                    _locker.ExitWriteLock();
                }
                return value;
            }
            finally
            {
                _locker.ExitUpgradeableReadLock();
            }
        }


        internal static PropertyAccessor GetPropertyAccessor(this PropertyInfo propertyInfo)
        {
            return new PropertyAccessor
            {
                Name = propertyInfo.Name,
                GetDelegate = propertyInfo.GetGetMethodByExpression()
            };
        }

        public static Func<object, object> GetGetMethodByExpression(this PropertyInfo propertyInfo)
        {
            var getMethodInfo = propertyInfo.GetMethod;
            var instance = Expression.Parameter(typeof(object), "instance");

            var instanceCast = Expression.Convert(instance, propertyInfo.DeclaringType);

            var callExpression = Expression.Convert(Expression.Call(instanceCast, getMethodInfo), typeof(object));
            return Expression.Lambda<Func<object, object>>(callExpression, instance)
                .Compile();
        }
    }
}