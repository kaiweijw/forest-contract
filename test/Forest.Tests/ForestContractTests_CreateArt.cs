using System;
using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Forest;

public class ForestContractTests_CreateArt : ForestContractTestBase
{
    private const string NftSymbol = "TESTNFT-1";
    private const string NftSymbol2 = "TESTNFT-2";
    private const string ElfSymbol = "ELF";
    private const int ServiceFeeRate = 1000; // 10%
    private const int AIServiceFee = 10000000; 
    private const string DefaultAIImageSize1024 = "1024x1024";
    private const string DefaultAIImageSize512 = "512x512";
    private const string DefaultAIImageSize256 = "256x256";

    private async Task InitializeForestContract()
    {
        await AdminForestContractStub.Initialize.SendAsync(new InitializeInput
        {
            ServiceFeeReceiver = MarketServiceFeeReceiverAddress,
            ServiceFeeRate = ServiceFeeRate,
            WhitelistContractAddress = WhitelistContractAddress
        });

        await AdminForestContractStub.SetWhitelistContract.SendAsync(WhitelistContractAddress);
    }

    private static Price Elf(long amunt)
    {
        return new Price()
        {
            Symbol = ElfSymbol,
            Amount = amunt
        };
    }
    
    [Fact]
    //cancel offer
    public async void SetAIServiceFee_Test()
    {
        await InitializeForestContract();
        var feeConfig = await ForestContractStub.GetAIServiceFee.CallAsync(new Empty());
        feeConfig.Price.ShouldBe(null);
        feeConfig.ServiceFeeReceiver.ShouldBe(null);
        
        await ForestContractStub.SetAIServiceFee.SendAsync(new SetAIServiceFeeInput()
        {
            Price = new Price()
            {
                Symbol = ElfSymbol,
                Amount = AIServiceFee
            },
            ServiceFeeReceiver = DefaultAddress
        });
        
        feeConfig = await ForestContractStub.GetAIServiceFee.CallAsync(new Empty());
        feeConfig.ShouldNotBe(null);
        feeConfig.Price.Amount.ShouldBe(AIServiceFee);
        feeConfig.Price.Symbol.ShouldBe(ElfSymbol);
        feeConfig.ServiceFeeReceiver.ShouldBe(DefaultAddress);

    }
    
    [Fact]
    public async Task AddAIImageSize_Test()
    {
        await InitializeForestContract();
        var imageSizes = await ForestContractStub.GetAIImageSizes.CallAsync(new Empty());
        imageSizes.Value.Count.ShouldBe(0);
        
        await ForestContractStub.AddAIImageSize.SendAsync(new StringValue(){Value = DefaultAIImageSize1024});
        imageSizes = await ForestContractStub.GetAIImageSizes.CallAsync(new Empty());
        imageSizes.Value.Count.ShouldBe(1);
        imageSizes.Value.ShouldContain(DefaultAIImageSize1024);
        
        await ForestContractStub.AddAIImageSize.SendAsync(new StringValue(){Value = DefaultAIImageSize512});
        imageSizes = await ForestContractStub.GetAIImageSizes.CallAsync(new Empty());
        imageSizes.Value.Count.ShouldBe(2);
        imageSizes.Value.ShouldContain(DefaultAIImageSize1024);
        imageSizes.Value.ShouldContain(DefaultAIImageSize512);
        
        await ForestContractStub.AddAIImageSize.SendAsync(new StringValue(){Value = DefaultAIImageSize256});
        imageSizes = await ForestContractStub.GetAIImageSizes.CallAsync(new Empty());
        imageSizes.Value.Count.ShouldBe(3);
        imageSizes.Value.ShouldContain(DefaultAIImageSize1024);
        imageSizes.Value.ShouldContain(DefaultAIImageSize512);
        imageSizes.Value.ShouldContain(DefaultAIImageSize256);
        
        var result = await ForestContractStub.AddAIImageSize.SendWithExceptionAsync(new StringValue(){Value = DefaultAIImageSize1024});
        result.TransactionResult.Error.ShouldContain("input size Already exists");
        
        result = await ForestContractStub.AddAIImageSize.SendWithExceptionAsync(new StringValue());
        result.TransactionResult.Error.ShouldContain("Invalid input");
    }
    
