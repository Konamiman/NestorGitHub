using System;
using System.Collections.Generic;
using System.Linq;

namespace Konamiman.NestorGithub
{
    static class GeneralExtensions
    {
        public static string JoinCommaSeparated(this IEnumerable<string> strings) => string.Join(", ", strings.ToArray());

        public static string JoinInLines(this IEnumerable<string> strings) => string.Join("\r\n", strings.ToArray());

        public static string JoinWithSpaces(this IEnumerable<string> strings) => string.Join(" ", strings.ToArray());

        private static readonly string[] newLineAsArray = new[] { "\r\n" };

        private static readonly string[] spaceAsArray = new[] { " " };

        private static readonly string[] slashAsArray = new[] { "/" };

        public static string[] SplitInLines(this string value, bool removeEmptyEntries = true) =>
            value.Split(newLineAsArray, removeEmptyEntries ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.None);

        public static string[] SplitBySpace(this string value) =>
            value.Split(spaceAsArray,StringSplitOptions.RemoveEmptyEntries);

        public static string[] SplitBySpace(this string value, int count) =>
            value.Split(spaceAsArray, count, StringSplitOptions.RemoveEmptyEntries);

        public static string[] SplitBySlash(this string value) =>
            value.Split(slashAsArray, StringSplitOptions.RemoveEmptyEntries);
    }
}
