using System;
using System.Collections.Generic;
using System.Text;

namespace FluentSettings.Extensions
{
    public static class TypeExtensions
    {
        /// <summary>
        /// Возвращает пространство имён на один уровень выше. 
        /// Если нет точек – возвращает само пространство.
        /// </summary>
        public static string GetParentNamespace(this Type type)
        {
            var ns = type.Namespace;
            if (string.IsNullOrEmpty(ns))
                return ns;

            var lastDot = ns.LastIndexOf('.');
            return lastDot > 0
                ? ns.Substring(0, lastDot)
                : ns;
        }
    }
}
