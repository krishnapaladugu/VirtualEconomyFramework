﻿using NBitcoin;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using VEDriversLite.DogeAPI;
using VEDriversLite.Security;

namespace VEDriversLite
{
    /// <summary>
    /// Doge Transaction Helpers
    /// </summary>
    public static class DogeTransactionHelpers
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static IClient _client;
        /// <summary>
        /// Conversion ration for Doge to convert from sat to 1 DOGE
        /// </summary>
        public static double FromSatToMainRatio = 100000000;
        /// <summary>
        /// NBitcoin Instance of Mainet Network of Dogecoin
        /// </summary>
        public static Network Network = NBitcoin.Altcoins.Dogecoin.Instance.Mainnet;
        /// <summary>
        /// Minimum number of confirmation to send the transaction
        /// </summary>
        public static int MinimumConfirmations = 1;

        private static readonly ConcurrentDictionary<string, GetTransactionInfoResponse> transactionDetails = new ConcurrentDictionary<string, GetTransactionInfoResponse>();

        /// <summary>
        /// Function will sign transaction with provided key and broadcast with Neblio API
        /// </summary>
        /// <param name="transaction">NBitcoin Transaction object</param>
        /// <param name="key">NBitcoin Key - must contain Private Key</param>
        /// <param name="utxos">List of the input utxos</param>
        /// <returns>New Transaction Hash - TxId</returns>
        public static async Task<string> SignAndBroadcastTransaction(Transaction transaction, BitcoinSecret key, ICollection<Utxo> utxos)
        {            
            BitcoinAddress addressForTx = null;
            if (key != null)
            {
                addressForTx = key.PubKey.GetAddress(ScriptPubKeyType.Legacy, Network);
            }
            else
            {
                throw new Exception("BitcoinSecret is null");
            }

            // add coins
            List<ICoin> coins = new List<ICoin>();
            try
            {
                foreach (var inp in utxos)
                {
                    if (transaction.Inputs.FirstOrDefault(i => (i.PrevOut.Hash == uint256.Parse(inp.TxId)) && i.PrevOut.N == (uint)inp.N) != null)
                    {
                        var val = (ulong)(Convert.ToDouble(inp.Value, CultureInfo.InvariantCulture) * FromSatToMainRatio);
                        coins.Add(new Coin(uint256.Parse(inp.TxId), (uint)inp.N, new Money(val), addressForTx.ScriptPubKey));
                    }
                }

                // add signature to inputs before signing
                foreach (var inp in transaction.Inputs)
                {
                    inp.ScriptSig = addressForTx.ScriptPubKey;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Exception during loading inputs. " + ex.Message);
            }

            // sign
            try
            {
                var tx = transaction.ToString();

                transaction.Sign(key, coins);

                var sx = transaction.ToString();

                if (tx == sx)
                {
                    throw new Exception("Transaction was not signed. Probably not spendable source.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Exception during signing tx! " + ex.Message);
            }

            // broadcast
            try
            {
                var txhex = transaction.ToHex();
                var res = await VENFTBroadcastTxAsync(new VENFTBroadcastTxRequest() { network = "dogecoin", tx_hex = txhex });
                return res;
            }
            catch (Exception)
            {
                throw new Exception("Cannot Broadcast the dogecoin transaction. Trouble with Dogecoin API. ");
            }
        }

        /// <summary>
        /// This function will calculate the fee based of the known lenght of the intputs and the outputs
        /// If there is the OP_RETURN output it is considered as the customMessage. Please fill it for token transactions.
        /// Token transaction also will add just for sure one output to calculation of the size for the case there will be some tokens back to original address
        /// </summary>
        /// <param name="numOfInputs">Number of input of the transaction "in" vector</param>
        /// <param name="numOfOutputs">Number of outpus of the transaction "out" vector</param>
        /// <param name="customMessageInOPReturn">Custom message - "OP_RETURN" output</param>
        /// <param name="isTokenTransaction">Token transaction will add another output for getting back the tokens</param>
        /// <returns></returns>
        public static double CalcFee(int numOfInputs, int numOfOutputs, string customMessageInOPReturn, bool isTokenTransaction)
        {
            if (numOfInputs <= 0)
                numOfInputs = 1;
            if (numOfOutputs <= 0)
                numOfOutputs = 1;

            var basicFee = 1000; //0.00001 per 1 byte 

            // inputs
            var blankInput = 41;
            var inputSignature = 56;
            var signedInput = blankInput + inputSignature;

            // outputs
            var outputWithAddress = 34;
            var emptyOpReturn = 11; // OP_RETURN with custom message with 10 characters had 21 bytes...etc.

            //common properties in each transaction
            var commonPropertiesSize = 214;

            var expectedSize = signedInput * numOfInputs + outputWithAddress * numOfOutputs + commonPropertiesSize;

            // add custom message if there is some
            if (!string.IsNullOrEmpty(customMessageInOPReturn))
            {
                expectedSize += emptyOpReturn + customMessageInOPReturn.Length;
            }

            // Expected outputs for the rest of the coins/tokens
            expectedSize += outputWithAddress; // DOGECOIN
            if (isTokenTransaction)
            {
                expectedSize += outputWithAddress;
            }

            if (expectedSize > 10000)
            {
                throw new Exception("Cannot send transaction bigger than 10kB on DOGE network!");
            }

            var fee = expectedSize * basicFee;
            if (fee < basicFee)
            {
                fee = basicFee * 250;
            }

            return fee;
        }

        /// <summary>
        /// This function will crate empty Transaction object based on Neblio network standard
        /// Then add the Neblio Inputs and sumarize their value
        /// </summary>
        /// <param name="dutxos">List of Dogecoin Utxos to use</param>
        /// <param name="address">Address of the owner</param>
        /// <returns>(NBitcoin Transaction object, sum of all inputs values in double)</returns>
        public static (Transaction, double) GetTransactionWithDogecoinInputs(ICollection<Utxo> dutxos, BitcoinAddress address)
        {
            // create template for new tx from last one
            var transaction = Transaction.Create(Network);

            var allNeblInputCoins = 0.0; //this is because of the optimization. When we iterate through values we can sum them for later use
            try
            {
                // add inputs of tx
                foreach (var utxo in dutxos)
                {
                    transaction.Inputs.Add(new TxIn()
                    {
                        PrevOut = new OutPoint(uint256.Parse(utxo.TxId), utxo.N),
                        ScriptSig = address.ScriptPubKey,
                    });
                    allNeblInputCoins += Convert.ToDouble(utxo.Value, CultureInfo.InvariantCulture);
                }
                return (transaction, allNeblInputCoins);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception during loading inputs. " + ex.Message);
            }

            return (null, 0);
        }        

        private static Transaction GetTransactionObject(SendTxData data, BitcoinAddress addressForTx, ICollection<Utxo> utxos, bool withMessage = false)
        {
            if (data == null)
            {
                throw new Exception("Data cannot be null!");
            }

            if (addressForTx == null)
            {
                throw new Exception("BitcoinAddress object cannot be null!");
            }

            // create receiver address
            BitcoinAddress recaddr;
            try
            {
                recaddr = BitcoinAddress.Create(data.ReceiverAddress, Network);
            }
            catch (Exception)
            {
                throw new Exception("Cannot send transaction. cannot create receiver address!");
            }

            // create template for new tx from last one
            var tx = GetTransactionWithDogecoinInputs(utxos, addressForTx);
            if (tx.Item1 == null)
            {
                throw new Exception("Cannot create the transaction object.");
            }

            var transaction = tx.Item1;
            var allDogelCoins = tx.Item2;
            try
            {
                var fee = CalcFee(transaction.Inputs.Count, 1, data.CustomMessage, false);

                if (allDogelCoins < (data.Amount))
                {
                    throw new Exception("Not enough Doge to spend.");
                }

                var amountinSat = Convert.ToUInt64(data.Amount * FromSatToMainRatio);
                var diffinSat = (Convert.ToUInt64(allDogelCoins) * FromSatToMainRatio) - amountinSat - Convert.ToUInt64(fee);

                // create outputs
                transaction.Outputs.Add(new Money(amountinSat), recaddr.ScriptPubKey); // send to receiver required amount

                if (withMessage)
                {
                    var bytes = Encoding.UTF8.GetBytes(data.CustomMessage);
                    transaction.Outputs.Add(new TxOut()
                    {
                        Value = (long)0,
                        ScriptPubKey = TxNullDataTemplate.Instance.GenerateScriptPubKey(bytes)
                    });
                }                               

                if (diffinSat > 0)
                    transaction.Outputs.Add(new Money(Convert.ToUInt64(diffinSat)), addressForTx.ScriptPubKey); // get diff back to sender address
            }
            catch (Exception ex)
            {
                throw new Exception("Exception during creating outputs. " + ex.Message);
            }

            return transaction;
        }

        /// <summary>
        /// Function will send standard Neblio transaction - Async version
        /// </summary>
        /// <param name="data">Send data, please see SendTxData class for the details</param>
        /// <param name="addressForTx">Address for process transaction - signature</param>
        /// <param name="utxos">Optional input neblio utxo</param>
        /// <returns>New Transaction Hash - TxId</returns>
        public static Transaction GetDogeTransactionAsync(SendTxData data, BitcoinAddress addressForTx, ICollection<Utxo> utxos)
        {
            return GetTransactionObject(data, addressForTx, utxos);            
        }

        /// <summary>
        /// Function will send standard Neblio transaction with included message.
        /// </summary>
        /// <param name="data">Send data, please see SendTxData class for the details - this include field for custom message</param>
        /// <param name="utxos">Optional input neblio utxo</param>
        /// <param name="addressForTx">Address for process transaction - signature</param>
        /// <returns>New Transaction Hash - TxId</returns>
        public static Transaction GetDogeTransactionWithMessageAsync(SendTxData data, BitcoinAddress addressForTx, ICollection<Utxo> utxos)
        {
            return GetTransactionObject(data, addressForTx, utxos, withMessage: true);
        }

        /// <summary>
        /// Function will send standard Neblio transaction with message and outputs which goes to different addresses
        /// </summary>
        /// <param name="receiverAmount">Dictionary of all receivers and amounts to send them</param>
        /// <param name="addressForTx">Address for process transaction - signature</param>
        /// <param name="utxos">Optional input neblio utxo</param>
        /// <param name="password">Password for encrypted key if it is encrypted and locked</param>
        /// <param name="message">Custom message</param>
        /// <returns>New Transaction Hash - TxId</returns>
        public static Transaction SendDogeTransactionWithMessageMultipleOutputAsync(Dictionary<string, double> receiverAmount, BitcoinAddress addressForTx, ICollection<Utxo> utxos, string password = "", string message = "")
        {
            if (receiverAmount == null)
                throw new Exception("Receivers Dictionary cannot be null!");

            if (addressForTx == null)
                throw new Exception("BitcoinAddress object cannot be null!");

            // create receiver address
            Dictionary<string, BitcoinAddress> recsaddr = new Dictionary<string, BitcoinAddress>();
            try
            {
                foreach (var r in receiverAmount)
                {
                    var rca = BitcoinAddress.Create(r.Key, Network);
                    recsaddr.Add(r.Key, rca);
                }
            }
            catch (Exception)
            {
                throw new Exception("Cannot send transaction. cannot create receiver address!");
            }

            // create template for new tx from last one
            var tx = GetTransactionWithDogecoinInputs(utxos, addressForTx);
            if (tx.Item1 == null)
            {
                throw new Exception("Cannot create the transaction object.");
            }

            var transaction = tx.Item1;
            var allDogeInputCoins = tx.Item2;

            try
            {
                var fee = CalcFee(transaction.Inputs.Count, receiverAmount.Count, message, false);

                ulong totalamnt = 0;
                foreach (var r in receiverAmount)
                {
                    var amntinSat = Convert.ToUInt64(r.Value * FromSatToMainRatio);
                    totalamnt += amntinSat;
                    // create outputs
                    if (recsaddr.TryGetValue(r.Key, out var badd))
                    {
                        transaction.Outputs.Add(new Money(amntinSat), badd.ScriptPubKey); // send to receiver required amount
                    }
                }

                var bytes = Encoding.UTF8.GetBytes(message);
                transaction.Outputs.Add(new TxOut()
                {
                    Value = (long)0,
                    ScriptPubKey = TxNullDataTemplate.Instance.GenerateScriptPubKey(bytes)
                });

                var diffinSat = (Convert.ToUInt64(allDogeInputCoins) * FromSatToMainRatio) - totalamnt - Convert.ToUInt64(fee);
                if (diffinSat > 0)
                {
                    transaction.Outputs.Add(new Money(Convert.ToUInt64(diffinSat)), addressForTx.ScriptPubKey); // get diff back to sender address
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Exception during creating outputs. " + ex.Message);
            }
           return transaction;
        }

        /// <summary>
        /// Parse the total sent value from Doge Tx Info. It takes all outputs together.
        /// </summary>
        /// <param name="txinfo"></param>
        /// <returns></returns>
        public static CommonReturnTypeDto ParseTotalSentValue(GetTransactionInfoResponse txinfo)
        {
            if (txinfo == null)
                return CommonReturnTypeDto.GetNew<double>();

            if (txinfo.Success != "success")
                return CommonReturnTypeDto.GetNew<double>();

            if (txinfo.Transaction.Vout == null || txinfo.Transaction.Vout.Count == 0)
                return CommonReturnTypeDto.GetNew<double>();

            var value = 0.0;
            var vouts = txinfo.Transaction.Vout.ToList();
            for (var i = 0; i < (vouts.Count - 2); i++)
            {
                var o = vouts[i];
                if (!string.IsNullOrEmpty(o.Script) && !o.Script.Contains("OP_RETURN"))
                {
                    var v = Convert.ToDouble(o.Value, CultureInfo.InvariantCulture);
                    value += v;
                }
            }

            return CommonReturnTypeDto.GetNew(true, value);             
        }

        /// <summary>
        /// Parse Message from Doge transaction (from OP_RETURN)
        /// </summary>
        /// <param name="txinfo"></param>
        /// <returns></returns>
        public static CommonReturnTypeDto ParseDogeMessage(GetTransactionInfoResponse txinfo)
        {
            if (txinfo == null)
                return CommonReturnTypeDto.GetNew(false, "No input data provided.");
            
            if (txinfo.Success != "success")
                return CommonReturnTypeDto.GetNew(false, "No input data provided.");

            if (txinfo.Transaction.Vout == null || txinfo.Transaction.Vout.Count == 0)        
                return CommonReturnTypeDto.GetNew(false, "No outputs in transaction.");

            foreach (var o in txinfo.Transaction.Vout)
            {
                if (!string.IsNullOrEmpty(o.Script) && o.Script.Contains("OP_RETURN"))
                {
                    var message = o.Script.Replace("OP_RETURN ", string.Empty);
                    var bytes = StringExt.HexStringToBytes(message);
                    var msg = Encoding.UTF8.GetString(bytes);
                    return CommonReturnTypeDto.GetNew(true, msg);
                }
            }

            return CommonReturnTypeDto.GetNew<string>();
        }

        ///////////////////////////////////////////
        // Tools for addresses

        /// <summary>
        /// Verify the Dogecoin address
        /// </summary>
        /// <param name="dogeAddress">Excpected Dogecoin address</param>
        /// <returns>true and Address if it is correct Dogecoin Address</returns>
        public static CommonReturnTypeDto ValidateDogeAddress(string dogeAddress)
        {
            try
            {
                if (string.IsNullOrEmpty(dogeAddress) || dogeAddress.Length < 34 || dogeAddress[0] != 'D')
                {
                    return CommonReturnTypeDto.GetNew<string>(); ;
                }

                var add = BitcoinAddress.Create(dogeAddress, Network);
                if (!string.IsNullOrEmpty(add.ToString()))
                {
                    return CommonReturnTypeDto.GetNew(true, add.ToString());
                }
            }
            catch (Exception)
            {
                return CommonReturnTypeDto.GetNew<string>();
            }
            return CommonReturnTypeDto.GetNew<string>();
        }

        /// <summary>
        /// Verify the Dogecoin private key
        /// </summary>
        /// <param name="privatekey">Excpected Dogecoin private key</param>
        /// <returns>true and NBitcoin.BitcoinSecret if it is correct Dogecoin private key</returns>
        public static CommonReturnTypeDto IsPrivateKeyValid(string privatekey)
        {
            try
            {
                if (string.IsNullOrEmpty(privatekey) || privatekey.Length < 52 || privatekey[0] != 'Q')
                    return CommonReturnTypeDto.GetNew<BitcoinSecret>();

                var sec = new BitcoinSecret(privatekey, Network);

                if (sec != null)
                    return CommonReturnTypeDto.GetNew(true, sec); 
            }
            catch (Exception)
            {
                return CommonReturnTypeDto.GetNew<BitcoinSecret>();
            }
            return CommonReturnTypeDto.GetNew<BitcoinSecret>();
        }

        /// <summary>
        /// Get Address from Dogecoin private key
        /// </summary>
        /// <param name="privatekey">Excpected Dogecoin private key</param>
        /// <returns>true and Address if it is correct Dogecoin private key</returns>
        public static CommonReturnTypeDto GetAddressFromPrivateKey(string privatekey)
        {
            try
            {
                var p = IsPrivateKeyValid(privatekey);
                if (p.Success)
                {
                    BitcoinSecret secret = (BitcoinSecret)p.Value;
                    var address = secret.PubKey.GetAddress(ScriptPubKeyType.Legacy, Network);
                    if (address != null)
                        return CommonReturnTypeDto.GetNew<string>(true, address.ToString());
                }
            }
            catch (Exception)
            {
                return CommonReturnTypeDto.GetNew<string>();
            }
            return CommonReturnTypeDto.GetNew<string>();
        }

        ///////////////////////////////////////////
        // calls of Doge API and helpers

        /// <summary>
        /// Returns private client for Neblio API. If it is null, it will create new instance.
        /// </summary>
        /// <returns></returns>
        private static IClient GetClient()
        {
            if (_client == null)
            {
                _client = new Client(httpClient);
            }

            return _client;
        }
    

        /// <summary>
        /// Broadcast of signed transaction. with chain.so
        /// </summary>
        /// <param name="data">tx hex</param>
        /// <returns></returns>
        public static async Task<string> ChainSoBroadcastTxAsync(ChainSoBroadcastTxRequest data)
        {
            var info = await GetClient().ChainSoBroadcastTxAsync(data);
            return info.Data.TxId;
        }

        /// <summary>
        /// Broadcast of signed transaction. with VENFT API
        /// </summary>
        /// <param name="data">tx hex</param>
        /// <returns></returns>
        public static async Task<string> VENFTBroadcastTxAsync(VENFTBroadcastTxRequest data)
        {
            var info = await GetClient().VENFTBroadcastTxAsync(data);
            return info.Data.TxId;
        }


        /// <summary>
        /// Return address info object. this object contains list of Utxos.
        /// </summary>
        /// <param name="addr"></param>
        /// <returns></returns>
        public static async Task<GetAddressUtxosResponse> AddressUtxosAsync(string addr)
        {
            var address = await GetClient().GetAddressUtxosAsync(addr);
            return address;
        }

        /// <summary>
        /// Return address spended transaction list. 
        /// </summary>
        /// <param name="addr"></param>
        /// <returns>This object contains list of spended Txs.</returns>
        public static async Task<List<SpentTx>> AddressSpendTxsAsync(string addr)
        {
            try
            {
                var txs = await GetClient().GetAddressSentTxAsync(addr);
                if (txs != null && txs?.Data != null)
                {
                    if (txs?.Data.Transactions != null)
                    {
                        return txs?.Data.Transactions.ToList();
                    }
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Cannot obtain dogecoin address info from the api.");
            }
            return new List<SpentTx>();
        }

        /// <summary>
        /// Return address received transaction list. 
        /// </summary>
        /// <param name="addr"></param>
        /// <returns>This object contains list of spended Txs.</returns>
        public static async Task<List<ReceivedTx>> AddressReceivedTxsAsync(string addr)
        {
            var txs = await GetClient().GetAddressReceivedTxAsync(addr);
            if (txs.Data != null && txs.Data.Transactions != null)
            {
                return txs.Data.Transactions.ToList();
            }

            return new List<ReceivedTx>();
        }

        /// <summary>
        /// Return transaction object
        /// </summary>
        /// <param name="txid"></param>
        /// <param name="fromMemory"></param>
        /// <returns></returns>
        public static async Task<GetTransactionInfoResponse> TransactionInfoAsync(string txid, bool fromMemory = false)
        {
            try
            {
                if (fromMemory)
                {
                    if (transactionDetails.TryGetValue(txid, out var txinfo))
                    {
                        if (txinfo.Transaction.Confirmations > 3)
                        {
                            return txinfo;
                        }
                        else
                        {
                            var tx = await GetClient().GetTransactionInfoAsync(txid);
                            if (tx != null)
                            {
                                transactionDetails.TryUpdate(txid, tx, txinfo);
                            }

                            return tx;
                        }
                    }
                    else
                    {
                        var tx = await GetClient().GetTransactionInfoAsync(txid);
                        if (tx != null)
                        {
                            transactionDetails.TryAdd(txid, tx);
                        }

                        return tx;
                    }
                }
                else
                {
                    var tx = await GetClient().GetTransactionInfoAsync(txid);
                    return tx;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Cannot obtain the data about the transaction from the API. " + ex.Message);
            }
            return new GetTransactionInfoResponse();
        }

        /// <summary>
        /// Returns list of spendable utxos which together match some input required amount for some transaction
        /// </summary>
        /// <param name="addr">address which has utxos for spend - sender in tx</param>
        /// <param name="minAmount">minimum amount of one utxo</param>
        /// <param name="requiredAmount">amount what must be collected even by multiple utxos</param>
        /// <returns></returns>
        public static async Task<ICollection<Utxo>> GetAddressSpendableUtxo(string addr, double minAmount = 0.0001, double requiredAmount = 0.0001)
        {
            var resp = new List<Utxo>();
            GetAddressUtxosResponse addinfo = null;
            try
            {
                addinfo = await GetClient().GetAddressUtxosAsync(addr);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Cannot obtain dogecoin address Utxos." + ex.Message);
                return null;
            }
            if (addinfo == null)
            {
                return null;
            }

            var utxos = addinfo.Data.Utxos;
            if (utxos == null)
            {
                return null;
            }

            utxos = utxos.OrderBy(u => Convert.ToDouble(u.Value, CultureInfo.InvariantCulture)).Reverse().ToList();

            var founded = 0.0;
            foreach (var utx in utxos)
            {
                var val = Convert.ToDouble(utx.Value, CultureInfo.InvariantCulture) * FromSatToMainRatio;
                if (utx.Confirmations > MinimumConfirmations && val > 10000)
                {
                    if (val > (minAmount * FromSatToMainRatio))
                    {
                        resp.Add(utx);
                        founded += (val / FromSatToMainRatio);
                        if (founded > requiredAmount)
                        {
                            return resp;
                        }
                    }
                }
            }
            return null;
        }
    }
}
