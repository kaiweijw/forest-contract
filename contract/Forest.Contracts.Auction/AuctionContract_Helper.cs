using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Types;

namespace Forest.Contracts.Auction;

public partial class AuctionContract
{
    private void AssertAdmin()
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
    }

    private void AssertInitialized()
    {
        Assert(State.Initialized.Value, "Not initialized.");
    }

    private void ValidatePrice(Price input)
    {
        Assert(input != null && !string.IsNullOrWhiteSpace(input.Symbol) &&
               input.Amount > 0, "Invalid input price.");
    }

    private void ValidateSymbol(string symbol)
    {
        var words = symbol.Split(AuctionContractConstants.NFTSymbolSeparator);
        Assert(words[0].Length > 0, "Invalid input symbol.");
        Assert(words.Length == 2 && words[1] != AuctionContractConstants.CollectionSymbolSuffix, "Only support NFT.");

        var tokenInfo = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
        {
            Symbol = symbol
        });
        Assert(!string.IsNullOrWhiteSpace(tokenInfo.Symbol), "Token not found.");
        Assert(tokenInfo.TotalSupply == 1, "Only support 721 type NFT.");
    }

    private Hash GenerateAuctionId(string symbol)
    {
        var counter = State.SymbolCounter[symbol];
        State.SymbolCounter[symbol] = counter.Add(1);

        return HashHelper.ConcatAndCompute(Context.OriginTransactionId,
            HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(symbol), HashHelper.ComputeFrom(counter)));
    }

    private void AssertAuctionControllerPermission()
    {
        Assert(
            State.AuctionController.Value != null && State.AuctionController.Value.Controllers.Contains(Context.Sender),
            "No auction controller permission.");
    }

    private void ValidateCreateAuctionInput(CreateAuctionInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(!string.IsNullOrWhiteSpace(input.Symbol), "Invalid input symbol.");
        ValidateSymbol(input.Symbol);
        Assert(input.ReceivingAddress == null || !input.ReceivingAddress.Value.IsNullOrEmpty(),
            "Invalid input receiving address.");
        Assert(input.AuctionType == AuctionType.English, "Invalid input auction type.");

        ValidatePrice(input.StartPrice);
        ValidateAuctionConfig(input.AuctionConfig);
    }

    private void AssertBidPriceEnough(long lastPrice, long amount, int minMarkup)
    {
        Assert(amount > lastPrice, "Bid price not high enough.");

        var diff = amount - lastPrice;

        // min markup 1000 / 10000 = 10%
        Assert(diff >= lastPrice.Mul(minMarkup).Div(AuctionContractConstants.TenThousandConstant),
            "Bid price not high enough.");
    }

    private void ValidateAuctionConfig(AuctionConfig input)
    {
        Assert(input != null, "Invalid input auction config.");
        Assert(input.Duration > 0, "Invalid input duration.");
        Assert(input.CountdownTime >= 0, "Invalid input countdown time.");
        Assert(input.MaxExtensionTime >= 0, "Invalid input max extension time.");
        Assert(input.MinMarkup >= 0, "Invalid input min markup.");
    }
}