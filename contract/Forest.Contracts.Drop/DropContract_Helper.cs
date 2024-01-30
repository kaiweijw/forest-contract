using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Types;
using Google.Protobuf.Collections;

namespace Forest.Contracts.Drop;

public partial class DropContract
{
    private void AssertAdmin()
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
    }

    private void AssertInitialized()
    {
        Assert(State.Initialized.Value, "Not initialized.");
    }
    
    private void AssertDropDetailList(DropDetailList dropDetailList, string collection, Hash dropId, int index, out long totalAmount)
    {
        Assert(dropDetailList != null, "Invalid detail list.");
        Assert(dropDetailList.Value.Count > 0 && dropDetailList.Value.Count <= State.MaxDropDetailListCount.Value, $"Invalid detail list.count:{dropDetailList.Value.Count}");
        totalAmount = 0L;
        foreach(var detail in dropDetailList.Value.Distinct())
        {
            detail.ClaimAmount = 0;
            Assert(detail.Symbol != null && !string.IsNullOrWhiteSpace(detail.Symbol), $"Invalid symbol data.token:{detail.Symbol}");
            Assert(detail.TotalAmount > 0, "Invalid amount.");
            AssertSymbolExist(detail.Symbol, SymbolType.Nft);
            AssertCollectionContainsNft(collection, detail.Symbol);
            Assert(State.DropSymbolMap[dropId][detail.Symbol] == null || State.DropSymbolMap[dropId][detail.Symbol] == 0, $"symbol:{detail.Symbol} is already exist in index{State.DropSymbolMap[dropId][detail.Symbol]}.");
            var balance = State.TokenContract.GetBalance.Call(new GetBalanceInput
            {
                Symbol = detail.Symbol,
                Owner = Context.Self
            });
            Assert(balance.Balance >= detail.TotalAmount, $"Insufficient balance. {detail.Symbol}: {balance},{detail.TotalAmount}");
            totalAmount += detail.TotalAmount;
            State.DropSymbolMap[dropId][detail.Symbol] = index;
        }
        totalAmount = 0L;
    }

    private void AssertCollectionContainsNft(string collection, string nft)
    {
        var collectionPrefix = collection.Split(TokenContractConstants.NFTSymbolSeparator)[0];
        var nftPrefix = nft.Split(TokenContractConstants.NFTSymbolSeparator)[0];
        Assert(collectionPrefix == nftPrefix, $"Invalid nft. collection:{collection},nft:{nft}");
    }
    
    private void AssertSymbolExist(string symbol, SymbolType targetType)
    {
        Assert(symbol != null && !string.IsNullOrWhiteSpace(symbol), $"Invalid symbol.{symbol}");
        var actualType = GetCreateInputSymbolType(symbol);
        Assert(actualType == targetType, $"Invalid symbol type.targetType:{targetType},actualType:{symbol}");
        var symbolInfo = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
        {
            Symbol = symbol
        });
        Assert(symbolInfo != null && !string.IsNullOrWhiteSpace(symbolInfo.Symbol), $"Not exist symbol. {targetType} - {symbol}");
    }

    private SymbolType GetCreateInputSymbolType(string symbol)
    {
        var words = symbol.Split(TokenContractConstants.NFTSymbolSeparator);
        Assert(words[0].Length > 0 && words[0].All(IsValidCreateSymbolChar), "Invalid Symbol input");
        if (words.Length == 1) return SymbolType.Token;
        Assert(words.Length == 2 && words[1].Length > 0 && words[1].All(IsValidItemIdChar), "Invalid NFT Symbol input");
        return words[1] == TokenContractConstants.CollectionSymbolSuffix ? SymbolType.NftCollection : SymbolType.Nft;
    }
    
    private bool IsValidCreateSymbolChar(char character)
    {
        return character >= 'A' && character <= 'Z';
    }
    
    private bool IsValidItemIdChar(char character)
    {
        return character >= '0' && character <= '9';
    }

}