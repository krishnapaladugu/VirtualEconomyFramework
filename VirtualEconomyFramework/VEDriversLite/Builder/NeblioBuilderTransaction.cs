﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Newtonsoft.Json;
using VEDriversLite.NeblioAPI;
using VEDriversLite.Security;

namespace VEDriversLite.Builder
{
    public class NewTokenTxReceiver
    {
        public string Address { get; set; } = string.Empty;
        public string TokenId { get; set; } = NeblioTransactionHelpers.VENFTId;
        public int Amount { get; set; } = 1;
    }
    public class NewTokenTxUtxo
    {
        public string Utxo { get; set; } = string.Empty;
        public string TokenId { get; set; } = string.Empty;
        public int Tokens { get; set; } = 0;
    }
    public class NewTokenTxMetaField
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
    public class NeblioBuilderTransaction
    {
        public NeblioBuilderAddress Sender { get; set; }
        public ConcurrentDictionary<string, NeblioBuilderAddress> Receivers { get; set; } = new ConcurrentDictionary<string, NeblioBuilderAddress>();

        public ConcurrentDictionary<string, NeblioBuilderUtxo> Inputs { get; set; } = new ConcurrentDictionary<string, NeblioBuilderUtxo>();
        public List<NeblioBuilderOutput> Outputs { get; set; } = new List<NeblioBuilderOutput>();
        public Transaction NewTransaction { get; set; } = Transaction.Create(NeblioTransactionBuilder.NeblioNetwork);
        public TransactionInputSummary InputSummary { get; set; } = new TransactionInputSummary();
        public TransactionOutputSummary OutputSummary { get; set; } = new TransactionOutputSummary();
        public string TxHex { get; set; } = string.Empty;
        public string TxHexSigned { get; set; } = string.Empty;
        public string TxString { get; set; } = string.Empty;
        public string TxStringSigned { get; set; } = string.Empty;
        public string OP_RETURN { get; set; } = string.Empty;
        public double ActualDifference
        {
            get
            {
                return (InputSummary.TotalNeblioAmount - OutputSummary.TotalNeblioAmount);
            }
        }

        public event EventHandler SenderRefreshed;

        public async Task<string> AddSender(string address)
        {
            try
            {
                if (Sender != null)
                    if (!string.IsNullOrEmpty(Sender.Address))
                        Sender.Refreshed -= NeblioBuilderTransaction_SenderRefreshed;

                Sender = new NeblioBuilderAddress(address);
                Sender.Refreshed += NeblioBuilderTransaction_SenderRefreshed;
                await Sender.LoadUtxos();
                Sender.StartRefreshingData();
                return "OK";
            }
            catch(Exception ex)
            {
                return ex.Message;
            }
        }

        private void NeblioBuilderTransaction_SenderRefreshed(object sender, EventArgs e)
        {
            SenderRefreshed?.Invoke(this, null);
        }

