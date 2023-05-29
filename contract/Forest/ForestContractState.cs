using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace Forest
{
    /// <summary>
    /// The state class of the contract, it inherits from the AElf.Sdk.CSharp.State.ContractState type. 
    /// </summary>
    public partial class ForestContractState : ContractState
    {
        public SingletonState<Address> Admin { get; set; }

        public SingletonState<Address> ServiceFeeReceiver { get; set; }

        public Int32State ServiceFeeRate { get; set; }
        public Int64State ServiceFee { get; set; }

        public SingletonState<StringList> GlobalTokenWhiteList { get; set; }

        /// <summary>
        /// Symbol -> Token Id -> Owner -> List NFT Info List
        /// </summary>
        public MappedState<string, Address, ListedNFTInfoList> ListedNFTInfoListMap { get; set; }

        /// <summary>
        /// Project Id -> Whitelist Id
        /// </summary>
        public MappedState<Hash, Hash> WhitelistIdMap { get; set; }

        /// <summary>
        /// Symbol -> Token Id -> Offer Address List
        /// </summary>
        public MappedState<string, AddressList> OfferAddressListMap { get; set; }

        /// <summary>
        /// Symbol -> Token Id -> Offer Maker -> Offer List
        /// </summary>
        public MappedState<string, Address, OfferList> OfferListMap { get; set; }

        public MappedState<string, Address, Bid> BidMap { get; set; }

        public MappedState<string, AddressList> BidAddressListMap { get; set; }

        /// <summary>
        /// Symbol -> Token Id -> Royalty
        /// </summary>
        public MappedState<string, int> RoyaltyMap { get; set; }

        public MappedState<string, Address> RoyaltyFeeReceiverMap { get; set; }
        public MappedState<string, long, CertainNFTRoyaltyInfo> CertainNFTRoyaltyMap { get; set; }
        public MappedState<string, StringList> TokenWhiteListMap { get; set; }

        public MappedState<string, CustomizeInfo> CustomizeInfoMap { get; set; }
        public MappedState<string, RequestInfo> RequestInfoMap { get; set; }

        public MappedState<string, EnglishAuctionInfo> EnglishAuctionInfoMap { get; set; }
        public MappedState<string, DutchAuctionInfo> DutchAuctionInfoMap { get; set; }
    }
}