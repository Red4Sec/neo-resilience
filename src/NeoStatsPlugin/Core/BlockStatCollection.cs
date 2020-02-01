using System;
using System.Collections.Concurrent;
using System.Linq;
using Newtonsoft.Json;

namespace NeoStatsPlugin.Core
{
    public class BlockStatCollection
    {
        /// <summary>
        /// Title
        /// </summary>
        public string Title { get; set; } = "";

        /// <summary>
        /// Blocks
        /// </summary>
        public ConcurrentDictionary<uint, BlockStat> Blocks { get; } = new ConcurrentDictionary<uint, BlockStat>();

        /// <summary>
        /// Constructor
        /// </summary>
        public BlockStatCollection() { }

        /// <summary>
        /// Compute average tx per block
        /// </summary>
        /// <param name="minTxPerBlock"></param>
        /// <param name="maxTxPerBlock"></param>
        /// <param name="avgTxPerBlock"></param>
        public void ComputeAverageTxPerBlock(out object minTxPerBlock, out object maxTxPerBlock, out object avgTxPerBlock)
        {
            maxTxPerBlock = Blocks.Values.Select(u => u.Transactions.Count).Max();
            minTxPerBlock = Blocks.Values.Select(u => u.Transactions.Count).Min();
            avgTxPerBlock = Blocks.Values.Select(u => u.Transactions.Count).Sum(u => u) / (Blocks.Count + 0.0);
        }

        /// <summary>
        /// Compute average times per block
        /// </summary>
        /// <param name="minTimePerBlock"></param>
        /// <param name="maxTimePerBlock"></param>
        /// <param name="avgTimePerBlock"></param>
        public void ComputeAverageTimePerBlock(out TimeSpan minTimePerBlock, out TimeSpan maxTimePerBlock, out TimeSpan avgTimePerBlock)
        {
            maxTimePerBlock = Blocks.Values.Select(u => u.ElapsedTime).Max();
            minTimePerBlock = Blocks.Values.Select(u => u.ElapsedTime).Min();
            avgTimePerBlock = TimeSpan.FromMilliseconds(Blocks.Values.Select(u => u.ElapsedTime).Sum(u => u.TotalMilliseconds) / (Blocks.Count + 0.0));
        }

        /// <summary>
        /// Convert from json
        /// </summary>
        /// <param name="json">Json</param>
        /// <returns>Return BlockStats</returns>
        public static BlockStatCollection FromJson(string json)
        {
            return JsonConvert.DeserializeObject<BlockStatCollection>(json);
        }

        /// <summary>
        /// Convert to json
        /// </summary>
        /// <param name="indented">Indented</param>
        /// <returns>Returns json</returns>
        public string ToJson(bool indented = true)
        {
            return JsonConvert.SerializeObject(this, indented ? Formatting.Indented : Formatting.None);
        }

        /// <summary>
        /// Convert from block array
        /// </summary>
        /// <param name="json">Json</param>
        public static BlockStatCollection FromBlockArray(string json)
        {
            var ret = new BlockStatCollection();
            var blocks = JsonConvert.DeserializeObject<BlockStat[]>(json);

            foreach(var block in blocks)
            {
                ret.Blocks[block.Index] = block;
            }

            return ret;
        }

        /// <summary>
        /// String representation
        /// </summary>
        /// <returns></returns>
        public override string ToString() => this.ToJson();
    }
}
