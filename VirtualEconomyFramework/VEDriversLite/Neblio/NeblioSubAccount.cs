﻿using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text;
using VEDriversLite.NFT;
using VEDriversLite.NeblioAPI;
using VEDriversLite.Bookmarks;
using VEDriversLite.Security;
using System.Threading.Tasks;
using System.Linq;
using VEDriversLite.Events;
using NBitcoin;
using VEDriversLite.NFT.Coruzant;

namespace VEDriversLite.Neblio
{
    public class NeblioSubAccount
    {
        /// <summary>
        /// Neblio Address hash
        /// </summary>
        public string Address { get; set; } = string.Empty;
        public string EKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// When this flag is set, account reload the Utxos state - inside autorefresh
        /// </summary>
        [JsonIgnore]
        public bool IsAutoRefreshActive { get; set; } = false;
        /// <summary>
        /// List of actual address NFTs. Based on Utxos list
        /// </summary>
        [JsonIgnore]
        public List<INFT> NFTs { get; set; } = new List<INFT>();
        /// <summary>
        /// Actual list of all Utxos on this address.
        /// </summary>
        [JsonIgnore]
        public List<Utxos> Utxos { get; set; } = new List<Utxos>();
        [JsonIgnore]
        public Bookmark BookmarkFromAccount { get; set; } = new Bookmark();
        [JsonIgnore]
        public bool IsInBookmark { get; set; } = false;
        /// <summary>
        /// Actual loaded address info. It has inside list of all transactions.
        /// </summary>
        [JsonIgnore]
        public GetAddressResponse AddressInfo { get; set; } = new GetAddressResponse();
        /// <summary>
        /// Actual loaded address info with list of Utxos. When utxos are loaded first, this is just fill with it to prevent not necessary API request.
        /// </summary>
        [JsonIgnore]
        public GetAddressInfoResponse AddressInfoUtxos { get; set; } = new GetAddressInfoResponse();
        /// <summary>
        /// Carrier for encrypted private key from storage and its password.
        /// </summary>
        [JsonIgnore]
        public EncryptionKey AccountKey { get; set; }
        /// <summary>
        /// Loaded Secret, NBitcoin Class which carry Public Key and Private Key
        /// </summary>
        [JsonIgnore]
        public BitcoinSecret Secret { get; set; }

        /// <summary>
        /// Total actual balance based on Utxos. This means sum of spendable and unconfirmed balances.
        /// </summary>
        public double TotalBalance { get; set; } = 0.0;
        /// <summary>
        /// Total spendable balance based on Utxos.
        /// </summary>
        public double TotalSpendableBalance { get; set; } = 0.0;
        /// <summary>
        /// Total balance which is now unconfirmed based on Utxos.
        /// </summary>
        public double TotalUnconfirmedBalance { get; set; } = 0.0;

        /// <summary>
        /// Total balance of VENFT tokens which can be used for minting purposes.
        /// </summary>
        public double SourceTokensBalance { get; set; } = 0.0;
        /// <summary>
        /// Total balance of VENFT tokens which can be used for minting purposes.
        /// </summary>
        public double CoruzantSourceTokensBalance { get; set; } = 0.0;

        /// <summary>
        /// Actual all token supplies. Consider also other tokens than VENFT.
        /// </summary>
        public Dictionary<string, TokenSupplyDto> TokensSupplies { get; set; } = new Dictionary<string, TokenSupplyDto>();

        /// <summary>
        /// This event is called whenever some important thing happen. You can obtain success, error and info messages.
        /// </summary>
        public event EventHandler<IEventInfo> NewEventInfo;

        /// <summary>
        /// Invoke Success message info event
        /// </summary>
        /// <param name="txid">new tx id hash</param>
        /// <param name="title">Title of the event message</param>
        /// <returns></returns>
        private async Task InvokeSendPaymentSuccessEvent(string txid, string title = "Neblio Payment Sent")
        {
            NewEventInfo?.Invoke(this,
                        await EventFactory.GetEvent(EventType.Info,
                                                    title,
                                                    $"Successfull send. Please wait a while for enough confirmations.",
                                                    Address,
                                                    txid,
                                                    100));
        }

