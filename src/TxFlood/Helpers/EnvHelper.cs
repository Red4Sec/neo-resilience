using System;

namespace Neo.Plugins.Helpers
{
    public static class EnvHelper
    {
        public static bool UpdateEnvVar(ref int val, string varName)
        {
            var value = Environment.GetEnvironmentVariable(varName);
            if (string.IsNullOrEmpty(value)) return false;
            if (!int.TryParse(value, out var intValue)) return false;
            if (intValue == val) return false;

            val = intValue;
            return true;
        }

        public static bool UpdateEnvVar(ref string val, string varName)
        {
            var value = Environment.GetEnvironmentVariable(varName);
            if (string.IsNullOrEmpty(value)) return false;
            if (value == val) return false;

            val = value;
            return true;
        }
    }
}
