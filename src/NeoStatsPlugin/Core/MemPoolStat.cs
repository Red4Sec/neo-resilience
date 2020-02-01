using NeoStatsPlugin.Extensions;

namespace NeoStatsPlugin.Core
{
    public class MemPoolStat
    {
        /// <summary>
        /// Count
        /// </summary>
        public int Count => UnVerified + Verified;

        /// <summary>
        /// UnVerified Count
        /// </summary>
        public int UnVerified { get; set; } = 0;

        /// <summary>
        /// Verified Count
        /// </summary>
        public int Verified { get; set; } = 0;

        /// <summary>
        /// Capacity
        /// </summary>
        public int Capacity { get; set; } = 0;

        /// <summary>
        /// String representation
        /// </summary>
        /// <returns></returns>
        public override string ToString() => this.ToJson();
    }
}