        /// <summary>
        /// Invoke Error message because account is locked
        /// </summary>
        /// <param name="title">Title of the event message</param>
        /// <returns></returns>
        private async Task InvokeAccountLockedEvent(string title = "Cannot send transaction")
        {
            NewEventInfo?.Invoke(this,
                        await EventFactory.GetEvent(EventType.Error,
                                                    title,
                                                    "Account is Locked. Please unlock it in account page.",
                                                    Address,
                                                    string.Empty,
                                                    100));
        }
        /// <summary>
        /// Invoke Error message which occured during sending of the transaction
        /// </summary>
        /// <param name="errorMessage">Error message content</param>
        /// <param name="title">Title of the event message</param>
        /// <returns></returns>
        private async Task InvokeErrorDuringSendEvent(string errorMessage, string title = "Cannot send transaction")
        {
            NewEventInfo?.Invoke(this,
                        await EventFactory.GetEvent(EventType.Error,
                                                    title,
                                                    errorMessage,
                                                    Address,
                                                    string.Empty,
                                                    100));
        }
        /// <summary>
        /// Invoke Common Error message
        /// </summary>
        /// <param name="errorMessage">Error message content</param>
        /// <param name="title">Title of the event message</param>
        /// <returns></returns>
        private async Task InvokeErrorEvent(string errorMessage, string title = "Error")
        {
            NewEventInfo?.Invoke(this,
                        await EventFactory.GetEvent(EventType.Error,
                                                    title,
                                                    errorMessage,
                                                    Address,
                                                    string.Empty,
                                                    100));
        }

        public async Task<(bool,string)> CreateAddress(BitcoinSecret mainSecret, string name)
        {
            if (!string.IsNullOrEmpty(Address))
                return (false, "Account already contains address.");

            try
            {
                Key privateKey = new Key(); // generate a random private key
                PubKey publicKey = privateKey.PubKey;
                BitcoinSecret privateKeyFromNetwork = privateKey.GetBitcoinSecret(NeblioTransactionHelpers.Network);
                var address = publicKey.GetAddress(ScriptPubKeyType.Legacy, NeblioTransactionHelpers.Network);
                Address = address.ToString();
                Secret = privateKeyFromNetwork;
                // todo load already encrypted key
                AccountKey = new Security.EncryptionKey(privateKeyFromNetwork.ToString());
                AccountKey.PublicKey = Address;
                EKey = mainSecret.PubKey.Encrypt(privateKeyFromNetwork.ToString());
                Name = name;
                return (true, Address);
            }
            catch(Exception ex)
            {
                //todo
                return (false, ex.Message);
            }
        }

        public async Task<(bool,string)> LoadFromBackupString(BitcoinSecret mainSecret, string inputData)
        {
            try
            {
                var dto = JsonConvert.DeserializeObject<AccountExportDto>(inputData);
                if (dto == null)
                    return (false, "Cannot load SubAccount from backup. Wrong input data.");

                Address = dto.Address;
                EKey = dto.EKey;
                Name = dto.Name;
                var key = mainSecret.PrivateKey.Decrypt(dto.EKey);
                AccountKey = new EncryptionKey(key);
                AccountKey.PublicKey = Address;
                Secret = new BitcoinSecret(key, NeblioTransactionHelpers.Network);
                return (true, Address);
            }
            catch (Exception ex)
            {
                //todo
                return (false, ex.Message);
            }
        }

        public async Task<(bool, string)> LoadFromBackupDto(BitcoinSecret mainSecret, AccountExportDto dto)
        {
            try
            {
                if (dto == null)
                    return (false, "Cannot load SubAccount from backup. Wrong input data.");

                Address = dto.Address;
                EKey = dto.EKey;
                Name = dto.Name;
                var key = mainSecret.PrivateKey.Decrypt(dto.EKey);
                AccountKey = new EncryptionKey(key);
                AccountKey.PublicKey = Address;
                Secret = new BitcoinSecret(key, NeblioTransactionHelpers.Network);
                return (true, Address);
            }
            catch (Exception ex)
            {
                //todo
                return (false, ex.Message);
            }
        }

        public async Task<(bool,string)> BackupAddressToString()
        {
            if (string.IsNullOrEmpty(Address) || string.IsNullOrEmpty(EKey))
                return (false, "Account is not loaded.");

            try
            {
                var dto = new AccountExportDto()
                {
                    Address = Address,
                    EKey = EKey,
                    Name = Name
                };
                var res = JsonConvert.SerializeObject(dto);
                return (true, res);
            }
            catch(Exception ex)
            {
                return (false, "Cannot backup address from SubAccount. " + ex.Message);
            }
        }

