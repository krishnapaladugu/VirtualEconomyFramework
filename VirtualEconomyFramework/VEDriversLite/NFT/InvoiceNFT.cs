﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

namespace VEDriversLite.NFT
{
    /// <summary>
    /// Item for Invoice or Order NFTs
    /// </summary>
    public class InvoiceItem
    {
        /// <summary>
        /// Item Utxo hash
        /// </summary>
        public string ItemUtxo { get; set; } = string.Empty;
        /// <summary>
        /// Item Utxo index
        /// </summary>
        public int ItemUtxoIndex { get; set; } = 0;
        /// <summary>
        /// Item price - one line of invoice
        /// </summary>
        public double ItemPrice { get; set; } = 0.0;
        /// <summary>
        /// Count of the items - one line of invoice
        /// </summary>
        public int ItemCount { get; set; } = 0;

        /// <summary>
        /// Loaded Product NFT
        /// </summary>
        [JsonIgnore]
        public ProductNFT InvoiceProductNFT { get; set; } = new ProductNFT("");
    }
    /// <summary>
    /// Invoice NFT is based on NFT Order and NFT Product
    /// </summary>
    public class InvoiceNFT : CommonNFT
    {
        /// <summary>
        /// Create empty NFT class
        /// </summary>
        public InvoiceNFT(string utxo)
        {
            Utxo = utxo;
            Type = NFTTypes.Invoice;
            TypeText = "NFT Invoice";
        }
        /// <summary>
        /// List of the invoice items - ivoice lines
        /// </summary>
        public List<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
        /// <summary>
        /// Seller profile NFT hash
        /// </summary>
        public string SellerProfileNFT { get; set; } = string.Empty;
        /// <summary>
        /// Buyer profile NFT hahs
        /// </summary>
        public string BuyerProfileNFT { get; set; } = string.Empty;
        /// <summary>
        /// Hash of the original payment
        /// </summary>
        public string OriginalPaymentTxId { get; set; } = string.Empty;
        /// <summary>
        /// Hash of the Order NFT
        /// </summary>
        public string OrderTxId { get; set; } = string.Empty;
        /// <summary>
        /// Related file link - for example some attachenment on IPFS
        /// </summary>
        public string FileLink { get; set; } = string.Empty;
        /// <summary>
        /// Total price of the invoice
        /// </summary>
        public double TotalPrice { get; set; } = 0.0;
        /// <summary>
        /// Flag for already paid invoices
        /// </summary>

        public bool AlreadyPaid = false;
        /// <summary>
        /// Date when invoice has been created
        /// </summary>
        public DateTime ExposeDate { get; set; } = DateTime.UtcNow;
        /// <summary>
        /// Maximum pay term - how long can the buyer pay the invoice
        /// </summary>
        public int MaxCountOfDaysFromExposeToPayment { get; set; } = 30;
        /// <summary>
        /// Maximum count of days after the peyment should be paid
        /// </summary>
        public int MaxCountOfDaysAfterPaymentDate { get; set; } = 30;

