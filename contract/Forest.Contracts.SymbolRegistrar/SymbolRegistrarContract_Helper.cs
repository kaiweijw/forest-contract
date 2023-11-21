using System.Linq;
using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using Google.Protobuf.WellKnownTypes;

namespace Forest.Contracts.SymbolRegistrar
{
    /// <summary>
    /// The C# implementation of the contract defined in symbol_registrar_contract.proto that is located in the "protobuf"
    /// folder.
    /// Notice that it inherits from the protobuf generated code. 
    /// </summary>
    public partial class SymbolRegistrarContract : SymbolRegistrarContractContainer.SymbolRegistrarContractBase
    {
        private void AssertContractInitialize()
        {
            Assert(State.Initialized.Value, "Contract not Initialized.");
        }

        private void AssertContractAuthor()
        {
            // Initialize by author only
            State.GenesisContract.Value = Context.GetZeroSmartContractAddress();
            var author = State.GenesisContract.GetContractAuthor.Call(Context.Self);
            Assert(author == Context.Sender, "No permission");
        }

        private void AssertAdmin()
        {
            AssertContractInitialize();
            Assert(State.Admin.Value == Context.Sender, "No permission.");
        }
        
        
        private void AssertAssociateOrganization()
        {
            AssertContractInitialize();
            Assert(State.AssociateOrganization.Value == Context.Sender, "No Associate permission.");
        }

        private void AssertSymbolPattern(string symbol)
        {
            Assert(symbol.Length > 0 && symbol.Length <= SymbolRegistrarContractConstants.MaxSymbolLength,
                "Invalid symbol length.");

            var symbolPartition = symbol.Split(SymbolRegistrarContractConstants.NFTSymbolSeparator);
            Assert(symbolPartition.Length == 1 || symbolPartition.Length == 2, "Invalid symbol.");
            foreach (var c in symbolPartition[0])
            {
                Assert(c >= 'A' && c <= 'Z', "Invalid symbol.");
            }
            if (symbolPartition.Length == 2)
            {
                Assert( symbolPartition[1] == SymbolRegistrarContractConstants.CollectionSymbolSuffix, "Invalid NFT Symbol");
            }
        }
        
        private void AssertPriceListLength(PriceList priceList)
        {
            Assert(priceList?.Value?.Count == SymbolRegistrarContractConstants.MaxSymbolLength,
                "price list length must be " + SymbolRegistrarContractConstants.MaxSymbolLength);
        }

        private void AssertUniquePriceListLength(PriceList priceList)
        {
            Assert(priceList?.Value?.Count <= SymbolRegistrarContractConstants.MaxSymbolLength,
                "price list length must be less or equal " + SymbolRegistrarContractConstants.MaxSymbolLength);

        }
        private void AssertPriceList(PriceList priceList)
        {
            var tracker = new int[SymbolRegistrarContractConstants.MaxSymbolLength];
            foreach (var priceItem in priceList.Value)
            {
                Assert(
                    priceItem.SymbolLength >= 1 &&
                    priceItem.SymbolLength <= SymbolRegistrarContractConstants.MaxSymbolLength,
                    "Invalid symbolLength: " + priceItem.SymbolLength);
                Assert(tracker[priceItem.SymbolLength - 1] == 0, "Duplicate symbolLength: " + priceItem.SymbolLength);
                Assert(priceItem.Amount >= 0, "Invalid amount");
                tracker[priceItem.SymbolLength - 1] = 1;
            }
        }

        private PriceItem GetDealPrice(string symbol)
        {
            var isNFT = symbol.Contains(SymbolRegistrarContractConstants.NFTSymbolSeparator);
            return isNFT ? State.NFTPrice[symbol.Length] : State.FTPrice[symbol.Length];
        }


        private AuthorityInfo GetDefaultParliamentController()
        {
            if (State.ParliamentContract.Value == null)
            {
                var parliamentContractAddress =
                    Context.GetContractAddressByName(SmartContractConstants.ParliamentContractSystemName);
                if (parliamentContractAddress == null)
                    // Test environment.
                    return new AuthorityInfo();

                State.ParliamentContract.Value = parliamentContractAddress;
            }

            var defaultOrganizationAddress = State.ParliamentContract.GetDefaultOrganizationAddress.Call(new Empty());
            return new AuthorityInfo
            {
                ContractAddress = State.ParliamentContract.Value,
                OwnerAddress = defaultOrganizationAddress
            };
        }

        private TokenInfo GetTokenInfo(string symbol)
        {
            return State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
            {
                Symbol = symbol
            });
        }

        private void InitializeAuctionConfig()
        {
            State.AuctionConfig.Value = new AuctionConfig
            {
                Duration = SymbolRegistrarContractConstants.DefaultDuration,
                CountdownTime = SymbolRegistrarContractConstants.DefaultCountdownTime,
                MaxExtensionTime = SymbolRegistrarContractConstants.DefaultMaxExtensionTime,
                MinMarkup = SymbolRegistrarContractConstants.DefaultMinMarkup
            };
        }

        private void AssertSaleController()
        {
            Assert(State.SaleController.Value != null && State.SaleController.Value.Controllers.Contains(Context.Sender),
                "No sale controller permission.");
        }

        private void AssertInitialized()
        {
            Assert(State.Initialized.Value, "Contract not initialized.");
        }

        private void CheckSymbolExisted(string symbol)
        {
            var symbols = symbol.Split(SymbolRegistrarContractConstants.NFTSymbolSeparator);
            var tokenSymbol = symbols.First();
            var tokenInfo = GetTokenInfo(tokenSymbol);
            Assert(tokenInfo.Symbol.Length < 1, "Symbol exists");
            var tokenSeed = State.SymbolSeedMap[tokenSymbol];
            Assert(string.IsNullOrWhiteSpace(tokenSeed) || State.SeedInfoMap[tokenSeed].ExpireTime < Context.CurrentBlockTime.Seconds, "symbol seed existed");
            var collectionSymbol = symbols.First() + SymbolRegistrarContractConstants.NFTSymbolSeparator +
                                   SymbolRegistrarContractConstants.CollectionSymbolSuffix;
            var collectionInfo = GetTokenInfo(collectionSymbol);
            Assert(collectionInfo.Symbol.Length < 1, "Symbol exists");
            var collectionSeed = State.SymbolSeedMap[collectionSymbol];
            Assert(string.IsNullOrWhiteSpace(collectionSeed) || State.SeedInfoMap[collectionSeed].ExpireTime < Context.CurrentBlockTime.Seconds, "symbol seed existed");
        }
    }
}