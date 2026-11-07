using System;
namespace KeyMind
{
    internal static class Program
    {
        public static void LogError(Exception e) { Console.Error.WriteLine(e.Message); }
    }
}
