using Neo.Consensus;
using Neo.Network.P2P.Payloads;
using NeoStatsPlugin.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NeoStatsPlugin.Core
{
    public class BlockStat
    {
        /// <summary>
        /// Consensus phases
        /// </summary>
        private readonly Dictionary<ConsensusMessageType, ConsensusPhaseStat> _consensus = new Dictionary<ConsensusMessageType, ConsensusPhaseStat>();

        /// <summary>
        /// Block index
        /// </summary>
        public uint Index { get; set; } = 0;

        /// <summary>
        /// Block hash
        /// </summary>
        public string Hash { get; set; } = "";

        /// <summary>
        /// Timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Size
        /// </summary>
        public long Size { get; set; } = 0;

        /// <summary>
        /// View number
        /// </summary>
        public byte ViewNumber { get; set; } = 0;

        /// <summary>
        /// Time between blocks
        /// </summary>
        public TimeSpan ElapsedTime { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Mem pool stats
        /// </summary>
        public MemPoolStat MemPool { get; } = new MemPoolStat();

        /// <summary>
        /// Transactions stats
        /// </summary>
        public TransactionStat Transactions { get; } = new TransactionStat();

        /// <summary>
        /// P2P stats
        /// </summary>
        public P2PStat P2P { get; } = new P2PStat();

        /// <summary>
        /// Storage Hash
        /// </summary>
        public string StorageHash { get; set; } = "";

        /// <summary>
        /// Consensus phases
        /// </summary>
        public IDictionary<ConsensusMessageType, ConsensusPhaseCount> Consensus => _consensus.ToDictionary(k => k.Key, v => v.Value.Messages);

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="index">Index</param>
        public BlockStat(uint index)
        {
            Index = index;
        }

        #region Consensus

        public void OnCommitReceived(ConsensusPayload payload, Commit commit)
        {
            if (!_consensus.TryGetValue(ConsensusMessageType.Commit, out var state))
            {
                state = new ConsensusPhaseStat();
            }

            state.Add(payload);
            ViewNumber = Math.Max(ViewNumber, commit.ViewNumber);
        }

        public void OnPrepareResponseReceived(ConsensusPayload payload, PrepareResponse response)
        {
            if (!_consensus.TryGetValue(ConsensusMessageType.Commit, out var state))
            {
                state = new ConsensusPhaseStat();
            }

            state.Add(payload);

        }

        public void OnPrepareRequestReceived(ConsensusPayload payload, PrepareRequest request)
        {
            if (!_consensus.TryGetValue(ConsensusMessageType.PrepareRequest, out var state))
            {
                state = new ConsensusPhaseStat();
            }

            state.Add(payload);
        }

        public void OnChangeViewReceived(ConsensusPayload payload, ChangeView view)
        {
            if (!_consensus.TryGetValue(ConsensusMessageType.ChangeView, out var state))
            {
                state = new ConsensusPhaseStat();
            }

            state.Add(payload);
        }

        public void OnRecoveryMessageReceived(ConsensusPayload payload, RecoveryMessage recovery)
        {
            if (!_consensus.TryGetValue(ConsensusMessageType.RecoveryMessage, out var state))
            {
                state = new ConsensusPhaseStat();
            }

            state.Add(payload);
        }

        #endregion

        /// <summary>
        /// String representation
        /// </summary>
        /// <returns></returns>
        public override string ToString() => this.ToJson();
    }
}
