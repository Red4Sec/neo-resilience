using Akka.Actor;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.VM;
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

        private int SLEEP_START = 70000;
        private int SLEEP_ROUND = 5000;
        private int SLEEP_TX = 50;
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
                case "launch": Launch(); return true;
                case "stop": Stop(); return true;
                case "balances": Balances(); return true;
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

        private bool Balances()
        {
            if (!CheckWallet()) return false;
            foreach (var dir in Wallet.GetAccounts())
            {
                var neo = Fixed8.Zero;
                var gas = Fixed8.Zero;
                var addr = dir.Address.ToScriptHash();
                var unSpendCoins = Wallet.GetCoins(new[] { addr }).Where(x => x.State != CoinState.Spent && x.State == CoinState.Confirmed);
                foreach (var coin in unSpendCoins)
                {
                    if (coin.Output.AssetId == Blockchain.GoverningToken.Hash)
                    {
                        neo += coin.Output.Value;
                    }
                    else if (coin.Output.AssetId == Blockchain.UtilityToken.Hash)
                    {
                        gas += coin.Output.Value;
                    }
                }
                Console.WriteLine(dir.Address.ToString() + "\n\tNEO: " + neo + "\n\tGAS: " + gas);
            }
            return true;
        }

        private bool Distribute(string[] args)
        {
            if (!CheckWallet()) return false;
            if (args.Length < 3) return false;
            var value = BigDecimal.Parse(args[1], 0);
            var org = Wallet.GetAccounts().First().Address.ToScriptHash();
            var dest = Wallet.GetAccounts().Skip(1).Select(d => d.Address.ToScriptHash()).ToArray();
            if (args[2].ToLower() == "gas")
            {
                return SendMany(Blockchain.UtilityToken.Hash, org, dest, value, Fixed8.One, org);
            }
            return SendMany(Blockchain.GoverningToken.Hash, org, dest, value, Fixed8.One, org);
        }

        private bool Collect(string[] args)
        {
            if (!CheckWallet()) return false;
            if (args.Length < 2) return false;
            var addr = Wallet.GetAccounts().First().Address.ToScriptHash();
            if (args[1].ToLower() == "gas")
            {
                var balance = Wallet.GetBalance(Blockchain.UtilityToken.Hash) - Fixed8.One;
                var amount = new BigDecimal(new BigInteger(balance.GetData()), 8);
                return Send(Blockchain.UtilityToken.Hash, null, addr, amount, Fixed8.One, addr);
            }
            else
            {
                var balance = Wallet.GetBalance(Blockchain.GoverningToken.Hash);
                var amount = new BigDecimal(new BigInteger(balance.GetData()), 8);
                return Send(Blockchain.GoverningToken.Hash, null, addr, amount, Fixed8.One, addr);
            }

        }

        private bool Flood()
        {
            if (!CheckWallet()) return false;

            var rnd = new Random();
            var contract = UInt160.Parse(CONTRACT);

            foreach (var dir in Wallet.GetAccounts().Skip(1))
            {
                if (Interlocked.Read(ref _taskRun) == 0) return true;
                var addr = dir.Address.ToScriptHash();
                var from = addr;
                var to = addr;
                var change_address = addr;
                var fee = Fixed8.FromDecimal(rnd.Next(0, 25) / 543.3m);

                var option = rnd.Next(1, 6);
                switch (option)
                {
                    case 1:
                    case 2:
                        {
                            // NEO
                            var neo = BigDecimal.Parse(rnd.Next(1, 10).ToString(), 0);
                            Console.WriteLine(dir.Address + " --  NEO - Amount: " + neo + " Fee: " + fee);
                            Send(Blockchain.GoverningToken.Hash, from, to, neo, fee, change_address);
                            break;
                        }

                    case 3:
                    case 4:
                        {
                            // GAS
                            var gas = new BigDecimal(new BigInteger(new Random().Next(10000000, 900000000)), 8);
                            Console.WriteLine(dir.Address + " --  GAS - Amount: " + gas + " Fee: " + fee);
                            Send(Blockchain.UtilityToken.Hash, from, to, gas, fee, change_address);
                            break;
                        }
                    case 5:
                        {
                            // CLAIM
                            Console.WriteLine(dir.Address + " --  CLAIM ");
                            Claim(dir.Address);
                            break;
                        }
                    case 6:
                    case 7:
                        {
                            // SC
                            var amount = rnd.Next(10, 100);
                            var args = new[]
                            {
                                new ContractParameter()
                                {
                                    Type = ContractParameterType.Hash160,
                                    Value = dir.Address.ToScriptHash()
                                },
                                new ContractParameter()
                                {
                                    Type = ContractParameterType.Integer,
                                    Value = amount
                                }
                            };
                            Console.WriteLine(dir.Address + " --  NEP5 Transfer - Amount: " + amount);
                            // TODO fix nep5 transfer
                            Invoke(contract, "transfer", args, dir.Address.ToScriptHash());
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
                Wallet.ApplyTransaction(tx);
                System.LocalNode.Tell(new LocalNode.Relay { Inventory = tx });
                return true;
            }
            return false;
        }

        private bool Send(UIntBase assetId, UInt160 from, UInt160 to, BigDecimal amount, Fixed8 fee, UInt160 change_address)
        {
            if (!CheckWallet()) return false;
            if (amount.Sign <= 0 || fee < Fixed8.Zero)
            {
                Console.WriteLine("Invalid value");
                return false;
            }

            var outputs = new[]
            {
                new TransferOutput
                {
                    AssetId = assetId,
                    Value = amount,
                    ScriptHash = to
                }
            };

            var tx = Wallet.MakeTransaction(null, outputs, from: from, change_address: change_address, fee: fee);
            if (tx == null)
            {
                Console.WriteLine("Insufficient funds");
                return false;
            }

            var context = new ContractParametersContext(tx);
            Wallet.Sign(context);
            tx.Witnesses = context.GetWitnesses();
            if (tx.Size > 1024)
            {
                var calFee = Fixed8.FromDecimal(tx.Size * 0.00001m + 0.001m);
                if (fee < calFee)
                {
                    fee = calFee;
                    tx = Wallet.MakeTransaction(null, outputs, from: from, change_address: change_address, fee: fee);
                    if (tx == null)
                    {
                        Console.WriteLine("Insufficient funds");
                        return false;
                    }
                }
            }
            return SignAndRelay(tx);
        }

        private bool SendMany(UIntBase assetId, UInt160 from, UInt160[] to, BigDecimal amount, Fixed8 fee, UInt160 change_address)
        {
            if (!CheckWallet()) return false;
            if (to.Length == 0 || fee < Fixed8.Zero)
            {
                Console.WriteLine("Invalid params");
                return false;
            }
            var outputs = new TransferOutput[to.Length];

            for (int i = 0; i < to.Length; i++)
            {
                outputs[i] = new TransferOutput
                {
                    AssetId = assetId,
                    Value = amount,
                    ScriptHash = to[i]
                };
                if (outputs[i].Value.Sign <= 0)
                {
                    Console.WriteLine("Invalid params");
                    return false;
                }
            }
            var tx = Wallet.MakeTransaction(null, outputs, from: from, change_address: change_address, fee: fee);
            if (tx == null)
            {
                Console.WriteLine("Insufficient funds");
                return false;
            }

            var context = new ContractParametersContext(tx);
            Wallet.Sign(context);
            tx.Witnesses = context.GetWitnesses();
            if (tx.Size > 1024)
            {
                var calFee = Fixed8.FromDecimal(tx.Size * 0.00001m + 0.001m);
                if (fee < calFee)
                {
                    fee = calFee;
                    tx = Wallet.MakeTransaction(null, outputs, from: from, change_address: change_address, fee: fee);
                    if (tx == null)
                    {
                        Console.WriteLine("Insufficient funds");
                        return false;
                    }
                }
            }
            return SignAndRelay(tx);
        }

        private bool Claim(string addr)
        {
            if (!CheckWallet()) return false;

            var claims = Wallet.GetUnclaimedCoins()
                .Where(x => x.Address == addr)
                .Select(p => p.Reference)
                .Take(5)
                .ToArray();

            if (claims.Count() == 0)
                return true;

            ClaimTransaction tx;
            using (var snapshot = Blockchain.Singleton.GetSnapshot())
            {
                tx = new ClaimTransaction
                {
                    Claims = claims,
                    Attributes = new TransactionAttribute[0],
                    Inputs = new CoinReference[0],
                    Outputs = new[]
                    {
                        new TransactionOutput
                        {
                            AssetId = Blockchain.UtilityToken.Hash,
                            Value = snapshot.CalculateBonus(claims),
                            ScriptHash = addr.ToScriptHash()
                        }
                    }
                };
            }
            return SignAndRelay(tx);
        }

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
    }
}
