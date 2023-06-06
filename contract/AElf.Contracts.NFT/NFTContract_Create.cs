using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Contracts.NFT;

public partial class NFTContract : NFTContractContainer.NFTContractBase
{
    /// <summary>
    ///     The Create method can only be executed in aelf MainChain.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public override StringValue Create(CreateInput input)
    {
        Assert(Context.ChainId == ChainHelper.ConvertBase58ToChainId("AELF"),
            "NFT Collection can only be created at aelf mainchain.");
        MakeSureTokenContractAddressSet();
        MakeSureRandomNumberProviderContractAddressSet();
        var symbol = GetSymbol(input.NftType);
        var tokenExternalInfo = GetTokenExternalInfo(input);
        var creator = input.Creator ?? Context.Sender;
        var tokenCreateInput = new MultiToken.CreateInput
        {
            Symbol = symbol,
            Decimals = 0, // Fixed
            Issuer = creator,
            IsBurnable = input.IsBurnable,
            IssueChainId = input.IssueChainId,
            TokenName = input.CollectionName,
            TotalSupply = input.TotalSupply,
            ExternalInfo = tokenExternalInfo
        };
        State.TokenContract.Create.Send(tokenCreateInput);

        var minterList = input.MinterList ?? new MinterList();
        if (!minterList.Value.Contains(creator)) minterList.Value.Add(creator);
        State.MinterListMap[symbol] = minterList;

        var collectionInfo = new NFTCollectionInfo
        {
            Symbol = symbol,
            BaseUri = input.BaseUri,
            TotalSupply = tokenCreateInput.TotalSupply,
            Creator = tokenCreateInput.Issuer,
            Metadata = new Metadata { Value = { tokenExternalInfo.Value } },
            CollectionName = tokenCreateInput.TokenName,
            IsTokenIdReuse = input.IsTokenIdReuse,
            IssueChainId = tokenCreateInput.IssueChainId,
            IsBurnable = tokenCreateInput.IsBurnable,
            NftType = input.NftType
        };
        State.NftCollectionMap[symbol] = collectionInfo;

        Context.Fire(new NFTCollectionCreated
        {
            Symbol = tokenCreateInput.Symbol,
            Creator = tokenCreateInput.Issuer,
            IsBurnable = tokenCreateInput.IsBurnable,
            IssueChainId = tokenCreateInput.IssueChainId,
            CollectionName = tokenCreateInput.TokenName,
            TotalSupply = tokenCreateInput.TotalSupply,
            Metadata = collectionInfo.Metadata,
            BaseUri = collectionInfo.BaseUri,
            IsTokenIdReuse = collectionInfo.IsTokenIdReuse,
            NftType = collectionInfo.NftType
        });

        return new StringValue
        {
            Value = symbol
        };
    }

    public override Empty CrossChainCreate(CrossChainCreateInput input)
    {
        MakeSureTokenContractAddressSet();
        InitialNFTTypeNameMap();
        Assert(State.NftCollectionMap[input.Symbol] == null, $"Collection {input.Symbol} already created.");
        var tokenInfo = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
        {
            Symbol = input.Symbol
        });
        if (string.IsNullOrEmpty(tokenInfo.Symbol))
            throw new AssertionException($"Token info {input.Symbol} not exists.");

        var baseUri = tokenInfo.ExternalInfo.Value[NftBaseUriMetadataKey];
        var isTokenIdReuse = bool.Parse(tokenInfo.ExternalInfo.Value[NftTokenIdReuseMetadataKey]);
        var nftTypeShortName = input.Symbol.Substring(0, 2);
        var nftTypeFullName = State.NFTTypeFullNameMap[nftTypeShortName];
        if (nftTypeFullName == null)
            throw new AssertionException(
                $"Full name of {nftTypeShortName} not found. Use AddNFTType to add this new pair.");

        var nftCollectionInfo = new NFTCollectionInfo
        {
            Symbol = input.Symbol,
            TotalSupply = tokenInfo.TotalSupply,
            BaseUri = baseUri,
            Creator = tokenInfo.Issuer,
            IsBurnable = tokenInfo.IsBurnable,
            IssueChainId = tokenInfo.IssueChainId,
            IsTokenIdReuse = isTokenIdReuse,
            Metadata = new Metadata { Value = { tokenInfo.ExternalInfo.Value } },
            CollectionName = tokenInfo.TokenName,
            NftType = nftTypeFullName
        };
        State.NftCollectionMap[input.Symbol] = nftCollectionInfo;

