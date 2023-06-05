using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using Google.Protobuf.WellKnownTypes;

namespace Forest;

public partial class ForestContract
{
    public override Empty SetRoyalty(SetRoyaltyInput input)
    {
        AssertContractInitialized();

        // 0% - 10%
        Assert(0 <= input.Royalty && input.Royalty <= 1000, "Royalty should be between 0% to 10%.");
        var nftProtocolInfos = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
        {
            Symbol = input.Symbol
        });
        State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
        {
            Symbol = input.Symbol
        });
        
        var nftProtocolInfo = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
        {
            Symbol = input.Symbol,
        });
        Assert(!string.IsNullOrEmpty(nftProtocolInfo.Symbol), "NFT Protocol not found.");
        
        var nftInfo = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
        {
            Symbol = input.Symbol,
        });

        Assert(nftProtocolInfo.Issuer == Context.Sender || nftInfo.Issuer==Context.Sender,
            "No permission.");
        State.CertainNFTRoyaltyMap[input.Symbol] = new CertainNFTRoyaltyInfo
        {
            IsManuallySet = true,
            Royalty = input.Royalty
        };

        State.RoyaltyFeeReceiverMap[input.Symbol] = input.RoyaltyFeeReceiver;
        return new Empty();
    }

    public override Empty SetTokenWhiteList(SetTokenWhiteListInput input)
    {
        AssertContractInitialized();
        var nftProtocolInfo = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
        {
            Symbol = input.Symbol
        });

        // var nftProtocolInfo = State.NFTContract.GetNFTProtocolInfo.Call(new StringValue {Value = input.Symbol});
        Assert(nftProtocolInfo.Issuer != null, "NFT Protocol not found.");
        Assert(nftProtocolInfo.Issuer == Context.Sender, "Only NFT Protocol Creator can set token white list.");
        State.TokenWhiteListMap[input.Symbol] = input.TokenWhiteList;
        Context.Fire(new TokenWhiteListChanged
        {
            Symbol = input.Symbol,
            TokenWhiteList = input.TokenWhiteList
        });
        return new Empty();
    }
}