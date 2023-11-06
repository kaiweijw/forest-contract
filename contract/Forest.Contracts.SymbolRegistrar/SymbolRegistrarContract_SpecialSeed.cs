using System.Collections.Generic;
using System.Linq;
using AElf.Sdk.CSharp;
using Google.Protobuf.Collections;
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
        
        public override Empty AddSpecialSeeds(SpecialSeedList input)
        {
            if (State.Initialized.Value)
                AssertSaleController();
            else
                AssertContractAuthor();
            Assert(input.Value.Count <= SymbolRegistrarContractConstants.MaxAddSpecialSeedCount, "Seed list max limit exceeded.");

            var priceSymbolExists = new HashSet<string> { SymbolRegistrarContractConstants.ELFSymbol };
            var exists = new HashSet<string>();
            for (var index = 0; index < input?.Value?.Count; index++)
            {
                var item = input.Value[index];
                Assert(item.PriceAmount > 0, "Invalid price amount");
                AssertSymbolPattern(item.Symbol);
                if (!priceSymbolExists.Contains(item.PriceSymbol))
                {
                    var tokenInfo = GetTokenInfo(item.PriceSymbol);
                    Assert(tokenInfo?.Symbol.Length > 0, "Price token " + item.PriceSymbol + " not exists");
                    priceSymbolExists.Add(item.PriceSymbol);
                }

                if (item.SeedType == SeedType.Notable)
                {
                    var issueChainList = State.IssueChainList?.Value?.IssueChain ?? new RepeatedField<string>();
                    Assert(item.IssueChain?.Length > 0, "Invalid issue chain of symbol " + item.Symbol);
                    Assert(issueChainList.Contains(item.IssueChain), "Issue chain of symbol " + item.Symbol + " not exists");
                    Assert(item.IssueChainContractAddress?.Length > 0,
                        "Invalid issue chain contract of symbol " + item.Symbol);
                }

                Assert(!exists.Contains(item.Symbol), "Duplicate symbol " + item.Symbol);
                exists.Add(item.Symbol);
                State.SpecialSeedMap[item.Symbol] = item;
            }

            Context.Fire(new SpecialSeedAdded
            {
                AddList = input,
            });
            return new Empty();
        }

        public override Empty RemoveSpecialSeeds(RemoveSpecialSeedInput input)
        {
            
            if (State.Initialized.Value)
                Assert(GetDefaultParliamentController().OwnerAddress == Context.Sender, "No permission.");
            else 
                AssertContractAuthor();
            
            var removedList = new SpecialSeedList();
            foreach (var symbol in input.Symbols)
            {
                var itemData = State.SpecialSeedMap[symbol];
                if (itemData == null) continue;
                removedList.Value.Add(itemData);
                State.SpecialSeedMap.Remove(symbol);
            }

            if (removedList.Value.Count > 0)
            {
                Context.Fire(new SpecialSeedRemoved
                {
                    RemoveList = removedList
                });
            }

            return new Empty();
        }

        public override Empty AddUniqueSeeds(UniqueSeedList input)
        {
            if (State.Initialized.Value)
                Assert(GetDefaultParliamentController().OwnerAddress == Context.Sender, "No permission.");
            else
                AssertContractAuthor();
            Assert(input.Symbols.Count <= SymbolRegistrarContractConstants.MaxAddSpecialSeedCount, "Seed list max limit exceeded.");

            var symbols = input.Symbols.Distinct().ToList();
            var specialSeedList = new SpecialSeedList();
            foreach (var symbol in symbols)
            {
                AssertSymbolPattern(symbol);
                var symbolPartition = symbol.Split(SymbolRegistrarContractConstants.NFTSymbolSeparator);
                var ftSeed = State.SpecialSeedMap[symbolPartition[0]];
                if (ftSeed == null)
                {
                    ftSeed = new SpecialSeed
                    {
                        Symbol = symbolPartition[0],
                        SeedType = SeedType.Unique
                    };
                    State.SpecialSeedMap[symbolPartition[0]] = ftSeed;
                    specialSeedList.Value.Add(ftSeed);
                }

                var nftSymbol = symbolPartition[0] + SymbolRegistrarContractConstants.NFTSymbolSeparator +
                                SymbolRegistrarContractConstants.CollectionSymbolSuffix;
                var nftSeed = State.SpecialSeedMap[nftSymbol];
                if (nftSeed == null)
                {
                    nftSeed = new SpecialSeed
                    {
                        Symbol = nftSymbol,
                        SeedType = SeedType.Unique
                    };
                    State.SpecialSeedMap[nftSymbol] = nftSeed;
                    specialSeedList.Value.Add(nftSeed);
                }
            }

            Context.Fire(new SpecialSeedAdded
            {
                AddList = specialSeedList,
            });
            return new Empty();
        }
    }
}