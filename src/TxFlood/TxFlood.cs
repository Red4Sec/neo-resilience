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
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Plugins
{
    public class TxFlood : Plugin
    {
        private Wallet Wallet => System?.RpcServer?.Wallet;
        private Task _task;
        private long _taskRun = 0;

        private const string ENV_TASK_CONTROLLER = "NEO_TX_RUN";

        private int SLEEP_START = 51000;
        private int SLEEP_ROUND = 5000;
        private int SLEEP_TX = 500;
        private string CONTRACT = "0x185072a45df4d002545db31157a8955baa39e11a";

        public override void Configure() { }

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
                case "launch": Launch(); return true;
                case "stop": Stop(); return true;
                case "balances": Balances(args); return true;
                case "distribute": Distribute(args); return true;
                case "collect": Collect(args); return true;
                case "flood": Flood(); return true;
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
            if (!CheckWallet()) return false;

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
            if (!CheckWallet()) return false;
            if (args.Length != 3) return false;

            var value = BigDecimal.Parse(args[1], 0);
            var org = Wallet.GetAccounts().First().Address.ToScriptHash();
            var dest = Wallet.GetAccounts().Skip(1).Select(d => d.Address.ToScriptHash()).ToArray();
            long fee = 300000000;

            if (args[2].ToLower() == "gas")
            {
                return SendMany(NativeContract.GAS.Hash, org, dest, value.ToString(), fee);
            }

            return SendMany(NativeContract.NEO.Hash, org, dest, value.ToString(), fee);
        }

        private bool Collect(string[] args)
        {
            if (!CheckWallet()) return false;
            if (args.Length != 2) return false;

            var dest = Wallet.GetAccounts().First().Address.ToScriptHash();
            var sources = Wallet.GetAccounts().Skip(1).Select(d => d.Address.ToScriptHash()).ToArray();
            long fee = 250000000;

            var assetId = args[1].ToLower() == "gas" ? NativeContract.GAS.Hash : NativeContract.NEO.Hash;

            foreach (var dir in sources)
            {
                var balance = Wallet.GetBalance(assetId, dir);

                if (assetId == NativeContract.GAS.Hash)
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
            if (!CheckWallet()) return false;

            var rnd = new Random();
            //var contract = UInt160.Parse(CONTRACT);

            var sources = Wallet.GetAccounts().Skip(1).ToArray();
            var destinations = sources.OrderByDescending(x => x.ScriptHash).ToArray();
            Console.WriteLine();

            for (int i = 0; i < sources.Count(); i++)
            {
                if (Interlocked.Read(ref _taskRun) == 0) return true;

                var from = sources[i];
                var to = destinations[i];
                long fee = rnd.Next(250000000, 800000000);

                var option = rnd.Next(1, 3);
                switch (option)
                {
                    case 1:
                        {
                            // NEO
                            var neo = BigDecimal.Parse(rnd.Next(1, 10).ToString(), 0);
                            Console.WriteLine("  NEO - " + from.Address + " >> " + to.Address + " --  " + neo);
                            Send(NativeContract.NEO.Hash, from.ScriptHash, to.ScriptHash, neo.ToString(), fee);
                            break;
                        }

                    case 2:
                        {
                            // GAS
                            var gas = new BigDecimal(new BigInteger(new Random().Next(10000000, 900000000)), 8);
                            Console.WriteLine("  GAS - " + from.Address + " >> " + to.Address + " --  " + gas);
                            Send(NativeContract.GAS.Hash, from.ScriptHash, to.ScriptHash, gas.ToString(), fee);
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

        private bool CheckWallet()
        {
            if (Wallet is null)
            {
                Console.WriteLine("no wallet or rpc disabled");
                return false;
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
                System.LocalNode.Tell(new LocalNode.Relay { Inventory = tx });
                return true;
            }
            return false;
        }

        private bool Send(UInt160 assetId, UInt160 from, UInt160 to, string amount, long fee)
        {
            if (!CheckWallet()) return false;

            AssetDescriptor descriptor = new AssetDescriptor(assetId);
            BigDecimal value = BigDecimal.Parse(amount, descriptor.Decimals);

            if (value.Sign <= 0 || fee < 0)
            {
                Console.WriteLine("Invalid value");
                return false;
            }

            Transaction tx = Wallet.MakeTransaction(new[]
            {
                new TransferOutput
                {
                    AssetId = assetId,
                    Value = value,
                    ScriptHash = to
                }
            }, from);
            if (tx == null)
            {
                Console.WriteLine("Insufficient funds");
                return false;
            }

            //tx.NetworkFee = fee;
            ContractParametersContext transContext = new ContractParametersContext(tx);

            Wallet.Sign(transContext);
            tx.Witnesses = transContext.GetWitnesses();

            if (tx.Size > 1024)
            {
                long calFee = tx.Size * 1000 + 100000;
                if (tx.NetworkFee < calFee)
                    tx.NetworkFee = calFee;
            }

            return SignAndRelay(tx);
        }

        private bool SendMany(UInt160 assetId, UInt160 from, UInt160[] to, string amount, long fee)
        {
            if (!CheckWallet()) return false;

            AssetDescriptor descriptor = new AssetDescriptor(assetId);
            BigDecimal value = BigDecimal.Parse(amount, descriptor.Decimals);

            if (to.Length == 0 || value.Sign <= 0 || fee < 0)
            {
                Console.WriteLine("Invalid value");
                return false;
            }

            TransferOutput[] outputs = new TransferOutput[to.Length];

            for (int i = 0; i < to.Length; i++)
            {
                outputs[i] = new TransferOutput
                {
                    AssetId = assetId,
                    Value = value,
                    ScriptHash = to[i]
                };
                if (outputs[i].Value.Sign <= 0)
                {
                    Console.WriteLine("Invalid params");
                    return false;
                }
            }

            Transaction tx = Wallet.MakeTransaction(outputs, from);
            if (tx == null)
            {
                Console.WriteLine("Insufficient funds");
                return false;
            }

            //tx.NetworkFee = fee;
            ContractParametersContext transContext = new ContractParametersContext(tx);

            Wallet.Sign(transContext);
            tx.Witnesses = transContext.GetWitnesses();

            if (tx.Size > 1024)
            {
                long calFee = tx.Size * 1000 + 100000;
                if (tx.NetworkFee < calFee)
                    tx.NetworkFee = calFee;
            }

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