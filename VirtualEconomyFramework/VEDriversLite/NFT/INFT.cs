﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace VEDriversLite.NFT
{
    public enum NFTTypes
    {
        Image,
        Post,
        Profile,
        Music,
        YouTube,
        Spotify
    }
    public interface INFT
    {
        string TypeText { get; set; }
        NFTTypes Type { get; set; }
        string Name { get; set; }
        string Author { get; set; }
        string Description { get; set; }
        string Link { get; set; }
        string IconLink { get; set; }
        string ImageLink { get; set; }
        string Tags { get; set; }
        string Utxo { get; set; }
        string SourceTxId { get; set; }
        string NFTOriginTxId { get; set; }

        Task ParseOriginData();
    }
}
