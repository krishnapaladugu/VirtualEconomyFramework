﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace VEDriversLite.NFT
{
    public class ProfileNFT : CommonNFT
    {
        public ProfileNFT(string utxo)
        {
            Utxo = utxo;
            Type = NFTTypes.Profile;
            TypeText = "NFT Profile";
        }

        public int Age { get; set; } = 0;
        public string Surname { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
        public string RelationshipStatus { get; set; } = string.Empty;

        public override async Task ParseOriginData()
        {
            var nftData = await NFTHelpers.LoadNFTOriginData(Utxo);
            if (nftData != null)
            {
                if (nftData.NFTMetadata.TryGetValue("Name", out var name))
                    Name = name;
                if (nftData.NFTMetadata.TryGetValue("Surname", out var surname))
                    Surname = surname;
                if (nftData.NFTMetadata.TryGetValue("Nickname", out var nickname))
                    Nickname = nickname;
                if (nftData.NFTMetadata.TryGetValue("RelationshipStatus", out var relationshipStatus))
                    RelationshipStatus = relationshipStatus;
                if (nftData.NFTMetadata.TryGetValue("Description", out var description))
                    Description = description;
                if (nftData.NFTMetadata.TryGetValue("Link", out var link))
                    Link = link;
                if (nftData.NFTMetadata.TryGetValue("Image", out var imagelink))
                    ImageLink = imagelink;
                if (nftData.NFTMetadata.TryGetValue("Age", out var age))
                    Age = Convert.ToInt32(age);
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
                if (nftData.NFTMetadata.TryGetValue("Surname", out var surname))
                    Surname = surname;
                if (nftData.NFTMetadata.TryGetValue("Nickname", out var nickname))
                    Nickname = nickname;
                if (nftData.NFTMetadata.TryGetValue("RelationshipStatus", out var relationshipStatus))
                    RelationshipStatus = relationshipStatus;
                if (nftData.NFTMetadata.TryGetValue("Description", out var description))
                    Description = description;
                if (nftData.NFTMetadata.TryGetValue("Link", out var link))
                    Link = link;
                if (nftData.NFTMetadata.TryGetValue("Image", out var imagelink))
                    ImageLink = imagelink;
                if (nftData.NFTMetadata.TryGetValue("Age", out var age))
                    Age = Convert.ToInt32(age);
                if (nftData.NFTMetadata.TryGetValue("Type", out var type))
                    TypeText = type;

                SourceTxId = nftData.SourceTxId;
                NFTOriginTxId = nftData.NFTOriginTxId;
            }
        }
    }
}