        public async Task<(bool, AccountExportDto)> BackupAddressToDto()
        {
            if (string.IsNullOrEmpty(Address) || string.IsNullOrEmpty(EKey))
                return (false, null);

            try
            {
                var dto = new AccountExportDto()
                {
                    Address = Address,
                    EKey = EKey,
                    Name = Name
                };
                return (true, dto);
            }
            catch (Exception ex)
            {
                return (false, null);
            }
        }

        public void LoadBookmark(Bookmark bkm)
        {
            if (!string.IsNullOrEmpty(bkm.Address) && !string.IsNullOrEmpty(bkm.Name))
            {
                IsInBookmark = true;
                BookmarkFromAccount = bkm;
            }
            else
                ClearBookmark();
        }
        public void ClearBookmark()
        {
            IsInBookmark = false;
            BookmarkFromAccount = new Bookmark();
        }

        /// <summary>
        /// This function will load the actual data and then run the task which periodically refresh this data.
        /// It doesnt have cancellation now!
        /// </summary>
        /// <param name="interval">Default interval is 3000 = 3 seconds</param>
        /// <returns></returns>
        public async Task<string> StartRefreshingData(int interval = 3000)
        {
            if (string.IsNullOrEmpty(Address) || !AccountKey.IsLoaded)
            {
                await InvokeErrorEvent("Please fill subaccount Address and Key first.", "Not loaded address and key.");
                return await Task.FromResult("Please fill subaccount Address and Key first.");
            }

            try
            {
                AddressInfo = new GetAddressResponse();
                AddressInfo.Transactions = new List<string>();
                await ReloadUtxos();
                await ReLoadNFTs();
                await ReloadTokenSupply();
            }
            catch (Exception ex)
            {
                // todo
            }

            var minorRefresh = 5;

            // todo cancelation token
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        if (IsAutoRefreshActive)
                        {
                            await ReloadUtxos();
                            await ReLoadNFTs();
                            await ReloadTokenSupply();
                        }
                    }
                    catch (Exception ex)
                    {
                        //await InvokeErrorEvent(ex.Message, "Unknown Error During Refreshing Data");
                    }