        public async Task<string> AddReceiver(string address)
        {
            try
            {
                var r = new NeblioBuilderAddress(address);
                Receivers.TryAdd(address, r);
                return "OK";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<string> RemoveReceiver(string address)
        {
            try
            {
                Receivers.TryRemove(address, out var naddress);
                return "OK";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<string> GetTransactionHex()
        {
            return NewTransaction.ToHex();
        }
        public async Task<string> GetTransactionString()
        {
            var str = NewTransaction.ToString();
            var formated = JsonConvert.SerializeObject(JsonConvert.DeserializeObject<object>(str), Formatting.Indented);
            return formated;
        }

        public async Task RefreshInputSummary()
        {
            InputSummary.TotalNeblioAmount = 0.0;
            InputSummary.Tokens.Clear();
            foreach (var i in Inputs.Values)
            {
                InputSummary.TotalNeblioAmount += (double)i.Utxo.Value;
                if (i.Utxo.Tokens != null)
                {
                    foreach (var t in i.Utxo.Tokens)
                    {
                        var ti = i.TokenInfo.Where(tio => tio.TokenId == t.TokenId).ToList()?.FirstOrDefault();
                        if (ti != null)
                            InputSummary.Tokens.Add(ti, (int)t.Amount);
                    }
                }
            }

            TxHex = await GetTransactionHex();
            TxString = await GetTransactionString();
        }

        public async Task RefreshOutputSummary()
        {
            OutputSummary.TotalNeblioAmount = 0.0;
            OutputSummary.Tokens.Clear();
            foreach (var o in Outputs)
            {
                OutputSummary.TotalNeblioAmount += (double)o.Amount;
                if (o.WithTokens)
                {
                    foreach (var t in InputSummary.Tokens)
                    {
                        if (t.Key.TokenId == o.TokenId)
                            OutputSummary.Tokens.Add(t.Key, o.TokenAmount);
                    }
                }
            }

            TxHex = await GetTransactionHex();
            TxString = await GetTransactionString();
        }

        public async Task<string> AddInput(string utxid, int nindex)
        {
            string uid = utxid + ":" + nindex.ToString();
            if (Sender.Utxos.TryGetValue(uid, out var utxo))
            {
                utxo.Used = true;
                Inputs.TryAdd(utxid + ":" + nindex.ToString(), utxo);
                NewTransaction.Inputs.Add(new TxIn(new OutPoint(uint256.Parse(utxid), (int)utxo.Utxo.Index)));
                NewTransaction.Inputs.Last().ScriptSig = Sender.NBitcoinAddress.ScriptPubKey;
                await RefreshInputSummary();
            }
            else
                return "Wrong Input Utxo TxId.";

            return "OK";
        }

        public async Task<string> Sign(string inpkey)
        {
            BitcoinSecret key = null;
            try
            {
                key = NeblioTransactionBuilder.NeblioNetwork.CreateBitcoinSecret(inpkey);
            }
            catch (Exception ex)
            {
                throw new Exception("Cannot sign token transaction. cannot create keys!");
            }

            List<ICoin> coins = new List<ICoin>();
            try
            {
                // add all spendable coins of this address
                foreach (var inp in Sender.Utxos.Values)
                    coins.Add(new Coin(uint256.Parse(inp.Utxo.Txid), (uint)inp.Utxo.Index, new Money((int)inp.Utxo.Value), Sender.NBitcoinAddress.ScriptPubKey));

                // add signature to inputs before signing
                foreach (var inp in NewTransaction.Inputs)
                    inp.ScriptSig = Sender.NBitcoinAddress.ScriptPubKey;
            }
            catch (Exception ex)
            {
                throw new Exception("Exception during loading inputs. " + ex.Message);
            }

            NewTransaction.Sign(key, coins);

            TxHexSigned = NewTransaction.ToHex();
            var str = NewTransaction.ToString();
            TxStringSigned = JsonConvert.SerializeObject(JsonConvert.DeserializeObject<object>(str), Formatting.Indented);

            return "OK";
        }

        public async Task<string> Broadcast()
        {
            // broadcast
            try
            {
                if (!string.IsNullOrEmpty(TxHexSigned))
                {
                    var res = await NeblioTransactionHelpers.BroadcastSignedTransaction(TxHexSigned);
                    return res;
                }
                return "Cannot broadcast unsigned transaction. ";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task<string> CreateRawTransaction(List<NewTokenTxUtxo> utxos,
                                                      List<NewTokenTxReceiver> receivers,
                                                      List<NewTokenTxMetaField> metadata)
        {
            try
            {
                var res = await NeblioTransactionBuilder.CreateRawTransaction(utxos, receivers, metadata);
                NewTransaction = res;
                return "OK";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public async Task LoadTransaction(Transaction tx)
        {
            try
            {
                if (Inputs.Count > 0)
                    foreach (var i in Inputs)
                        await RemoveInput(i.Value.Utxo.Txid, (int)i.Value.Utxo.Index);
            }
            catch (Exception ex)
            {
                //
            }

            foreach (var inp in tx.Inputs)
                await AddInput(inp.PrevOut.Hash.ToString(), (int)inp.PrevOut.N);

            try
            {
                if (Outputs.Count > 0)
                    foreach (var i in Outputs)
                        await RemoveOutput(Outputs.IndexOf(i));
            }
            catch(Exception ex)
            {

            }

            foreach (var o in tx.Outputs)
            {
                await AddNeblioOutput((int)o.Value.Satoshi);
                NewTransaction.Outputs[tx.Outputs.IndexOf(o)].ScriptPubKey = o.ScriptPubKey;
                if (o.ScriptPubKey.ToString().Contains("OP_RETURN"))
                    OP_RETURN = o.ScriptPubKey.ToString();
            }

            await RefreshInputSummary();
            await RefreshOutputSummary();
            SenderRefreshed?.Invoke(this, null);
        }

        public async Task<string> RemoveInput(string utxid, int nindex)
        {
            string uid = utxid + ":" + nindex.ToString();
            if (Inputs.TryRemove(uid, out var utxo))
            {
                if (Sender.Utxos.TryGetValue(uid, out var nutxo))
                    nutxo.Used = false;

                var inp = NewTransaction.Inputs.Where(i => i.PrevOut.N == nindex && i.PrevOut.Hash == uint256.Parse(utxid)).ToList()?.FirstOrDefault();
                NewTransaction.Inputs.Remove(inp);

                await RefreshInputSummary();
                return "OK";
            }
            else
                return "Wrong Input Utxo TxId. Cannot find it in the inputs.";
        }


        public async Task AddNeblioOutput(int amount = 10000)
        {
            Outputs.Add(new NeblioBuilderOutput(amount));
            NewTransaction.Outputs.Add(new TxOut(new Money(amount), new Script()));
            await RefreshOutputSummary();
        }

        public async Task RemoveOutput(int nindex)
        {
            if (Outputs.Count > nindex)
            {
                Outputs.RemoveAt(nindex);
                NewTransaction.Outputs.RemoveAt(nindex);
                await RefreshOutputSummary();
            }
        }

        private class tokenUrlCarrier
        {
            public string name { get; set; } = string.Empty;
            public string url { get; set; } = string.Empty;
            public string mimeType { get; set; } = string.Empty;
        }
        public string GetTokenImageUrl(string tokenId)
        {
            foreach (var u in Sender.Utxos.Values)
                foreach (var ti in u.TokenInfo)
                {
                    if (ti.TokenId == tokenId)
                    {
                        var tus = JsonConvert.DeserializeObject<List<tokenUrlCarrier>>(JsonConvert.SerializeObject(ti.MetadataOfIssuance.Data.Urls));
                        var img = string.Empty;
                        var tu = tus.FirstOrDefault();
                        if (tu != null)
                        {
                            img = tu.url;
                            return img;
                        }
                    }
                }

            return string.Empty;
        }

        public async Task AddNeblioTokensToOutput(string tokenId, int index, int amount = 1)
        {
            if (index < Outputs.Count)
            {
                var img = GetTokenImageUrl(tokenId);
                await Outputs[index].AddTokens(tokenId, amount, img);
            }

            await RefreshOutputSummary();
        }

        public async Task RemoveNeblioTokensFromOutput(int index)
        {
            if (index < Outputs.Count)
            {
                await Outputs[index].RemoveTokens();
            }

            await RefreshOutputSummary();
        }

        public async Task AddReceiverToOutput(string address, int index)
        {
            if (Receivers.TryGetValue(address, out var receiver))
                if (index < Outputs.Count)
                {
                    Outputs[index].NBitcoinAddress = receiver.NBitcoinAddress;
                    NewTransaction.Outputs[index].ScriptPubKey = receiver.NBitcoinAddress.ScriptPubKey;
                    await RefreshOutputSummary();
                }
        }

        public async Task RemoveReceiverFromOutput(int index)
        {
            if (index < Outputs.Count)
            {
                Outputs[index].NBitcoinAddress = null;
                NewTransaction.Outputs[index].ScriptPubKey = new Script();
                await RefreshOutputSummary();
            }
        }

    }
}
