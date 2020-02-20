using Akka.Actor;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins.Helpers;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    public class TxFlood : Plugin, IPersistencePlugin
    {
        private readonly Random _rand;
        private readonly Wallet Wallet;
        private readonly Type _sendDirectlyType;
        private readonly FieldInfo _sendDirectlyField;
        private readonly WalletAccount[] _sources, _destinations;

        private const string ENV_TASK_CONTROLLER = "NEO_TX_RUN";
        private const string WALLET_FILE = "wallet.json";
        private const string WALLET_PASS = "pass";

        private AssetDescriptor NEO, GAS;

        private Task _task;
        private Task _warmUpTask;
        private long _taskRun = 0;
        private bool _distribute = false;
        private Transaction _mintTransaction = null;
        private int SLEEP_START = 51_000;
        private int SLEEP_ROUND = 5_000;
        private int SLEEP_TX = 500;
        private string CONTRACT = "0x185072a45df4d002545db31157a8955baa39e11a";

        /// <summary>
        /// Constructor
        /// </summary>
        public TxFlood() : base()
        {
            _rand = new Random();

            // This is used for relay directly without enter in our mempool

            _sendDirectlyType = typeof(LocalNode).GetMembers(BindingFlags.NonPublic)
                .Where(u => u.Name == "SendDirectly")
                .Cast<Type>()
                .FirstOrDefault();

            _sendDirectlyField = _sendDirectlyType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(u => u.Name == "Inventory")
                .FirstOrDefault();

            // Open wallet

            Wallet = WalletHelper.OpenWallet(WALLET_FILE, WALLET_PASS);

            _sources = Wallet.GetAccounts().Skip(1).ToArray();
            _destinations = _sources.Skip(1).Concat(_sources.Take(1)).ToArray();

            // Warm up

            _warmUpTask = new Task(() => Parallel.ForEach(_sources, (a) => a.GetKey()));
            _warmUpTask.Start();
        }

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        private bool InitWallet()
        {
            if (Wallet == null)
            {
                Console.WriteLine("no wallet found");
                return false;
            }

            if (NEO != null)
            {
                return true;
            }

            // Open wallet

            NEO = new AssetDescriptor(NativeContract.NEO.Hash);
            GAS = new AssetDescriptor(NativeContract.GAS.Hash);

            return true;
        }

        void CreateMintTx()
        {
            if (!InitWallet())
            {
                return;
            }

            UInt160 to = Wallet.GetAccounts().First().ScriptHash;

            // Import all CN keys

            var wallet = new NEP6Wallet(JObject.Parse("{\"name\":\"name\",\"version\":\"3.0\",\"scrypt\":{\"n\":0,\"r\":0,\"p\":0},\"accounts\":[],\"extra\":{}}"));
            using var unlock = wallet.Unlock(WALLET_PASS);

            var cnWallets = Settings.Default.Wifs.Select(wif => wallet.Import(wif)).ToArray();

            // Get CN contract

            var m = Blockchain.StandbyValidators.Length / 2 + 1;
            // var m = Blockchain.StandbyValidators.Length - (Blockchain.StandbyValidators.Length - 1) / 3;
            var CNContract = Contract.CreateMultiSigContract(m, Blockchain.StandbyValidators);
            wallet.CreateAccount(CNContract);

            // Create TX

            var mintTx = wallet.MakeTransaction
                (
                new TransferOutput[]
                {
                    new TransferOutput()
                    {
                         AssetId = NEO.AssetId,
                         ScriptHash = to,
                         Value = new BigDecimal(100_000_000, 0),
                    },
                    new TransferOutput()
                    {
                         AssetId = GAS.AssetId,
                         ScriptHash = to,
                         Value = new BigDecimal(20_000_000_0000_0000, 8),
                    },
                },
                CNContract.ScriptHash
                );

            // Create context

            var context = new ContractParametersContext(mintTx);

            // Sign with all required CN

            foreach (var cnWallet in cnWallets)
            {
                var key = cnWallet.GetKey();
                var signature = context.Verifiable.Sign(key);

                context.AddSignature(CNContract, key.PublicKey, signature);
                if (context.Completed) break;
            }

            mintTx.Witnesses = context.GetWitnesses();

            // Relay

            _mintTransaction = mintTx;
            //var send = new LocalNode.Relay { Inventory = _mintTransaction };
            var send = Activator.CreateInstance(_sendDirectlyType);
            _sendDirectlyField.SetValue(send, _mintTransaction);
            System.LocalNode.Tell(send);
            System.LocalNode.Tell(new LocalNode.Relay() { Inventory = _mintTransaction });
        }

        public void OnCommit(StoreView snapshot)
        {
            // Check if we need to watch the blocks

            if (_mintTransaction == null)
            {
                if (_distribute)
                {
                    // Send distribute

                    Distribute("gas", new BigDecimal(20_000_0000_0000, 8));
                    Distribute("neo", new BigDecimal(20_000, 0));

                    _distribute = false;
                }

                return;
            }

            foreach (var tx in snapshot.PersistingBlock.Transactions)
            {
                // Check if the Mint transaction arrived

                if (tx.Hash == _mintTransaction.Hash)
                {
                    // Remove the watcher

                    _mintTransaction = null;

                    // Send distribute

                    _distribute = true;

                    return;
                }
            }

            // Send again in order to prevent a mempool bug

            var send = Activator.CreateInstance(_sendDirectlyType);
            _sendDirectlyField.SetValue(send, _mintTransaction);
            System.LocalNode.Tell(send);
        }

        protected override void OnPluginsLoaded()
        {
            if (Blockchain.Singleton.Height < 10)
            {
                try
                {
                    CreateMintTx();
                }
                catch { }
            }

            new Task(() =>
            {
                while (true)
                {
                    EnvHelper.UpdateEnvVar(ref SLEEP_START, ENV_TASK_CONTROLLER + "_SLEEP_START");
                    EnvHelper.UpdateEnvVar(ref SLEEP_ROUND, ENV_TASK_CONTROLLER + "_SLEEP_ROUND");
                    EnvHelper.UpdateEnvVar(ref SLEEP_TX, ENV_TASK_CONTROLLER + "_SLEEP_TX");
                    EnvHelper.UpdateEnvVar(ref CONTRACT, ENV_TASK_CONTROLLER + "_CONTRACT");

                    // Start stop

                    var flag = Environment.GetEnvironmentVariable(ENV_TASK_CONTROLLER);

                    if (!string.IsNullOrEmpty(flag))
                    {
                        if (flag.Equals("true", StringComparison.InvariantCultureIgnoreCase))
                        {
                            Launch();
                        }
                        else
                        {
                            Stop();
                        }
                    }

                    Thread.Sleep(5_000);
                }
            })
            .Start();
        }

        protected override bool OnMessage(object message)
        {
            if (!(message is string[] args)) return false;
            if (args.Length == 0) return false;

            switch (args[0].ToLowerInvariant())
            {
                case "stop": Stop(); return true;
                case "launch": Launch(); return true;
                case "balances": return Balances(args);
                case "distribute":
                    {
                        if (args.Length != 3) return false;
                        Distribute(args[2], BigDecimal.Parse(args[1], 0));
                        return true;
                    }
                case "collect":
                    {
                        if (args.Length != 2) return false;
                        Collect(args[1]);
                        return true;
                    }
                case "flood":
                    {
                        Flood();
                        return true;
                    }
                case "mint":
                    {
                        CreateMintTx();
                        return true;
                    }
            }

            return false;
        }

        public bool Launch()
        {
            if (_task?.Status == TaskStatus.Running)
            {
                LogHelper.Debug("Already running");
                return false;
            }

            Interlocked.Exchange(ref _taskRun, 1);
            _task = Task.Run(() =>
            {
                LogHelper.Debug("Start sender");

                Thread.Sleep(SLEEP_START);

                if (_warmUpTask?.IsCompleted == false)
                {
                    _warmUpTask.Wait();
                    _warmUpTask.Dispose();
                    _warmUpTask = null;
                }

                while (Interlocked.Read(ref _taskRun) == 1)
                {
                    Flood();
                    Thread.Sleep(SLEEP_ROUND);
                }
            });

            return _task.Status == TaskStatus.Running;
        }

        public bool Stop()
        {
            if (_task == null || _task.Status != TaskStatus.Running)
            {
                LogHelper.Debug("Already stoped");
                return false;
            }

            Interlocked.Exchange(ref _taskRun, 0);
            LogHelper.Debug("Stoping sender...");
            return _task?.Status != TaskStatus.Running;
        }

        private bool Balances(string[] args)
        {
            if (!InitWallet()) return false;

            if (args.Length > 1 && args[1].ToLower() == "all")
            {
                foreach (UInt160 account in Wallet.GetAccounts().Select(p => p.ScriptHash))
                {
                    var neo = Wallet.GetBalance(NativeContract.NEO.Hash, account);
                    var gas = Wallet.GetBalance(NativeContract.GAS.Hash, account);
                    if (neo.Value != 0 || gas.Value != 0)
                    {
                        Console.WriteLine(account.ToAddress() + "    " + "NEO: " + neo + "    GAS: " + gas);
                    }
                }
            }

            Console.WriteLine("Total" +
                "    NEO: " + Wallet.GetAvailable(NativeContract.NEO.Hash) +
                "    GAS: " + Wallet.GetAvailable(NativeContract.GAS.Hash));

            return true;
        }

        private bool Distribute(string asset, BigDecimal value)
        {
            if (!InitWallet()) return false;

            var org = Wallet.GetAccounts().First().Address.ToScriptHash();
            var dest = Wallet.GetAccounts().Skip(1).Select(d => d.Address.ToScriptHash()).ToArray();
            long fee = 0; // 300_000_000;

            return SendMany(asset.ToLowerInvariant() == "gas" ? GAS : NEO, org, dest, value, fee);
        }

        private bool Collect(string asset)
        {
            if (!InitWallet()) return false;

            var assetId = asset.ToLowerInvariant() == "gas" ? GAS : NEO;
            var dest = Wallet.GetAccounts().First().Address.ToScriptHash();
            var sources = Wallet.GetAccounts().Skip(1).Select(d => d.Address.ToScriptHash()).ToArray();
            long fee = 250_000_000;

            foreach (var dir in sources)
            {
                var balance = Wallet.GetBalance(assetId.AssetId, dir);

                if (assetId == GAS)
                {
                    var bi = BigInteger.Subtract(balance.Value, fee);
                    balance = new BigDecimal(bi, 8);
                };

                if (balance.Value > 0)
                {
                    Send(assetId, dir, dest, balance.ToString(), fee);
                }
            }
            return true;
        }

        private bool Flood()
        {
            if (!InitWallet()) return false;

            //var contract = UInt160.Parse(CONTRACT);
            Console.WriteLine();

            for (int i = 0; i < _sources.Length; i++)
            {
                if (Interlocked.Read(ref _taskRun) == 0) return true;

                var from = _sources[i];
                var to = _destinations[i];
                long fee = _rand.Next(600_000, 250_000_000);
                var option = _rand.Next(1, 3);

                switch (option)
                {
                    case 1:
                        {
                            // NEO
                            var neo = BigDecimal.Parse(_rand.Next(1, 5).ToString(), 0);
                            try
                            {
                                Send(NEO, from.ScriptHash, to.ScriptHash, neo.ToString(), fee);
                                Console.WriteLine($"  NEO - {from.Address} >> {to.Address} --  {neo}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"  {Blockchain.Singleton.Height}:NEO - {from.Address} >> {to.Address} --  {neo} [ERROR:{ex.ToString()}]");
                            }
                            break;
                        }
                    case 2:
                        {
                            // GAS
                            var gas = new BigDecimal(new BigInteger(_rand.Next(1, 5_0000_0000)), 8);
                            try
                            {
                                Send(GAS, from.ScriptHash, to.ScriptHash, gas.ToString(), fee);
                                Console.WriteLine($"  GAS - {from.Address} >> {to.Address} --  {gas}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"  {Blockchain.Singleton.Height}:GAS - {from.Address} >> {to.Address} --  {gas} [ERROR:{ex.ToString()}]");
                            }
                            break;
                        }
                    case 3:
                        {
                            /*
                            // SC
                            var amount = rnd.Next(10, 100);
                            var args = new[]
                            {
                                new ContractParameter()
                                {
                                    Type = ContractParameterType.Hash160,
                                    Value = to.Address.ToScriptHash()
                                },
                                new ContractParameter()
                                {
                                    Type = ContractParameterType.Integer,
                                    Value = amount
                                }
                            };
                            Console.WriteLine("  NEP5  " + from.Address + " >> " + to.Address + " --  " + gas);
                            // TODO fix nep5 transfer
                            Invoke(contract, "transfer", args, to.Address.ToScriptHash());
                            */
                            break;
                        }
                }
                Thread.Sleep(SLEEP_TX);
            }
            return true;
        }

        private bool SignAndRelay(Transaction tx)
        {
            var context = new ContractParametersContext(tx);
            Wallet.Sign(context);
            if (context.Completed)
            {
                tx.Witnesses = context.GetWitnesses();
                //var send = new LocalNode.Relay { Inventory = tx };
                var send = Activator.CreateInstance(_sendDirectlyType);
                _sendDirectlyField.SetValue(send, tx);

                System.LocalNode.Tell(send);
                return true;
            }
            return false;
        }

        private bool Send(AssetDescriptor asset, UInt160 from, UInt160 to, string amount, long fee)
        {
            var value = BigDecimal.Parse(amount, asset.Decimals);

            if (value.Sign <= 0 || fee < 0)
            {
                Console.WriteLine("Invalid value");
                return false;
            }

            var tx = Wallet.MakeTransaction(new[]
            {
                new TransferOutput
                {
                    AssetId = asset.AssetId,
                    Value = value,
                    ScriptHash = to
                }
            }, from);

            if (tx == null)
            {
                throw new Exception("Insufficient funds");
            }

            if (fee > tx.NetworkFee) tx.NetworkFee = fee;

            return SignAndRelay(tx);
        }

        private bool SendMany(AssetDescriptor asset, UInt160 from, UInt160[] to, BigDecimal amount, long fee)
        {
            var value = BigDecimal.Parse(amount.ToString(), asset.Decimals);

            if (to.Length == 0 || value.Sign <= 0 || fee < 0)
            {
                Console.WriteLine("Invalid value");
                return false;
            }

            var outputs = new TransferOutput[to.Length];

            for (int i = 0; i < to.Length; i++)
            {
                outputs[i] = new TransferOutput
                {
                    AssetId = asset.AssetId,
                    Value = value,
                    ScriptHash = to[i]
                };
                if (outputs[i].Value.Sign <= 0)
                {
                    Console.WriteLine("Invalid params");
                    return false;
                }
            }

            var tx = Wallet.MakeTransaction(outputs, from);
            if (tx == null)
            {
                Console.WriteLine("Insufficient funds");
                return false;
            }

            if (fee > tx.NetworkFee) tx.NetworkFee = fee;

            return SignAndRelay(tx);
        }

        /*
        private bool Invoke(UInt160 hash, string method, ContractParameter[] args, UInt160 from)
        {
            byte[] script;
            using (var sb = new ScriptBuilder())
            {
                if (string.IsNullOrEmpty(method))
                    sb.EmitAppCall(hash, parameters: args);
                else
                    sb.EmitAppCall(hash, method, args: args);
                script = sb.ToArray();
            }

            var invokeTrans = new InvocationTransaction()
            {
                Version = 1,
                Script = script,
                Inputs = new CoinReference[0],
                Outputs = new TransactionOutput[0],
                Attributes = new TransactionAttribute[0],
                Witnesses = new Witness[0]
            };

            var engine = ApplicationEngine.Run(invokeTrans.Script, invokeTrans, testMode: true);

            if (engine.State.HasFlag(VMState.FAULT))
            {
                Console.WriteLine("Execution Failed");
                return false;
            }

            invokeTrans.Gas = engine.GasConsumed - Fixed8.FromDecimal(10);
            if (invokeTrans.Gas < Fixed8.Zero) invokeTrans.Gas = Fixed8.Zero;
            invokeTrans.Gas = invokeTrans.Gas.Ceiling();
            var fee = invokeTrans.Gas.Equals(Fixed8.Zero) ? Fixed8.FromDecimal(0.001m) : Fixed8.Zero;

            var tx = Wallet.MakeTransaction(invokeTrans, from: from, change_address: from, fee: fee);
            if (tx == null)
            {
                Console.WriteLine("Insufficient Funds");
                return false;
            }

            var context = new ContractParametersContext(tx);
            Wallet.Sign(context);
            tx.Witnesses = context.GetWitnesses();
            return SignAndRelay(tx);
        }
        */
    }
}