    [Fact]
    public async void RemoveAIImageSize_Test()
    {
        await AddAIImageSize_Test();
        var imageSizes = await ForestContractStub.GetAIImageSizes.CallAsync(new Empty());
        imageSizes.Value.Count.ShouldBe(3);
        imageSizes.Value.ShouldContain(DefaultAIImageSize1024);
        imageSizes.Value.ShouldContain(DefaultAIImageSize512);
        imageSizes.Value.ShouldContain(DefaultAIImageSize256);

        var result = await ForestContractStub.RemoveAIImageSize.SendWithExceptionAsync(new StringValue(){Value = "10x10"});
        result.TransactionResult.Error.ShouldContain("input size not exists");
        
        await ForestContractStub.RemoveAIImageSize.SendAsync(new StringValue(){Value = DefaultAIImageSize1024});
        imageSizes = await ForestContractStub.GetAIImageSizes.CallAsync(new Empty());
        imageSizes.Value.Count.ShouldBe(2);
        imageSizes.Value.ShouldNotContain(DefaultAIImageSize1024);
        imageSizes.Value.ShouldContain(DefaultAIImageSize512);
        imageSizes.Value.ShouldContain(DefaultAIImageSize256);
        
        await ForestContractStub.RemoveAIImageSize.SendAsync(new StringValue(){Value = DefaultAIImageSize512});
        imageSizes = await ForestContractStub.GetAIImageSizes.CallAsync(new Empty());
        imageSizes.Value.Count.ShouldBe(1);
        imageSizes.Value.ShouldNotContain(DefaultAIImageSize1024);
        imageSizes.Value.ShouldNotContain(DefaultAIImageSize512);
        imageSizes.Value.ShouldContain(DefaultAIImageSize256);
        
        await ForestContractStub.RemoveAIImageSize.SendAsync(new StringValue(){Value = DefaultAIImageSize256});
        imageSizes = await ForestContractStub.GetAIImageSizes.CallAsync(new Empty());
        imageSizes.Value.Count.ShouldBe(0);
        imageSizes.Value.ShouldNotContain(DefaultAIImageSize1024);
        imageSizes.Value.ShouldNotContain(DefaultAIImageSize512);
        imageSizes.Value.ShouldNotContain(DefaultAIImageSize256);
        
        result = await ForestContractStub.RemoveAIImageSize.SendWithExceptionAsync(new StringValue(){Value = DefaultAIImageSize256});
        result.TransactionResult.Error.ShouldContain("input size not exists");
        
        result = await ForestContractStub.RemoveAIImageSize.SendWithExceptionAsync(new StringValue());
        result.TransactionResult.Error.ShouldContain("Invalid input");
    }
    
