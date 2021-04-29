﻿using Ipfs.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using VEDriversLite.Bookmarks;
using VEDriversLite.NeblioAPI;

namespace VEDriversLite.NFT
{
    public enum LoadingImageStages
    {
        NotStarted,
        Loading,
        Loaded
    }
    public class LoadNFTOriginDataDto
    {
        public string Utxo { get; set; } = string.Empty;
        public string SourceTxId { get; set; } = string.Empty;
        public string NFTOriginTxId { get; set; } = string.Empty;
        public Dictionary<string, string> NFTMetadata { get; set; } = new Dictionary<string, string>();
    }

    public static class NFTHelpers
    {
        private static HttpClient httpClient = new HttpClient();
        private static IClient _client;
        private static string BaseURL = "https://ntp1node.nebl.io/";
        private static string TokenId = "La58e9EeXUMx41uyfqk6kgVWAQq9yBs44nuQW8";
        private static string TokenSymbol = "VENFT";
        public static readonly IpfsClient ipfs = new IpfsClient("https://ipfs.infura.io:5001");

        public static async Task<LoadNFTOriginDataDto> LoadNFTOriginData(string utxo)
        {
            var result = new LoadNFTOriginDataDto();
            var txid = utxo;
            while (true)
            {
                try
                {
                    var check = await CheckIfMintTx(txid);
                    if (check.Item1)
                    {
                        var meta = await CheckIfContainsNFTData(txid);
                        if (meta != null)
                        {
                            result.NFTMetadata = meta;
                            result.SourceTxId = check.Item2;
                            result.NFTOriginTxId = txid;
                            return result;
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        txid = check.Item2;
                    }

                    await Task.Delay(1);
                }
                catch(Exception ex)
                {
                    Console.WriteLine("Problem in loading of NFT origin!");
                }
            }
        }

        public static async Task<LoadNFTOriginDataDto> LoadLastData(string utxo)
        {
            var result = new LoadNFTOriginDataDto();
            var txid = utxo;
            try
            {
                var meta = await NeblioTransactionHelpers.GetTransactionMetadata(TokenId, utxo);
                if (meta.TryGetValue("NFT", out var value))
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        if (value == "true")
                        {
                            result.NFTMetadata = meta;

                            if (meta.TryGetValue("SourceUtxo", out var sourceTxId))
                                result.NFTOriginTxId = sourceTxId;

                            return result;
                        }
                    }
                }
                if (meta.TryGetValue("SourceUtxo", out var srcTxId))
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        result.NFTMetadata = meta;
                        result.NFTOriginTxId = srcTxId;

                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Problem in loading of NFT origin!");
            }

            return null;
        }

        public static async Task<Dictionary<string,string>> CheckIfContainsNFTData(string utxo)
        {
            var meta = await NeblioTransactionHelpers.GetTransactionMetadata(TokenId, utxo);

            if (meta.TryGetValue("NFT", out var value))
            {
                if (!string.IsNullOrEmpty(value))
                {
                    if (value == "true")
                    {
                        return meta;
                    }
                }
            }

            return null;
        }

        public static async Task<(bool,string)> CheckIfMintTx(string utxo)
        {
            var info = await NeblioTransactionHelpers.GetTransactionInfo(utxo);

            if (info != null) {
                if (info.Vin != null)
                {
                    if (info.Vin.Count > 0)
                    {
                        var vin = info.Vin.ToArray()?[0];
                        if (vin.Tokens != null)
                        {
                            if (vin.Tokens.Count > 0)
                            {
                                var vintok = vin.Tokens.ToArray()?[0];
                                if (vintok != null)
                                {
                                    if (vintok.Amount > 1)
                                        return (true, vin.Txid);
                                    else if (vintok.Amount == 1)
                                        return (false, vin.Txid);
                                }
                            }
                        }
                    }
                }
            }
            
            return (false, string.Empty);
        }

