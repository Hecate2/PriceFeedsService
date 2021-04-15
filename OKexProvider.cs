using Neo;
using Neo.SmartContract;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using System;

namespace PriceFeedService
{
    [ContractPermission("0x6af9e0a8fd8d43dfcbfb32d475e408337dbb3152", "updatePriceByProvider")]
    [ContractPermission("0xfffdc93764dbaddd97c48f252a53ea4643faa3fd", "destroy", "update")]
    public class OKexProvider : SmartContract
    {
        public const string Prefix_Price_URL = "https://www.okex.com/api/v5/market/history-candles?bar=1m&limit=1";
        public const string Prefix_Price_InstId = "&instId=";
        public const string Prefix_Price_Time = "&after=";
        // { "code": "0", "msg": "", "data": [["1597026360000","11983.2","11988.5","11980.2","11988.2","26.43284234","316742.81553508"]]}
        public const string filter = "$.data[0][4]";
        public const long gasForResponse = Oracle.MinimumResponseFee; // TBD
        private static StorageMap price => new StorageMap(Storage.CurrentContext, nameof(price));
        private static StorageMap tradingPair => new StorageMap(Storage.CurrentContext, nameof(tradingPair));

        [InitialValue("NWhJATyChXvaBqS9debbk47Uf2X33WtHtL", ContractParameterType.Hash160)]
        private static readonly UInt160 Owner = default; //  Replace it with your own address
        [InitialValue("5231bb7d3308e475d432fbcbdf438dfda8e0f96a", ContractParameterType.ByteArray)]
        private static readonly UInt160 ProviderRegistry = default;

        public static string Name => "OKexProvider";

        public static void GetPriceRequest(uint blockIndex, string symbol) // BTC-USDT, 1597026383085
        {
            ulong timestamp = Ledger.GetBlock(blockIndex).Timestamp;
            if (Runtime.CallingScriptHash != ProviderRegistry) throw new Exception("No authorization");
            string symbolUrl = Prefix_Price_URL + Prefix_Price_InstId + symbol + Prefix_Price_Time + timestamp;
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
    }
}
