using System;
using System.Collections.Generic;

namespace AmlabParser3
{
    public class ConsoleHelper
    {
        private static ConsoleHelper _default;
        public static ConsoleHelper Default
        {
            get
            {
                if (_default == null)
                {
                    _default = new ConsoleHelper();
                }
                return _default;
            }
        }
        private static object _lock = new object();
        private void WriteLines(IEnumerable<string> texts, ConsoleColor color, bool pressAnyKeyToContinue)
        {
            lock (_lock)
            {
                ConsoleColor restoreColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                foreach (string text in texts)
                {
                    Console.WriteLine(text);
                }
                Console.ForegroundColor = restoreColor;
                if (pressAnyKeyToContinue)
                {
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey();
                }
            }
        }
        public void WriteInfoLines(params string[] texts)
        {
            this.WriteLines(texts, ConsoleColor.White, false);
        }
        public void WriteInfoLines(bool pressAnyKeyToContinue, params string[] texts)
        {
            this.WriteLines(texts, ConsoleColor.White, pressAnyKeyToContinue);
        }
        public void WriteErrorLines(params string[] texts)
        {
            this.WriteLines(texts, ConsoleColor.DarkRed, false);
        }
        public void WriteErrorLines(bool pressAnyKeyToContinue, params string[] texts)
        {
            this.WriteLines(texts, ConsoleColor.DarkRed, pressAnyKeyToContinue);
        }
    }
}