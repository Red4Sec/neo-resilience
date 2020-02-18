using Neo.Wallets;
using Neo.Wallets.NEP6;
using Neo.Wallets.SQLite;
using System;
using System.IO;

namespace Neo.Plugins.Helpers
{
    public static class WalletHelper
    {
        public static Wallet OpenWallet(string path, string password)
        {
            if (!File.Exists(path)) throw new FileNotFoundException();
            switch (Path.GetExtension(path))
            {
                case ".db3":
                    {
                        return UserWallet.Open(path, password);
                    }
                case ".json":
                    {
                        var nep6wallet = new NEP6Wallet(path);
                        nep6wallet.Unlock(password);
                        return nep6wallet;
                    }
                default: throw new NotSupportedException();
            }
        }
    }
}