        public static async Task<List<INFT>> LoadAddressNFTs(string address)
        {
            List<INFT> nfts = new List<INFT>();
            var utxos = await NeblioTransactionHelpers.GetAddressNFTsUtxos(address);

            foreach (var u in utxos)
            {
                NFTTypes ntype = NFTTypes.Image;

                var meta = await NeblioTransactionHelpers.GetTransactionMetadata(TokenId, u.Txid);
                if (meta.TryGetValue("Type", out var type))
                {
                    if (!string.IsNullOrEmpty(type))
                    {
                        switch (type)
                        {
                            case "NFT Profile":
                                ntype = NFTTypes.Profile;
                                break;
                            case "NFT Post":
                                ntype = NFTTypes.Post;
                                break;
                            case "NFT Image":
                                ntype = NFTTypes.Image;
                                break;
                        }
                    }
                }

                var nft = await NFTFactory.GetNFT(ntype, u.Txid);

                if (ntype == NFTTypes.Post)
                {
                    nft.TypeText = "NFT Post";
                    await (nft as PostNFT).GetLastData();
                }
                if (ntype == NFTTypes.Profile)
                {
                    nft.TypeText = "NFT Profile";
                    await (nft as ProfileNFT).GetLastData();
                }

                if (nft != null)
                {
                    if (!(nfts.Any(n => n.Utxo == nft.Utxo)))
                        nfts.Add(nft);
                }
            }

            return nfts;
        }

        public static async Task<List<ActiveTab>> GetTabs(string tabs)
        {
            return JsonConvert.DeserializeObject<List<ActiveTab>>(tabs);
        }

        public static async Task<string> SerializeTabs(List<ActiveTab> tabs)
        {
            return JsonConvert.SerializeObject(tabs);
        }


