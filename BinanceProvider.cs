using Neo;
using Neo.SmartContract;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using System;

namespace PriceFeedService
{
    [ManifestExtra("Description", "Binance Provider")]
    [ContractPermission("0x0d12df57f86ee9d2350636da1ff2e2f6376b6202", "updatePriceByProvider")]
    [ContractPermission("0xfffdc93764dbaddd97c48f252a53ea4643faa3fd", "destroy", "update")]
    public class BinanceProvider : SmartContract
    {
        public const string Prefix_Price_URL = "https://binance.com/api/v3/klines?interval=1m&limit=1";
        public const string Prefix_Price_InstId = "&symbol=";  // NEOUSDT
        public const string Prefix_Price_Time = "&endTime=";
        // [[1511149320000,"36.00000000","36.00000000","36.00000000","36.00000000","1.14000000",1511149379999,"41.04000000",2,
        // "0.57000000", "20.52000000", "3039601.46000000"]]
        public const string filter = "$[0][4]";
        public const long gasForResponse = Oracle.MinimumResponseFee; // TBD
        private static StorageMap price => new StorageMap(Storage.CurrentContext, nameof(price));

        [InitialValue("NWhJATyChXvaBqS9debbk47Uf2X33WtHtL", ContractParameterType.Hash160)]
        private static readonly UInt160 Owner = default; //  Replace it with your own address
        [InitialValue("02626b37f6e2f21fda360635d2e96ef857df120d", ContractParameterType.ByteArray)]
        private static readonly UInt160 ProviderRegistry = default;

        public static void GetPriceRequest(uint blockIndex, string symbol) // 5830, neo-usdt
        {
            string newSymbol = StandardizeSymbol(symbol); // NEOUSDT
            ulong timestamp = Ledger.GetBlock(blockIndex).Timestamp;
            if (Runtime.CallingScriptHash != ProviderRegistry) throw new Exception("No authorization");
            string symbolUrl = Prefix_Price_URL + Prefix_Price_InstId + newSymbol + Prefix_Price_Time + timestamp;
            Oracle.Request(symbolUrl, filter, "getPriceCallback", blockIndex + "#" + symbol, 100000000);
        }

        public static void GetPriceCallback(string url, string userdata, OracleResponseCode code, string result)
        {
            if (Runtime.CallingScriptHash != Oracle.Hash) throw new Exception("No authorization");
            if (code != OracleResponseCode.Success) throw new Exception("Oracle response failure with code " + (byte)code);

            object[] arr = (object[])StdLib.JsonDeserialize(result); // ["11988.2"]
            string value = (string)arr[0];
            string[] data = StdLib.StringSplit(userdata, "#");
            Contract.Call(ProviderRegistry, "updatePriceByProvider", CallFlags.All, new object[] { data[0], data[1], value });
        }

        public static void Update(ByteString nefFile, string manifest, object data)
        {
            if (!IsOwner()) throw new Exception("No authorization.");
            ContractManagement.Update(nefFile, manifest, data);
        }

        public static void Destroy()
        {
            if (!IsOwner()) throw new Exception("No authorization.");
            ContractManagement.Destroy();
        }

        private static bool IsOwner() => Runtime.CheckWitness(Owner);

        private static string StandardizeSymbol(string symbol)
        {
            byte[] ret = new byte[symbol.Length - 1];
            int i = 0;
            foreach (char c in symbol)
            {
                if (c == '-') continue;
                ret[i++] = (byte)(char)(c ^ 0x20);
            }
            return (ByteString)ret;
        }

        public static int test()
        {
            return 5;
        }
    }
}
