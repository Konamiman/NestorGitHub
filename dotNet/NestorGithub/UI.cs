using System;
using System.Linq;

namespace Konamiman.NestorGithub
{
    static class UI
    {
        public static void Print(string value)
        {
            Console.Write(value);
        }

        public static void PrintLine(string value)
        {
            Console.WriteLine(value);
        }

        public static ConsoleKey ReadKey(params ConsoleKey[] allowedValues)
        {
            while (true)
            {
                var keyInfo = Console.ReadKey();
                if(allowedValues.Contains(keyInfo.Key)) return keyInfo.Key;
            }
        }
    }
}
