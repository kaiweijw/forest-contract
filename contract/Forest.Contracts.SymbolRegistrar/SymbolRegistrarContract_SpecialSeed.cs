using System.Collections.Generic;
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
        

        public override Empty AddSpecialSeeds(SpecialSeedList input)
        {
            if (State.Initialized.Value)
                Assert(State.Admin.Value == Context.Sender // TODO support admin for test temp
                       || GetDefaultParliamentController().OwnerAddress == Context.Sender, $"No permission.({GetDefaultParliamentController().OwnerAddress},{Context.Sender})");
            else
                AssertContractAuthor();

            var priceSymbolExists = new HashSet<string> { SymbolRegistrarContractConstants.ELFSymbol };
            var exists = new HashSet<string>();
            for (var index = 0; index < input?.Value?.Count; index++)
            {
                var item = input.Value[index];
                if (!priceSymbolExists.Contains(item.PriceSymbol))
                {
                    var tokenInfo = GetTokenInfo(item.PriceSymbol);
                    Assert(tokenInfo?.Symbol.Length > 0, "Price token " + item.PriceSymbol + " not exists");
                    priceSymbolExists.Add(item.PriceSymbol);
                }

                if (item.SeedType == SeedType.Notable)
                {
                    Assert(item.IssueChain?.Length > 0, "Invalid issue chain of symbol " + item.Symbol);
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
            Assert(GetDefaultParliamentController().OwnerAddress == Context.Sender, "No permission.");

            var exists = new HashSet<string>();
            var removedList = new SpecialSeedList();
            for (var index = 0; index < input?.Symbols.Count; index++)
            {
                var symbol = input.Symbols[index];
                Assert(!exists.Contains(symbol), "Duplicate symbol " + symbol);
                exists.Add(symbol);

                var itemData = State.SpecialSeedMap[symbol];
                if (itemData == null) continue;
                removedList.Value.Add(itemData);
                State.SpecialSeedMap.Remove(symbol);
            }

            Context.Fire(new SpecialSeedRemoved
            {
                RemoveList = removedList
            });
            return new Empty();
        }
    }
}