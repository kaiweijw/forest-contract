using System.Linq;
using AElf;
using AElf.CSharp.Core;
using AElf.Types;

namespace Forest.Contracts.Auction;

public partial class AuctionContract
{
    private void AssertAdmin()
    {
        AssertInitialize();
        Assert(Context.Sender == State.Admin.Value, "No permission.");
    }

    private void AssertInitialize()
    {
        Assert(State.Initialized.Value, "Not initialized.");
    }
    
    private void AssertInputPrice(Price input)
    {
        Assert(input != null && !string.IsNullOrWhiteSpace(input.Symbol) &&
               input.Amount > 0, "Invalid input price.");
    }

    private void AssertInputSymbol(string symbol)
    {
        var words = symbol.Split(AuctionContractConstants.NFTSymbolSeparator);
        Assert(words[0].Length > 0 && words[0].All(IsValidCreateSymbolChar), "Invalid input symbol.");
        Assert(words.Length == 2 && words[1].Length > 0 && words[1].All(IsValidItemIdChar) && words[1] != AuctionContractConstants.NFTSymbolSuffix, "Only support NFT.");
    }
    
    private bool IsValidItemIdChar(char character)
    {
        return character >= '0' && character <= '9';
    }

    private bool IsValidCreateSymbolChar(char character)
    {
        return character >= 'A' && character <= 'Z';
    }

    private Hash GenerateAuctionId(string symbol)
    {
        var counter = State.SymbolCounterMap[symbol];
        State.SymbolCounterMap[symbol] = counter.Add(1);

        return HashHelper.ConcatAndCompute(Context.OriginTransactionId,
            HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(symbol), HashHelper.ComputeFrom(counter)));
    }
}