        /// <summary>
        /// Fill basic parameters
        /// </summary>
        /// <param name="NFT"></param>
        /// <returns></returns>
        public override async Task Fill(INFT NFT)
        {
            await FillCommon(NFT);

            var pnft = NFT as InvoiceNFT;
            InvoiceItems = pnft.InvoiceItems;
            SellerProfileNFT = pnft.SellerProfileNFT;
            TotalPrice = pnft.TotalPrice;
            BuyerProfileNFT = pnft.BuyerProfileNFT;
            OriginalPaymentTxId = pnft.OriginalPaymentTxId;
            OrderTxId = pnft.OrderTxId;
            AlreadyPaid = pnft.AlreadyPaid;
            ExposeDate = pnft.ExposeDate;
            FileLink = pnft.FileLink;
            MaxCountOfDaysFromExposeToPayment = pnft.MaxCountOfDaysFromExposeToPayment;
            MaxCountOfDaysAfterPaymentDate = pnft.MaxCountOfDaysAfterPaymentDate;
        }
        /// <summary>
        /// Parse specific parameters
        /// </summary>
        /// <param name="metadata"></param>
        public override void ParseSpecific(IDictionary<string, string> metadata)
        {
            if (metadata.TryGetValue("Seller", out var seller))
                SellerProfileNFT = seller;
            if (metadata.TryGetValue("Buyer", out var buyer))
                BuyerProfileNFT = buyer;
            if (metadata.TryGetValue("FileLink", out var filelink))
                FileLink = filelink;
            if (metadata.TryGetValue("OrderTxId", out var ordertx))
                OrderTxId = ordertx;
            if (metadata.TryGetValue("ExposeDay", out var date))
            {
                try
                {
                    ExposeDate = DateTime.Parse(date);
                }
                catch
                {
                    Console.WriteLine("Cannot parse NFT Ticket Event Date");
                }
            }
            if (metadata.TryGetValue("Items", out var items))
            {
                if (!string.IsNullOrEmpty(items))
                {
                    try
                    {
                        var itms = JsonConvert.DeserializeObject<List<InvoiceItem>>(items);
                        if (itms != null)
                            InvoiceItems = itms;
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine($"Cannot deserialize the Invoice Items of InvoiceNFT: {Utxo}:{UtxoIndex}. " + ex.Message);
                        InvoiceItems = new List<InvoiceItem>();
                    }
                }
                else
                {
                    InvoiceItems = new List<InvoiceItem>();
                }
            }
            if (metadata.TryGetValue("TotalPrice", out var totalPrice))
                if (!string.IsNullOrEmpty(totalPrice))
                    TotalPrice = Convert.ToDouble(totalPrice, CultureInfo.InvariantCulture);
            if (metadata.TryGetValue("MaxCountOfDaysFromExposeToPayment", out var maxCountOfDaysFromExposeToPayment))
                if (!string.IsNullOrEmpty(maxCountOfDaysFromExposeToPayment))
                    TotalPrice = Convert.ToInt32(maxCountOfDaysFromExposeToPayment, CultureInfo.InvariantCulture);

            if (metadata.TryGetValue("MaxCountOfDaysAfterPaymentDate", out var maxCountOfDaysAfterPaymentDate))
                if (!string.IsNullOrEmpty(maxCountOfDaysAfterPaymentDate))
                    TotalPrice = Convert.ToDouble(MaxCountOfDaysAfterPaymentDate, CultureInfo.InvariantCulture);

            if (metadata.TryGetValue("OriginalPaymentTxId", out var optxid))
                OriginalPaymentTxId = optxid;

            if (!string.IsNullOrEmpty(OriginalPaymentTxId))
                AlreadyPaid = true;

        }
        /// <summary>
        /// Find and parse origin data
        /// </summary>
        /// <param name="lastmetadata"></param>
        /// <returns></returns>
        public override Task ParseOriginData(IDictionary<string, string> lastmetadata)
        {
            return Task.CompletedTask;
        }
        /// <summary>
        /// Get the NFT data for the NFT
        /// </summary>
        /// <param name="address">Address of the sender</param>
        /// <param name="key">Private key of the sender for encryption</param>
        /// <param name="receiver">receiver of the NFT</param>
        /// <returns></returns>
        public override async Task<IDictionary<string, string>> GetMetadata(string address = "", string key = "", string receiver = "")
        {
            // create token metadata
            var metadata = await GetCommonMetadata();
            metadata.Add("Seller", SellerProfileNFT);
            metadata.Add("Buyer", BuyerProfileNFT);
            metadata.Add("ExposeDate", ExposeDate.ToString());
            if (!string.IsNullOrEmpty(FileLink))
                metadata.Add("FileLink", FileLink);            
            if (!string.IsNullOrEmpty(OrderTxId))
                metadata.Add("OrderTxId", OrderTxId);

            if (InvoiceItems.Count > 0)
            {
                metadata.Add("Items", JsonConvert.SerializeObject(InvoiceItems));
            }
            if (!string.IsNullOrEmpty(OriginalPaymentTxId))
                metadata.Add("OriginalPaymentTxId", OriginalPaymentTxId);

            if (TotalPrice > 0.0)
                metadata.Add("TotalPrice", Convert.ToString(TotalPrice,CultureInfo.InvariantCulture));
            return metadata;
        }

        public void AddInvoiceItem(string utxo, int index, double price, int amount)
        {
            var invit = new InvoiceItem()
            {
                ItemUtxo = utxo,
                ItemUtxoIndex = index,
                ItemPrice = price,
                ItemCount = amount
            };
            InvoiceItems.Add(invit);
            TotalPrice += price * amount;
        }
    }
}
