using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Forest;

public partial class ForestContract
{
    public override ListedNFTInfoList GetListedNFTInfoList(GetListedNFTInfoListInput input)
    {
        return State.ListedNFTInfoListMap[input.Symbol][input.Owner];
    }

    public override GetWhitelistIdOutput GetWhitelistId(GetWhitelistIdInput input)
    {
        var projectId = CalculateProjectId(input.Symbol, input.Owner);
        Assert(State.WhitelistIdMap[projectId] != null, $"Whitelist id not found.Project id:{projectId}");
        var whitelistId = State.WhitelistIdMap[projectId];
        return new GetWhitelistIdOutput
        {
            WhitelistId = whitelistId,
            ProjectId = projectId
        };
    }

    public override AddressList GetOfferAddressList(GetAddressListInput input)
    {
        return State.OfferAddressListMap[input.Symbol];
    }

    public override OfferList GetOfferList(GetOfferListInput input)
    {
        if (input.Address != null)
        {
            return State.OfferListMap[input.Symbol][input.Address];
        }

        var addressList = GetOfferAddressList(new GetAddressListInput
        {
            Symbol = input.Symbol,
        }) ?? new AddressList();
        var allOfferList = new OfferList();
        foreach (var address in addressList.Value)
        {
            var offerList = State.OfferListMap[input.Symbol][address];
            if (offerList != null)
            {
                allOfferList.Value.Add(offerList.Value);
            }
        }

        return allOfferList;
    }

    public override StringList GetTokenWhiteList(StringValue input)
    {
        return GetTokenWhiteList(input.Value);
    }

    public override StringList GetGlobalTokenWhiteList(Empty input)
    {
        return State.GlobalTokenWhiteList.Value;
    }

    public override RoyaltyInfo GetRoyalty(GetRoyaltyInput input)
    {
        var royaltyInfo = new RoyaltyInfo
        {
            Royalty = State.RoyaltyMap[input.Symbol]
        };

        var certainNftRoyalty = State.CertainNFTRoyaltyMap[input.Symbol] ?? new CertainNFTRoyaltyInfo();
        if (certainNftRoyalty.IsManuallySet)
        {
            royaltyInfo.Royalty = certainNftRoyalty.Royalty;
        }

        if (State.RoyaltyFeeReceiverMap[input.Symbol] != null)
        {
            royaltyInfo.RoyaltyFeeReceiver = State.RoyaltyFeeReceiverMap[input.Symbol];
        }

        return royaltyInfo;
    }

    public override ServiceFeeInfo GetServiceFeeInfo(Empty input)
    {
        return new ServiceFeeInfo
        {
            ServiceFeeRate = State.ServiceFeeRate.Value,
            ServiceFeeReceiver = State.ServiceFeeReceiver.Value
        };
    }

    public override Address GetAdministrator(Empty input)
    {
        return State.Admin.Value;
    }

    public override BizConfig GetBizConfig(Empty input)
    {
        return State.BizConfig.Value;
    }
    
    public override GetTotalOfferAmountOutput GetTotalOfferAmount(GetTotalOfferAmountInput input)
    {
        var totalAmount = GetOfferTotalAmount(input.Address, input.PriceSymbol);
        var allowance = GetAllowance(input.Address, input.PriceSymbol);
        var getTotalOfferAmountOutput = new GetTotalOfferAmountOutput()
        {
            Symbol = input.PriceSymbol,
            Allowance = allowance,
            TotalAmount = totalAmount
        };
        return getTotalOfferAmountOutput;
    }
    
    public override GetTotalEffectiveListedNFTAmountOutput GetTotalEffectiveListedNFTAmount(GetTotalEffectiveListedNFTAmountInput input)
    {
        var totalAmount = GetEffectiveListedNFTTotalAmount(input.Address, input.Symbol);
        var allowance = GetAllowance(input.Address, input.Symbol);
        var getTotalEffectiveListedNftAmountOutput = new GetTotalEffectiveListedNFTAmountOutput()
        {
            Symbol = input.Symbol,
            Allowance = allowance,
            TotalAmount = totalAmount
        };
        
        return getTotalEffectiveListedNftAmountOutput;
    }
    public override AIServiceFeeInfo GetAIServiceFee(Empty input)
    {
        return new AIServiceFeeInfo
        {
            Price = State.AIServiceFeeConfig.Value,
            ServiceFeeReceiver = State.AIServiceFeeReceiver.Value
        };
    }
    
    public override StringList GetAIImageSizes(Empty input)
    {
        return State.AIImageSizeList?.Value;
    }

        
    public override CreateArtInfo GetCreateArtInfo(GetCreateArtInfoInput input)
    {
        Assert(input != null, "Invalid TransactionId");
        return State.CreateArtInfoMap[input.Address ?? Context.Sender][input.TransactionId];
    }
}