    [Fact]
    public async Task CreateArt_Test()
    {
        await InitializeForestContract();
        var createArtInput = new CreateArtInput();

        var result = await ForestContractStub.CreateArt.SendWithExceptionAsync(createArtInput);
        result.TransactionResult.Error.ShouldContain("Invalid input number");
        
        createArtInput.Number = 10;
        result = await ForestContractStub.CreateArt.SendWithExceptionAsync(createArtInput);
        result.TransactionResult.Error.ShouldContain("Invalid input size");
        
        createArtInput.Size = "10x10";
        result = await ForestContractStub.CreateArt.SendWithExceptionAsync(createArtInput);
        result.TransactionResult.Error.ShouldContain("Invalid input size");

        createArtInput.Size = DefaultAIImageSize1024;
        createArtInput.Promt = "Promt str";
        createArtInput.NegativePrompt = "NegativePrompt str";
        createArtInput.Model = "Model str";
        createArtInput.Quality = "Quality str";
        createArtInput.Style = "Style str";
        createArtInput.PaintingStyle = "PaintingStyle str";
        
        var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Symbol = ElfSymbol,
            Owner = DefaultAddress
        });
        var admin = await ForestContractStub.GetAdministrator.CallAsync(new Empty());
                
        result = await Seller1ForestContractStub.CreateArt.SendWithExceptionAsync(createArtInput);
        result.TransactionResult.Error.ShouldContain("Check sender balance not enough.");
        
        await TokenContractStub.Transfer.SendAsync(new TransferInput() { To = User1Address, Symbol = ElfSymbol, Amount = AIServiceFee*createArtInput.Number });
        result = await Seller1ForestContractStub.CreateArt.SendWithExceptionAsync(createArtInput);
        result.TransactionResult.Error.ShouldContain("The allowance you set is less than required. Please reset it.");

        await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = ElfSymbol, Amount = AIServiceFee*createArtInput.Number });
        result = await Seller1ForestContractStub.CreateArt.SendAsync(createArtInput);

        //check init image size list
        var imageSizes = await ForestContractStub.GetAIImageSizes.CallAsync(new Empty());
        imageSizes.Value.Count.ShouldBe(3);
        imageSizes.Value.ShouldContain(DefaultAIImageSize1024);
        imageSizes.Value.ShouldContain(DefaultAIImageSize512);
        imageSizes.Value.ShouldContain(DefaultAIImageSize256);
        
        //check init ai service fee config
        var feeConfig = await ForestContractStub.GetAIServiceFee.CallAsync(new Empty());
        feeConfig.ShouldNotBe(null);
        feeConfig.Price.Amount.ShouldBe(AIServiceFee);
        feeConfig.Price.Symbol.ShouldBe(ElfSymbol);
        
        //result check
        var txId = result.TransactionResult.TransactionId;
        var createArtInfo  = await Seller1ForestContractStub.GetCreateArtInfo.CallAsync(new GetCreateArtInfoInput()
        {
            TransactionId = txId.ToHex(),
            Address = User1Address
        });
        createArtInfo.ShouldNotBe(null);
        createArtInfo.Promt.ShouldBe("Promt str");
        createArtInfo.Size.ShouldBe(DefaultAIImageSize1024);
        createArtInfo.NegativePrompt.ShouldBe("NegativePrompt str");
        createArtInfo.Model.ShouldBe("Model str");
        createArtInfo.Quality.ShouldBe("Quality str");
        createArtInfo.Style.ShouldBe("Style str");
        createArtInfo.PaintingStyle.ShouldBe("PaintingStyle str");
        createArtInfo.CostPrice.Amount.ShouldBe(AIServiceFee*createArtInput.Number);
        createArtInfo.CostPrice.Symbol.ShouldBe(ElfSymbol);
        
        //check log 
        var artCreatedEvent = ArtCreated.Parser
            .ParseFrom(result.TransactionResult.Logs.First(l => l.Name == nameof(ArtCreated))
                .NonIndexed);
        artCreatedEvent.ShouldNotBe(null);
        artCreatedEvent.Promt.ShouldBe("Promt str");
        artCreatedEvent.Size.ShouldBe(DefaultAIImageSize1024);
        artCreatedEvent.NegativePrompt.ShouldBe("NegativePrompt str");
        artCreatedEvent.Model.ShouldBe("Model str");
        artCreatedEvent.Quality.ShouldBe("Quality str");
        artCreatedEvent.Style.ShouldBe("Style str");
        artCreatedEvent.PaintingStyle.ShouldBe("PaintingStyle str");
        artCreatedEvent.CostPrice.Amount.ShouldBe(AIServiceFee*createArtInput.Number);
        artCreatedEvent.CostPrice.Symbol.ShouldBe(ElfSymbol);
    }
}