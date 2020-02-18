using System;

namespace Neo.Plugins.Helpers
{
    public static class LogHelper
    {
        public static void Debug(string input)
        {
#if DEBUG
            Console.WriteLine(input);
#endif
        }
    }
}