                    await Task.Delay(interval);
                }

            });

            return await Task.FromResult("RUNNING");
        }

        /// <summary>
        /// Reload address Utxos list. It will sort descending the utxos based on the utxos time stamps.
        /// </summary>
        /// <returns></returns>
        public async Task ReloadUtxos()
        {
            var ux = await NeblioTransactionHelpers.GetAddressUtxosObjects(Address);
            var ouxox = ux.OrderBy(u => u.Blocktime).Reverse().ToList();
            AddressInfoUtxos = new GetAddressInfoResponse()
            {
                Utxos = ouxox
            };

            if (ouxox.Count > 0)
            {
                Utxos.Clear();
                TotalBalance = 0.0;
                TotalUnconfirmedBalance = 0.0;
                TotalSpendableBalance = 0.0;
                // add new ones
                foreach (var u in ouxox)
                {
                    Utxos.Add(u);
                    if (u.Blockheight <= 0)
                        TotalUnconfirmedBalance += ((double)u.Value / NeblioTransactionHelpers.FromSatToMainRatio);
                    else
                        TotalSpendableBalance += ((double)u.Value / NeblioTransactionHelpers.FromSatToMainRatio);
                }

                TotalBalance = TotalSpendableBalance + TotalUnconfirmedBalance;
            }
        }

        /// <summary>
        /// This function will reload changes in the NFTs list based on provided list of already loaded utxos.
        /// </summary>
        /// <returns></returns>
        public async Task ReLoadNFTs()
        {
            if (!string.IsNullOrEmpty(Address))
                NFTs = await NFTHelpers.LoadAddressNFTs(Address, Utxos.ToList(), NFTs.ToList());
        }

        /// <summary>
        /// Reload actual token supplies based on already loaded list of address utxos
        /// </summary>
        /// <returns></returns>
        public async Task ReloadTokenSupply()
        {
            TokensSupplies = await NeblioTransactionHelpers.CheckTokensSupplies(Address, AddressInfoUtxos);
            if (TokensSupplies.TryGetValue(CoruzantNFTHelpers.CoruzantTokenId, out var ts))
                CoruzantSourceTokensBalance = ts.Amount;
        }

        /// <summary>
        /// This function will check if there is some spendable neblio of specific amount and returns list of the utxos for the transaction
        /// </summary>
        /// <param name="amount"></param>
        /// <returns></returns>
        public async Task<(string, ICollection<Utxos>)> CheckSpendableNeblio(double amount)
        {
            try
            {
                var nutxos = await NeblioTransactionHelpers.GetAddressNeblUtxo(Address, 0.0002, amount);
                if (nutxos == null || nutxos.Count == 0)
                    return ($"You dont have Neblio on the address. Probably waiting for more than {NeblioTransactionHelpers.MinimumConfirmations} confirmations.", null);
                else
                    return ("OK", nutxos);
            }
            catch (Exception ex)
            {
                return ("Cannot check spendable Neblio. " + ex.Message, null);
            }
        }

        /// <summary>
        /// This function will check if there is some spendable tokens of specific Id and amount and returns list of the utxos for the transaction.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public async Task<(string, ICollection<Utxos>)> CheckSpendableNeblioTokens(string id, int amount)
        {
            try
            {
                var tutxos = await NeblioTransactionHelpers.FindUtxoForMintNFT(Address, id, amount);
                if (tutxos == null || tutxos.Count == 0)
                    return ($"You dont have Tokens on the address. You need at least 5 for minting. Probably waiting for more than {NeblioTransactionHelpers.MinimumConfirmations} confirmations.", null);
                else
                    return ("OK", tutxos);
            }
            catch (Exception ex)
            {
                return ("Cannot check spendable Neblio Tokens. " + ex.Message, null);
            }
        }

        /// <summary>
        /// Mint new NFT. It is automatic function which will decide what NFT to mint based on provided type in the NFT input
        /// </summary>
        /// <param name="tokenId"></param>
        /// <param name="NFT">Input carrier of NFT data. It must specify the type</param>
        /// <returns></returns>
        public async Task<(bool, string)> MintNFT(INFT NFT)
        {
            var nft = await NFTFactory.CloneNFT(NFT);

            if (!AccountKey.IsLoaded)
            {
                await InvokeAccountLockedEvent();
                return (false, "SubAccount is locked.");
            }
            var res = await CheckSpendableNeblio(0.001);
            if (res.Item2 == null)
            {
                await InvokeErrorDuringSendEvent(res.Item1, "Not enought spendable Neblio inputs");
                return (false, res.Item1);
            }
            var tres = await CheckSpendableNeblioTokens(NFT.TokenId, 2);
            if (tres.Item2 == null)
            {
                await InvokeErrorDuringSendEvent(tres.Item1, "Not enought spendable Token inputs");
                return (false, tres.Item1);
            }

            try
            {
                var rtxid = await NFTHelpers.MintNFT(Address, AccountKey, nft, res.Item2, tres.Item2);

                if (rtxid != null)
                {
                    await InvokeSendPaymentSuccessEvent(rtxid, "Neblio NFT Minted");
                    return (true, rtxid);
                }
            }
            catch (Exception ex)
            {
                await InvokeErrorDuringSendEvent(ex.Message, "Unknown Error");
                return (false, ex.Message);
            }
            await InvokeErrorDuringSendEvent("Unknown Error", "Unknown Error");
            return (false, "Unexpected error during send.");
        }

        /// <summary>
        /// This function will destroy provided NFTs. It means it will connect them again to one output/lot of tokens.
        /// Now it is possible to destroy just same kind of tokens. The first provided NFT will define TokenId. Different tokensIds will be skipped.
        /// Maximum to destroy in one transaction is 10 of NFTs
        /// </summary>
        /// <param name="tokenId">Token Id hash</param>
        /// <param name="nfts">List of NFTs</param>
        /// <returns></returns>
        public async Task<(bool, string)> DestroyNFTs(ICollection<INFT> nfts, string receiver = "")
        {
            if (!AccountKey.IsLoaded)
            {
                await InvokeAccountLockedEvent();
                return (false, "SubAccount is locked.");
            }
            var res = await CheckSpendableNeblio(0.001);
            if (res.Item2 == null)
            {
                await InvokeErrorDuringSendEvent(res.Item1, "Not enought spendable Neblio inputs");
                return (false, res.Item1);
            }

            try
            {
                // send tx
                var rtxid = await NFTHelpers.DestroyNFTs(Address, AccountKey, nfts, res.Item2, receiver);
                if (rtxid != null)
                {
                    await InvokeSendPaymentSuccessEvent(rtxid, "NFTs Destroyed.");
                    return (true, rtxid);
                }
            }
            catch (Exception ex)
            {
                await InvokeErrorDuringSendEvent(ex.Message, "Unknown Error");
                return (false, ex.Message);
            }

            await InvokeErrorDuringSendEvent("Unknown Error", "Unknown Error");
            return (false, "Unexpected error during send.");
        }

        /// <summary>
        /// Change Post NFT. It requeires previous loadedPost NFT as input.
        /// </summary>
        /// <param name="NFT"></param>
        /// <returns></returns>
        public async Task<(bool, string)> ChangeNFT(INFT NFT)
        {
            var nft = await NFTFactory.CloneNFT(NFT);

            if (!AccountKey.IsLoaded)
            {
                await InvokeAccountLockedEvent();
                return (false, "Account is locked.");
            }
            if (string.IsNullOrEmpty(NFT.Utxo))
            {
                await InvokeErrorDuringSendEvent("Cannot change Post NFT without provided Utxo TxId.", "Cannot change the Post NFT");
                return (false, "Cannot change NFT without provided Utxo TxId.");
            }
            var res = await CheckSpendableNeblio(0.001);
            if (res.Item2 == null)
            {
                await InvokeErrorDuringSendEvent(res.Item1, "Not enought spendable Neblio inputs");
                return (false, res.Item1);
            }

            try
            {
                var rtxid = await NFTHelpers.ChangeNFT(Address, AccountKey, nft, res.Item2);

                if (rtxid != null)
                {
                    await InvokeSendPaymentSuccessEvent(rtxid, "NFT Post Changed");
                    return (true, rtxid);
                }
            }
            catch (Exception ex)
            {
                await InvokeErrorDuringSendEvent(ex.Message, "Unknown Error");
                return (false, ex.Message);
            }

            await InvokeErrorDuringSendEvent("Unknown Error", "Unknown Error");
            return (false, "Unexpected error during send.");
        }

        /// <summary>
        /// Send input NFT to new owner, or use it just for price write and resend it to yourself with new metadata about the price.
        /// </summary>
        /// <param name="receiver">If the pricewrite is set, this is filled with own address</param>
        /// <param name="NFT"></param>
        /// <param name="priceWrite">Set this if you need to just write the price to the NFT</param>
        /// <param name="price">Price must be bigger than 0.0002 NEBL</param>
        /// <returns></returns>
        public async Task<(bool, string)> SendNFT(string receiver, INFT NFT, bool priceWrite, double price)
        {
            var nft = await NFTFactory.CloneNFT(NFT);

            if (!AccountKey.IsLoaded)
            {
                await InvokeAccountLockedEvent();
                return (false, "SubAccount is locked.");
            }
            if (string.IsNullOrEmpty(NFT.Utxo))
            {
                await InvokeErrorDuringSendEvent("Cannot snd NFT without provided Utxo TxId.", "Cannot send NFT");
                return (false, "Cannot send NFT without provided Utxo TxId.");
            }
            var res = await CheckSpendableNeblio(0.001);
            if (res.Item2 == null)
            {
                await InvokeErrorDuringSendEvent(res.Item1, "Not enought spendable Neblio inputs");
                return (false, res.Item1);
            }

            if (string.IsNullOrEmpty(receiver) || priceWrite)
                receiver = Address;

            try
            {
                var rtxid = await NFTHelpers.SendNFT(Address, receiver, AccountKey, nft, priceWrite, res.Item2, price);

                if (rtxid != null)
                {
                    if (!priceWrite)
                        await InvokeSendPaymentSuccessEvent(rtxid, "NFT Sent");
                    else
                        await InvokeSendPaymentSuccessEvent(rtxid, "Price written to NFT");
                    return (true, rtxid);
                }
            }
            catch (Exception ex)
            {
                await InvokeErrorDuringSendEvent(ex.Message, "Unknown Error");
                return (false, ex.Message);
            }

            await InvokeErrorDuringSendEvent("Unknown Error", "Unknown Error");
            return (false, "Unexpected error during send.");
        }

        /// <summary>
        /// Add comment to Coruzant Post NFT or add comment and send it to new owner
        /// </summary>
        /// <param name="receiver">Fill when you want to send to different address</param>
        /// <param name="NFT"></param>
        /// <param name="commentWrite">Set this if you need to just write the comment to the NFT</param>
        /// <param name="comment">Add your comment</param>
        /// <returns></returns>
        public async Task<(bool, string)> SendCoruzantNFT(string receiver, INFT NFT, bool commentWrite, string comment = "")
        {
            var nft = await NFTFactory.CloneNFT(NFT);

            if (!AccountKey.IsLoaded)
            {
                await InvokeAccountLockedEvent();
                return (false, "SubAccount is not loaded.");
            }
            if (string.IsNullOrEmpty(NFT.Utxo))
            {
                await InvokeErrorDuringSendEvent("Cannot snd NFT without provided Utxo TxId.", "Cannot send NFT");
                return (false, "Cannot send NFT without provided Utxo TxId.");
            }
            var res = await CheckSpendableNeblio(0.001);
            if (res.Item2 == null)
            {
                await InvokeErrorDuringSendEvent(res.Item1, "Not enought spendable Neblio inputs");
                return (false, res.Item1);
            }

            if (string.IsNullOrEmpty(receiver))
                receiver = string.Empty;

            try
            {
                var rtxid = await CoruzantNFTHelpers.ChangeCoruzantPostNFT(Address, AccountKey, nft, res.Item2, receiver);

                if (rtxid != null)
                {
                    if (!commentWrite)
                        await InvokeSendPaymentSuccessEvent(rtxid, "NFT Sent");
                    else
                        await InvokeSendPaymentSuccessEvent(rtxid, "Comment written to NFT");
                    return (true, rtxid);
                }
            }
            catch (Exception ex)
            {
                await InvokeErrorDuringSendEvent(ex.Message, "Unknown Error");
                return (false, ex.Message);
            }

            await InvokeErrorDuringSendEvent("Unknown Error", "Unknown Error");
            return (false, "Unexpected error during send.");
        }

        /// <summary>
        /// Write Used flag into NFT Ticket
        /// </summary>
        /// <param name="NFT"></param>
        /// <returns></returns>
        public async Task<(bool, string)> UseNFTTicket(INFT NFT)
        {
            if (NFT.Type != NFTTypes.Ticket)
                throw new Exception("This is not NFT ticket.");

            var nft = await NFTFactory.CloneNFT(NFT);

            if (!AccountKey.IsLoaded)
            {
                await InvokeAccountLockedEvent();
                return (false, "SubAccount is not loaded.");
            }
            if (string.IsNullOrEmpty(NFT.Utxo))
            {
                await InvokeErrorDuringSendEvent("Cannot snd NFT without provided Utxo TxId.", "Cannot send NFT");
                return (false, "Cannot send NFT without provided Utxo TxId.");
            }
            var res = await CheckSpendableNeblio(0.001);
            if (res.Item2 == null)
            {
                await InvokeErrorDuringSendEvent(res.Item1, "Not enought spendable Neblio inputs");
                return (false, res.Item1);
            }

            try
            {
                var rtxid = await NFTHelpers.UseNFTTicket(Address, AccountKey, nft, res.Item2);

                if (rtxid != null)
                {
                    await InvokeSendPaymentSuccessEvent(rtxid, "NFT Ticket used.");
                    return (true, rtxid);
                }
            }
            catch (Exception ex)
            {
                await InvokeErrorDuringSendEvent(ex.Message, "Unknown Error");
                return (false, ex.Message);
            }

            await InvokeErrorDuringSendEvent("Unknown Error", "Unknown Error");
            return (false, "Unexpected error during send.");
        }

        /// <summary>
        /// Obtain verify code of some transaction. This will combine txid and UTC time (rounded to minutes) and sign with the private key.
        /// It will create unique code, which can be verified and it is valid just one minute.
        /// </summary>
        /// <param name="txid"></param>
        /// <returns></returns>
        public async Task<OwnershipVerificationCodeDto> GetNFTVerifyCode(string txid)
        {
            var res = await OwnershipVerifier.GetCodeInDto(txid, Secret);
            if (res != null)
                return res;
            else
                return new OwnershipVerificationCodeDto();
        }
        /// <summary>
        /// Verification function for the NFT ownership code generated by GetNFTVerifyCode function.
        /// </summary>
        /// <param name="txid"></param>
        /// <returns></returns>
        public async Task<(OwnershipVerificationCodeDto, byte[])> GetNFTVerifyQRCode(string txid)
        {
            var res = await OwnershipVerifier.GetQRCode(txid, Secret);
            if (res.Item1)
                return (res.Item2.Item1, res.Item2.Item2);
            else
                return (new OwnershipVerificationCodeDto(), new byte[0]);
        }

    }
}