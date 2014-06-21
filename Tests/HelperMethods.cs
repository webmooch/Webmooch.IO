using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
    internal class HelperMethods
    {
        private static readonly Random random = new Random();
        public static byte[] GenerateJunkByteArray(int length)
        {
            var arr = new Byte[length];
            random.NextBytes(arr);
            return arr;
        }
    }
}