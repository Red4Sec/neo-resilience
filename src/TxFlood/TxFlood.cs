using Akka.Actor;
using Neo.IO;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    public class TxFlood : Plugin
    {
        private readonly Random _rand;
        private readonly Type _sendDirectlyType;
        private readonly FieldInfo _sendDirectlyField;
        private readonly AssetDescriptor NEO, GAS;

        private Wallet Wallet => System?.RpcServer?.Wallet;
        private Task _task;
        private long _taskRun = 0;
        private WalletAccount[] _sources, _destinations;

        private const string ENV_TASK_CONTROLLER = "NEO_TX_RUN";

        private int SLEEP_START = 51000;
        private int SLEEP_ROUND = 5000;
        private int SLEEP_TX = 500;
        private string CONTRACT = "0x185072a45df4d002545db31157a8955baa39e11a";

        public override void Configure() { }

        /// <summary>
        /// Constructor
        /// </summary>
        public TxFlood() : base()
        {
            _rand = new Random();

            NEO = new AssetDescriptor(NativeContract.NEO.Hash);
            GAS = new AssetDescriptor(NativeContract.GAS.Hash);

            // This is used for relay directly without enter in our mempool

            _sendDirectlyType = typeof(LocalNode).GetMembers(BindingFlags.NonPublic)
                .Where(u => u.Name == "SendDirectly")
                .Cast<Type>()
                .FirstOrDefault();

            _sendDirectlyField = _sendDirectlyType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(u => u.Name == "Inventory")
                .FirstOrDefault();
        }

        private bool UpdateEnvVar(ref int val, string varName)
        {
            var value = Environment.GetEnvironmentVariable(varName);
            if (string.IsNullOrEmpty(value)) return false;
            if (!int.TryParse(value, out var intValue)) return false;
            if (intValue == val) return false;

            val = intValue;
            return true;
        }

        private bool UpdateEnvVar(ref string val, string varName)
        {
            var value = Environment.GetEnvironmentVariable(varName);
            if (string.IsNullOrEmpty(value)) return false;
            if (value == val) return false;

            val = value;
            return true;
        }

        protected override void OnPluginsLoaded()
        {
            new Task(() =>
            {
                while (true)
                {
                    UpdateEnvVar(ref SLEEP_START, ENV_TASK_CONTROLLER + "_SLEEP_START");
                    UpdateEnvVar(ref SLEEP_ROUND, ENV_TASK_CONTROLLER + "_SLEEP_ROUND");
                    UpdateEnvVar(ref SLEEP_TX, ENV_TASK_CONTROLLER + "_SLEEP_TX");
                    UpdateEnvVar(ref CONTRACT, ENV_TASK_CONTROLLER + "_CONTRACT");

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
                    Thread.Sleep(5000);
                }
            })
            .Start();
        }

        protected override bool OnMessage(object message)
        {
            if (!(message is string[] args)) return false;
            if (args.Length == 0) return false;

            switch (args[0].ToLower())
            {
                case "stop": Stop(); return true;
                case "launch": Launch(); return true;
                case "balances":
                    {
                        if (!InitWallet()) return false;
                        Balances(args);
                        return true;
                    }
                case "distribute":
                    {
                        if (!InitWallet()) return false;
                        Distribute(args);
                        return true;
                    }
                case "collect":
                    {
                        if (!InitWallet()) return false;
                        Collect(args);
                        return true;
                    }
                case "flood":
                    {
                        if (!InitWallet()) return false;
                        Flood();
                        return true;
                    }
            }

            return false;
        }

        public void Debug(string input)
        {
#if DEBUG
            Console.WriteLine(input);
#endif
        }

        public bool Launch()
        {
            if (_task?.Status == TaskStatus.Running)
            {
                Debug("Already running");
                return false;
            }

            Interlocked.Exchange(ref _taskRun, 1);
            _task = Task.Run(() =>
            {
                Debug("Start sender");
                Thread.Sleep(SLEEP_START);
                if (!InitWallet()) return;

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
                Debug("Already stoped");
                return false;
            }

            Interlocked.Exchange(ref _taskRun, 0);
            Debug("Stoping sender...");
            return _task?.Status != TaskStatus.Running;
        }

        private bool Balances(string[] args)
        {
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

            Console.WriteLine("Total    NEO: " + Wallet.GetAvailable(NativeContract.NEO.Hash) + "    GAS: " + Wallet.GetAvailable(NativeContract.GAS.Hash));
            return true;
        }

        private bool Distribute(string[] args)
        {
            if (args.Length != 3) return false;

            var value = BigDecimal.Parse(args[1], 0);
            var org = Wallet.GetAccounts().First().Address.ToScriptHash();
            var dest = Wallet.GetAccounts().Skip(1).Select(d => d.Address.ToScriptHash()).ToArray();
            long fee = 300000000;

            if (args[2].ToLower() == "gas")
            {
                return SendMany(GAS, org, dest, value.ToString(), fee);
            }

            return SendMany(NEO, org, dest, value.ToString(), fee);
        }

        private bool Collect(string[] args)
        {
            if (args.Length != 2) return false;

            var dest = Wallet.GetAccounts().First().Address.ToScriptHash();
            var sources = Wallet.GetAccounts().Skip(1).Select(d => d.Address.ToScriptHash()).ToArray();
            long fee = 250000000;

            var assetId = args[1].ToLower() == "gas" ? GAS : NEO;

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

        private bool InitWallet()
        {
            if (Wallet == null)
            {
                Console.WriteLine("no wallet or rpc disabled");
                return false;
            }

            _sources = Wallet.GetAccounts().Skip(1).ToArray();
            _destinations = _sources.OrderByDescending(x => x.ScriptHash).ToArray();

            // Warm up

            Parallel.ForEach(_sources, (a) => a.GetKey());

            return true;
        }

        private bool Flood()
        {
            //var contract = UInt160.Parse(CONTRACT);
            Console.WriteLine();

            for (int i = 0; i < _sources.Length; i++)
            {
                if (Interlocked.Read(ref _taskRun) == 0) return true;

                var from = _sources[i];
                var to = _destinations[i];
                long fee = _rand.Next(250_000_000, 800_000_000);
                var option = _rand.Next(1, 3);

                switch (option)
                {
                    case 1:
                        {
                            // NEO
                            var neo = BigDecimal.Parse(_rand.Next(1, 10).ToString(), 0);
                            try
                            {
                                Send(NEO, from.ScriptHash, to.ScriptHash, neo.ToString(), fee);
                                Console.WriteLine("  NEO - " + from.Address + " >> " + to.Address + " --  " + neo);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("  NEO - " + from.Address + " >> " + to.Address + " --  " + neo + $" [ERROR:{ex.ToString()}]");
                            }
                            break;
                        }
                    case 2:
                        {
                            // GAS
                            var gas = new BigDecimal(new BigInteger(_rand.Next(10000000, 900000000)), 8);
                            try
                            {
                                Send(GAS, from.ScriptHash, to.ScriptHash, gas.ToString(), fee);
                                Console.WriteLine("  GAS - " + from.Address + " >> " + to.Address + " --  " + gas);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("  GAS - " + from.Address + " >> " + to.Address + " --  " + gas + $" [ERROR:{ex.ToString()}]");
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
                Console.WriteLine("Insufficient funds");
                return false;
            }

            if (fee > tx.NetworkFee) tx.NetworkFee = fee;

            return SignAndRelay(tx);
        }

        private bool SendMany(AssetDescriptor asset, UInt160 from, UInt160[] to, string amount, long fee)
        {
            var value = BigDecimal.Parse(amount, asset.Decimals);

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

        private bool Invoke(UInt160 hash, string method, ContractParameter[] args, UInt160 from)
        {
            return true;
            /*
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
            */
        }
    }
}
