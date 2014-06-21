using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Webmooch.IO
{
    internal static class Extensions
    {
        public static string Replace(this string str, string oldValue, string newValue, StringComparison comparison)
        {
            if (string.IsNullOrEmpty(oldValue))
                throw new ArgumentNullException("Old value cannot be null or empty.");

            if (string.IsNullOrEmpty(newValue))
                throw new ArgumentNullException("New value cannot be null or empty.");

            if (string.IsNullOrEmpty(str))
                throw new ArgumentNullException("Str value cannot be null or empty.");

            var sb = new StringBuilder();
            var previousIndex = 0;
            var index = str.IndexOf(oldValue, comparison);

            if (index == -1)
                return str;

            while (index != -1)
            {
                sb.Append(str.Substring(previousIndex, index - previousIndex));
                sb.Append(newValue);
                index += oldValue.Length;

                previousIndex = index;
                index = str.IndexOf(oldValue, index, comparison);
            }
            sb.Append(str.Substring(previousIndex));

            return sb.ToString();
        }

        public static bool Contains(this string valueToBeSearched, string searchFor, StringComparison comparison)
        {
            if (string.IsNullOrEmpty(valueToBeSearched))
                throw new ArgumentException("ValueToBeSearched cannot be null or empty.");

            if (string.IsNullOrEmpty(searchFor))
                throw new ArgumentException("SearchFor cannot be null or empty.");

            return valueToBeSearched.IndexOf(searchFor, comparison) > -1;
        }

        public static bool IsCritical(this Exception ex)
        {
            if (ex is OutOfMemoryException) return true;
            if (ex is AppDomainUnloadedException) return true;
            if (ex is BadImageFormatException) return true;
            if (ex is CannotUnloadAppDomainException) return true;
            //if (ex is ExecutionEngineException) return true;
            if (ex is InvalidProgramException) return true;
            if (ex is ThreadAbortException) return true;
            return false;
        }
    }
}