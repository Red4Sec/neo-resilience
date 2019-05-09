using System;
using System.ComponentModel;
using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;

namespace Neo.SmartContract
{
    /// <summary>
    /// This is a test contract, please don't use it because could be insecure
    /// </summary>
    public class FakeNEP5 : Framework.SmartContract
    {
        public static readonly byte[] Owner = "AXnF3C8JW8Qz3M1Ua4mFdz8Def7ozrQJ5u".ToScriptHash();

        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> OnTransfer;

        public static object Main(string operation, params object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                return Runtime.CheckWitness(Owner);
            }

            if (operation == "transfer")
            {
                if (args.Length != 2) return false;

                byte[] to = (byte[])args[0];
                BigInteger value = (BigInteger)args[1];

                return Transfer(Owner, to, value);
            }
            else
            {
                if (operation == "mint")
                {
                    if (args.Length != 2) return false;

                    byte[] to = (byte[])args[0];
                    BigInteger value = (BigInteger)args[1];

                    return Mint(to, value);
                }
            }

            return operation;
        }

        /// <summary>
        /// Check if one address is a valid one
        /// </summary>
        /// <param name="address">Address</param>
        /// <returns>True or False</returns>
        public static bool IsValidAddress(byte[] address)
        {
            if (address.Length != 20) return false;

            return true;
        }

        /// <summary>
        /// Mint
        /// </summary>
        /// <param name="from">From</param>
        /// <param name="to">To</param>
        /// <param name="value">Value</param>
        /// <returns>True or False</returns>
        public static bool Mint(byte[] to, BigInteger value)
        {
            if (value <= 0) return false;

            if (!IsValidAddress(to)) return false;
            if (!Runtime.CheckWitness(to)) return false;

            BigInteger balance = Storage.Get(Storage.CurrentContext, to).AsBigInteger();
            Storage.Put(Storage.CurrentContext, to, balance + value);

            OnTransfer(null, to, value);
            return true;
        }

        /// <summary>
        /// Fake transfer with a mint behaviour
        /// </summary>
        /// <param name="from">From</param>
        /// <param name="to">To</param>
        /// <param name="value">Value</param>
        /// <returns>True or False</returns>
        public static bool Transfer(byte[] from, byte[] to, BigInteger value)
        {
            if (value <= 0) return false;

            if (!IsValidAddress(from)) return false;
            if (!IsValidAddress(to)) return false;
            // For testing purpose we can't check the from's signature
            if (!Runtime.CheckWitness(to/*from*/)) return false;

            BigInteger balance = Storage.Get(Storage.CurrentContext, to).AsBigInteger();
            Storage.Put(Storage.CurrentContext, to, balance + value);

            OnTransfer(from, to, value);
            return true;
        }
    }
}