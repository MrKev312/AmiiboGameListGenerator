using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmiiboGameList
{
    internal class Debugger
    {
        public static DebugLevel CurrentDebugLevel;
        public enum DebugLevel
        {
            Verbose,
            Info,
            Warn,
            Error
        }

        public static void Log(string Message, DebugLevel Severity)
        {
            if (Severity >= CurrentDebugLevel)
            {
                switch (Severity)
                {
                    case DebugLevel.Verbose:
                        Console.ForegroundColor = ConsoleColor.Gray;
                        break;
                    case DebugLevel.Info:
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                    case DebugLevel.Warn:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;
                    case DebugLevel.Error:
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                    default:
                        break;
                }
                Console.WriteLine(Message);
            }
        }
    }
}