        public static async Task<string> MintImageNFT(NeblioAccount account, ImageNFT newNFT)
        {
            // create token metadata
            var metadata = new Dictionary<string, string>();
            metadata.Add("NFT", "true");
            metadata.Add("Name", newNFT.Name);
            metadata.Add("Author", newNFT.Author);
            metadata.Add("Description", newNFT.Description);
            metadata.Add("Image", newNFT.ImageLink);
            metadata.Add("Link", newNFT.Link);
            metadata.Add("Type", "NFT Image");

            // fill input data for sending tx
            var dto = new MintNFTData() // please check SendTokenTxData for another properties such as specify source UTXOs
            {
                Id = "La58e9EeXUMx41uyfqk6kgVWAQq9yBs44nuQW8", // id of token
                Metadata = metadata,
                Password = "", // put here your password
                SenderAddress = account.Address,
            };

            try
            {
                // send tx
                var rtxid = await NeblioTransactionHelpers.MintNFTTokenAsync(dto, account);
                if (rtxid != null)
                {
                    return rtxid;
                }
                else
                {
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static async Task<string> MintProfileNFT(NeblioAccount account, ProfileNFT profile)
        {
            // create token metadata
            var metadata = new Dictionary<string, string>();
            metadata.Add("NFT", "true");
            metadata.Add("Name", profile.Name);
            metadata.Add("Surname", profile.Surname);
            metadata.Add("Age", profile.Age.ToString());
            metadata.Add("Description", profile.Description);
            metadata.Add("Image", profile.ImageLink);
            metadata.Add("Link", profile.Link);
            metadata.Add("Type", "NFT Profile");

            // fill input data for sending tx
            var dto = new MintNFTData() // please check SendTokenTxData for another properties such as specify source UTXOs
            {
                Id = "La58e9EeXUMx41uyfqk6kgVWAQq9yBs44nuQW8", // id of token
                Metadata = metadata,
                Password = "", // put here your password
                SenderAddress = account.Address,
            };

            try
            {
                // send tx
                var rtxid = await NeblioTransactionHelpers.MintNFTTokenAsync(dto, account);
                if (rtxid != null)
                {
                    return rtxid;
                }
                else
                {
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static async Task<string> MintPostNFT(NeblioAccount account, PostNFT newNFT)
        {
            // create token metadata
            var metadata = new Dictionary<string, string>();
            metadata.Add("NFT", "true");
            metadata.Add("Name", newNFT.Name);
            metadata.Add("Author", newNFT.Author);
            metadata.Add("Description", newNFT.Description);
            metadata.Add("Image", newNFT.ImageLink);
            metadata.Add("Link", newNFT.Link);
            metadata.Add("Type", "NFT Post");

            // fill input data for sending tx
            var dto = new MintNFTData() // please check SendTokenTxData for another properties such as specify source UTXOs
            {
                Id = "La58e9EeXUMx41uyfqk6kgVWAQq9yBs44nuQW8", // id of token
                Metadata = metadata,
                Password = "", // put here your password
                SenderAddress = account.Address,
            };

            try
            {
                // send tx
                var rtxid = await NeblioTransactionHelpers.MintNFTTokenAsync(dto, account);
                if (rtxid != null)
                {
                    return rtxid;
                }
                else
                {
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static async Task<string> ChangeProfileNFT(NeblioAccount account, ProfileNFT profile)
        {
            // create token metadata
            var metadata = new Dictionary<string, string>();
            metadata.Add("NFT", "true");
            metadata.Add("Name", profile.Name);
            metadata.Add("Surname", profile.Surname);
            metadata.Add("Nickname", profile.Nickname);
            metadata.Add("Age", profile.Age.ToString());
            metadata.Add("Description", profile.Description);
            metadata.Add("Image", profile.ImageLink);
            metadata.Add("Link", profile.Link);
            metadata.Add("Type", "NFT Profile");

            var utxo = account.Profile.Utxo;

            // fill input data for sending tx
            var dto = new SendTokenTxData() // please check SendTokenTxData for another properties such as specify source UTXOs
            {
                Id = "La58e9EeXUMx41uyfqk6kgVWAQq9yBs44nuQW8", // id of token
                Metadata = metadata,
                Amount = 1,
                sendUtxo = new List<string>() { utxo },
                Password = "", // put here your password
                SenderAddress = account.Address,
                ReceiverAddress = account.Address
            };

            try
            {
                // send tx
                var rtxid = await NeblioTransactionHelpers.SendNTP1TokenAPIAsync(dto, account, isNFTtx:true, fee: 30000);
                if (rtxid != null)
                {
                    return rtxid;
                }
                else
                {
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static async Task<string> ChangePostNFT(NeblioAccount account, PostNFT postnft, string utxo)
        {
            // create token metadata
            var metadata = new Dictionary<string, string>();
            metadata.Add("NFT", "true");
            metadata.Add("Name", postnft.Name);
            metadata.Add("Author", postnft.Author);
            metadata.Add("Description", postnft.Description);
            metadata.Add("Image", postnft.ImageLink);
            metadata.Add("Link", postnft.Link);
            metadata.Add("Type", "NFT Post");

            // fill input data for sending tx
            var dto = new SendTokenTxData() // please check SendTokenTxData for another properties such as specify source UTXOs
            {
                Id = "La58e9EeXUMx41uyfqk6kgVWAQq9yBs44nuQW8", // id of token
                Metadata = metadata,
                Amount = 1,
                sendUtxo = new List<string>() { utxo },
                Password = "", // put here your password
                SenderAddress = account.Address,
                ReceiverAddress = account.Address
            };

            try
            {
                // send tx
                var rtxid = await NeblioTransactionHelpers.SendNTP1TokenAPIAsync(dto, account, isNFTtx: true, fee: 30000);
                if (rtxid != null)
                {
                    return rtxid;
                }
                else
                {
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static async Task<ProfileNFT> FindProfileNFT(NeblioAccount account)
        {
            if (account != null)
            {
                if (account.NFTs != null)
                {
                    foreach(var n in account.NFTs)
                    {
                        if (n.TypeText == "NFT Profile")
                        {
                            return (ProfileNFT)n;
                        }
                    }
                }
            }

            return new ProfileNFT("");
        }

        public static async Task<ProfileNFT> FindProfileNFT(List<INFT> nfts)
        {
            if (nfts != null)
            {
                foreach (var n in nfts)
                {
                    if (n.TypeText == "NFT Profile")
                    {
                        return (ProfileNFT)n;
                    }
                }
            }

            return new ProfileNFT("");
        }
    }
}
