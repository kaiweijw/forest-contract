using System.Linq;
using AElf.Contracts.MultiToken;
using AElf.Types;

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
    
    private void AssertDropDetailList(DropDetailList dropDetailList, string collection, Hash dropId, out long totalAmount)
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
            Assert(State.DropSymbolMap[dropId][detail.Symbol] == 0, $"symbol:{detail.Symbol} is already exist in index{State.DropSymbolMap[dropId][detail.Symbol]}.");
            var balance = State.TokenContract.GetBalance.Call(new GetBalanceInput
            {
                Symbol = detail.Symbol,
                Owner = Context.Self
            });
            Assert(balance.Balance >= detail.TotalAmount, $"Insufficient balance. {detail.Symbol}: {balance},{detail.TotalAmount}");
            totalAmount += detail.TotalAmount;
            State.DropSymbolMap[dropId][detail.Symbol] = 1;
        }
    }

    private void AssertCollectionContainsNft(string collection, string nft)
    {
        var collectionPrefix = collection.Split(DropContractConstants.NFTSymbolSeparator)[0];
        var nftPrefix = nft.Split(DropContractConstants.NFTSymbolSeparator)[0];
        Assert(collectionPrefix == nftPrefix, $"Invalid nft. collection:{collection},nft:{nft}");
    }
    
    private void AssertSymbolExist(string symbol, SymbolType targetType, bool checkOwner = false)
    {
        Assert(symbol != null && !string.IsNullOrWhiteSpace(symbol), $"Invalid symbol.{symbol}");
        var actualType = GetCreateInputSymbolType(symbol);
        Assert(actualType == targetType, $"Invalid symbol type.targetType:{targetType},actualType:{symbol}");
        var symbolInfo = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
        {
            Symbol = symbol
        });
        Assert(symbolInfo != null && !string.IsNullOrWhiteSpace(symbolInfo.Symbol), $"Not exist symbol. {targetType} - {symbol}");

        if (!checkOwner) return;
        //check real address
        if (symbolInfo.Owner == Context.Sender) return;
        //check proxy virtual address
        Assert(IsProxyManager(symbolInfo.Owner), "Not token owner.");
    }

    private bool IsProxyManager(Address owner)
    {
        var proxyAccount = State.ProxyAccountContract.GetProxyAccountByProxyAccountAddress.Call(owner);
        if (proxyAccount?.ManagementAddresses == null) return false;
        foreach (var managementAddress in proxyAccount.ManagementAddresses)
        {
            if (managementAddress.Address == Context.Sender) return true;
        }

        return false;
    }

    private SymbolType GetCreateInputSymbolType(string symbol)
    {
        var words = symbol.Split(DropContractConstants.NFTSymbolSeparator);
        Assert(words[0].Length > 0 && words[0].All(IsValidCreateSymbolChar), "Invalid Symbol input");
        if (words.Length == 1) return SymbolType.Token;
        Assert(words.Length == 2 && words[1].Length > 0 && words[1].All(IsValidItemIdChar), "Invalid NFT Symbol input");
        return words[1] == DropContractConstants.CollectionSymbolSuffix ? SymbolType.NftCollection : SymbolType.Nft;
    }
    
    private static bool IsValidCreateSymbolChar(char character)
    {
        return character >= 'A' && character <= 'Z';
    }
    
    private static bool IsValidItemIdChar(char character)
    {
        return character >= '0' && character <= '9';
    }

}