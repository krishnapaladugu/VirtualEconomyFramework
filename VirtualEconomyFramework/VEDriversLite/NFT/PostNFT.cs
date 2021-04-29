﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace VEDriversLite.NFT
{
    public class PostNFT : CommonNFT
    {
        public PostNFT(string utxo)
        {
            Utxo = utxo;
            Type = NFTTypes.Post;
            TypeText = "NFT Post";
        }

        public void Fill(INFT nft) 
        {
            IconLink = nft.IconLink;
            ImageLink = nft.ImageLink;
            Name = nft.Name;
            Link = nft.Link;
            Description = nft.Description;
            Author = nft.Author;
            SourceTxId = nft.SourceTxId;
            Utxo = nft.Utxo;
        }

        public string Surname { get; set; } = string.Empty;

        public override async Task ParseOriginData()
        {
            var nftData = await NFTHelpers.LoadNFTOriginData(Utxo);
            if (nftData != null)
            {
                if (nftData.NFTMetadata.TryGetValue("Name", out var name))
                    Name = name;
                if (nftData.NFTMetadata.TryGetValue("Description", out var description))
                    Description = description;
                if (nftData.NFTMetadata.TryGetValue("Link", out var link))
                    Link = link;
                if (nftData.NFTMetadata.TryGetValue("Tags", out var tags))
                    Tags = tags;
                if (nftData.NFTMetadata.TryGetValue("Image", out var imagelink))
                    ImageLink = imagelink;
                if (nftData.NFTMetadata.TryGetValue("Type", out var type))
                    TypeText = type;

                SourceTxId = nftData.SourceTxId;
                NFTOriginTxId = nftData.NFTOriginTxId;
            }
        }

        public async Task GetLastData()
        {
            var nftData = await NFTHelpers.LoadLastData(Utxo);
            if (nftData != null)
            {
                if (nftData.NFTMetadata.TryGetValue("Name", out var name))
                    Name = name;
                if (nftData.NFTMetadata.TryGetValue("Description", out var description))
                    Description = description;
                if (nftData.NFTMetadata.TryGetValue("Link", out var link))
                    Link = link;
                if (nftData.NFTMetadata.TryGetValue("Tags", out var tags))
                    Tags = tags;
                if (nftData.NFTMetadata.TryGetValue("Image", out var imagelink))
                    ImageLink = imagelink;
                if (nftData.NFTMetadata.TryGetValue("Type", out var type))
                    TypeText = type;

                SourceTxId = nftData.SourceTxId;
                NFTOriginTxId = nftData.NFTOriginTxId;
            }
        }
    }
}
