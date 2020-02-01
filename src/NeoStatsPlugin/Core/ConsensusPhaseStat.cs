using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using NeoStatsPlugin.Extensions;
using System.Collections.Generic;

namespace NeoStatsPlugin.Core
{
    public class ConsensusPhaseStat
    {
        private readonly List<string> _hashes = new List<string>();

        /// <summary>
        /// Messages
        /// </summary>
        public ConsensusPhaseCount Messages { get; } = new ConsensusPhaseCount();

        /// <summary>
        /// String representation
        /// </summary>
        /// <returns></returns>
        public override string ToString() => this.ToJson();

        /// <summary>
        /// Add payload
        /// </summary>
        /// <param name="payload">Payload</param>
        public void Add(ConsensusPayload payload)
        {
            if (!Messages.ValidatorIndexes.Contains(payload.ValidatorIndex))
            {
                Messages.ValidatorIndexes.Add(payload.ValidatorIndex);
            }

            if (_hashes.Contains(payload.Hash.ToString()))
            {
                Messages.Duplicated++;
            }
            else
            {
                _hashes.Add(payload.Hash.ToString());
            }

            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                if (payload.Verify(snapshot))
                {
                    Messages.Valid++;
                }
                else
                {
                    Messages.Invalid++;
                }
            }
        }
    }
}