        State.MinterListMap[input.Symbol] = new MinterList
        {
            Value = { nftCollectionInfo.Creator }
        };

        Context.Fire(new NFTCollectionCreated
        {
            Symbol = input.Symbol,
            Creator = nftCollectionInfo.Creator,
            IsBurnable = nftCollectionInfo.IsBurnable,
            IssueChainId = nftCollectionInfo.IssueChainId,
            CollectionName = nftCollectionInfo.CollectionName,
            TotalSupply = nftCollectionInfo.TotalSupply,
            Metadata = nftCollectionInfo.Metadata,
            BaseUri = nftCollectionInfo.BaseUri,
            IsTokenIdReuse = isTokenIdReuse,
            NftType = nftCollectionInfo.NftType
        });
        return new Empty();
    }

    public override Empty AddNFTType(AddNFTTypeInput input)
    {
        AssertSenderIsParliamentDefaultAddress();
        InitialNFTTypeNameMap();
        var fullName = input.FullName;
        Assert(input.ShortName.Length == 2, "Incorrect short name.");
        Assert(State.NFTTypeFullNameMap[input.ShortName] == null, $"Short name {input.ShortName} already exists.");
        Assert(State.NFTTypeShortNameMap[fullName] == null, $"Full name {fullName} already exists.");
        State.NFTTypeFullNameMap[input.ShortName] = fullName;
        State.NFTTypeShortNameMap[fullName] = input.ShortName;
        var nftTypes = State.NFTTypes.Value;
        nftTypes.Value.Add(input.ShortName, fullName);
        State.NFTTypes.Value = nftTypes;
        Context.Fire(new NFTTypeAdded
        {
            ShortName = input.ShortName,
            FullName = input.FullName
        });
        return new Empty();
    }

    public override Empty RemoveNFTType(StringValue input)
    {
        AssertSenderIsParliamentDefaultAddress();
        InitialNFTTypeNameMap();
        Assert(input.Value.Length == 2, "Incorrect short name.");
        Assert(State.NFTTypeFullNameMap[input.Value] != null, $"Short name {input.Value} does not exist.");
        var fullName = State.NFTTypeFullNameMap[input.Value];
        State.NFTTypeFullNameMap.Remove(input.Value);
        State.NFTTypeShortNameMap.Remove(fullName);
        var nftTypes = State.NFTTypes.Value;
        nftTypes.Value.Remove(input.Value);
        State.NFTTypes.Value = nftTypes;
        Context.Fire(new NFTTypeRemoved
        {
            ShortName = input.Value
        });
        return new Empty();
    }

    private void AssertSenderIsParliamentDefaultAddress()
    {
        if (State.ParliamentContract.Value == null)
            State.ParliamentContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.ParliamentContractSystemName);

        if (State.ParliamentDefaultAddress.Value == null)
            State.ParliamentDefaultAddress.Value =
                State.ParliamentContract.GetDefaultOrganizationAddress.Call(new Empty());

        Assert(Context.Sender == State.ParliamentDefaultAddress.Value, "No permission.");
    }

    private ExternalInfo GetTokenExternalInfo(CreateInput input)
    {
        if (input.Metadata != null) AssertMetadataKeysAreCorrect(input.Metadata.Value.Keys);

        var tokenExternalInfo = input.Metadata == null
            ? new ExternalInfo()
            : new ExternalInfo
            {
                Value = { input.Metadata.Value }
            };

        // Add NFT Type to external info.
        tokenExternalInfo.Value[NftTypeMetadataKey] = input.NftType;
        // Add Uri to external info.
        tokenExternalInfo.Value[NftBaseUriMetadataKey] = input.BaseUri;
        tokenExternalInfo.Value[NftTokenIdReuseMetadataKey] = input.IsTokenIdReuse.ToString();
        return tokenExternalInfo;
    }
}