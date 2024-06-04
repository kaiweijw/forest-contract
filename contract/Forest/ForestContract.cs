using System.Collections.Specialized;
using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using AElf.Sdk.CSharp.State;
using AElf.CSharp.Core;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Microsoft.VisualBasic;

namespace Forest
{
    /// <summary>
    /// The C# implementation of the contract defined in forest_contract.proto that is located in the "protobuf"
    /// folder.
    /// Notice that it inherits from the protobuf generated code. 
    /// </summary>
    public partial class ForestContract : ForestContractContainer.ForestContractBase
    {
        public override Empty Initialize(InitializeInput input)
        {
            Assert(State.TokenContract.Value == null, "Already initialized.");
            Assert(input.WhitelistContractAddress != null, "Empty WhitelistContractAddress");
            State.Admin.Value = input.AdminAddress ?? Context.Sender;
            State.ServiceFeeRate.Value = input.ServiceFeeRate == 0 ? DefaultServiceFeeRate : input.ServiceFeeRate;
            State.ServiceFeeReceiver.Value = input.ServiceFeeReceiver ?? State.Admin.Value;
            State.ServiceFee.Value = input.ServiceFee == 0 ? DefaultServiceFeeAmount : input.ServiceFee;
            State.TokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
            State.WhitelistContract.Value = input.WhitelistContractAddress;
            State.GlobalTokenWhiteList.Value = new StringList
            {
                Value = { Context.Variables.NativeSymbol }
            };
            State.BizConfig.Value = new BizConfig
            {
                MaxListCount = DefaultMaxListCount,
                MaxOfferCount = DefaultMaxOfferCount,
                MaxTokenWhitelistCount = DefaultMaxTokenWhiteListCount,
                MaxOfferDealCount = DefaultMaxOfferDealCount
            };
            return new Empty();
        }

        public override Empty SetAdministrator(Address input)
        {
            AssertSenderIsAdmin();
            Assert(input != null, "Empty Address");
            State.Admin.Value = input;
            return new Empty();
        }

        public override Empty SetServiceFee(SetServiceFeeInput input)
        {
            AssertSenderIsAdmin();
            Assert(input.ServiceFeeRate >= 0, "Invalid ServiceFeeRate");
            State.ServiceFeeRate.Value = input.ServiceFeeRate;
            State.ServiceFeeReceiver.Value = input.ServiceFeeReceiver ?? State.Admin.Value;
            return new Empty();
        }
        
        public override Empty SetGlobalTokenWhiteList(StringList input)
        {
            AssertSenderIsAdmin();
            if (!input.Value.Contains(Context.Variables.NativeSymbol))
            {
                input.Value.Add(Context.Variables.NativeSymbol);
            }

            foreach (var symbol in input.Value)
            {
                var tokenInfo = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
                {
                    Symbol = symbol
                });
                Assert(tokenInfo?.Symbol?.Length > 0, "Invalid token : " + symbol);
            }

            State.GlobalTokenWhiteList.Value = input;
            Context.Fire(new GlobalTokenWhiteListChanged
            {
                TokenWhiteList = input
            });
            return new Empty();
        }

        public override Empty SetWhitelistContract(Address input)
        {
            AssertSenderIsAdmin();
            Assert(input != null, "Empty contract address");
            State.WhitelistContract.Value = input;
            return new Empty();
        }

        public override Empty SetBizConfig(BizConfig bizConfig)
        {
            AssertSenderIsAdmin();
            Assert(bizConfig != null, "Empty bizConfig");
            Assert(bizConfig?.MaxTokenWhitelistCount > 0
                   && bizConfig?.MaxListCount > 0
                   && bizConfig?.MaxOfferCount > 0
                   && bizConfig?.MaxOfferDealCount > 0,
                "Count config should greater than 0");
            State.BizConfig.Value = bizConfig;
            return new Empty();
        }

        private void AssertWhitelistContractInitialized()
        {
            Assert(State.WhitelistContract.Value != null, "Whitelist Contract not initialized.");
        }

        private void AssertSenderIsAdmin()
        {
            AssertContractInitialized();
            Assert(Context.Sender == State.Admin.Value, "No permission.");
        }

        private void AssertContractInitialized()
        {
            Assert(State.Admin.Value != null, "Contract not initialized.");
        }
        
        public override Empty SetOfferTotalAmount(SetOfferTotalAmountInput input)
        {
            AssertSenderIsAdmin();
            Assert(input.Address != null, $"Invalid param Address");
            Assert(input.PriceSymbol != null, $"Invalid param PriceSymbol");
            Assert(input.TotalAmount >= 0, "Invalid param TotalAmount");
            
            State.OfferTotalAmountMap[input.Address][input.PriceSymbol] = input.TotalAmount;
            return new Empty();
        }
        
        public override Empty SetAIServiceFee(SetAIServiceFeeInput input)
        {
            AssertSenderIsAdmin();
            Assert(input is { Price: { Amount: >= 0, Symbol: AIServiceFeeToken } } && input.ServiceFeeReceiver != null, "Invalid AIServiceFeeRate");
            State.AIServiceFeeConfig.Value = input.Price; 
            State.AIServiceFeeReceiver.Value = input.ServiceFeeReceiver;
            return new Empty();
        }
        
        public override Empty AddAIImageSize(StringValue input)
        {
            AssertSenderIsAdmin();
            Assert(input is { Value: { } } && input.Value != "", "Invalid input");
            var sizeList = State.AIImageSizeList.Value;
            if (sizeList == null)
            {
                State.AIImageSizeList.Value = new StringList()
                {
                    Value  = { input.Value }
                };
                return new Empty();
            }

            Assert(!sizeList.Value.Contains(input.Value), "input size Already exists");
            
            sizeList.Value.Add(input.Value);
            State.AIImageSizeList.Value = new StringList
            {
                Value =  { sizeList.Value }
            };
            return new Empty();
        }
        
        public override Empty RemoveAIImageSize(StringValue input)
        {
            AssertSenderIsAdmin();
            Assert(input is { Value: { } } && input.Value != "", "Invalid input");
            var sizeList = State.AIImageSizeList.Value;
            if (sizeList?.Value == null)
            {
                return new Empty();
            }
            Assert(sizeList.Value.Contains(input.Value), "input size not exists");
            sizeList.Value.Remove(input.Value);
            State.AIImageSizeList.Value = new StringList
            {
                Value =  { sizeList.Value }
            };
            return new Empty();
        }
        
        public override Empty CreateArt(CreateArtInput input)
        {
            AssertContractInitialized();
            RequireContractAIServiceFeeConfigSet();
            RequireContractAIImageSizeListSet();
            CheckCreateArtParams(input);
            var aiServiceFeeConfig = State.AIServiceFeeConfig.Value;
            var balance = State.TokenContract.GetBalance.Call(new GetBalanceInput
            {
                Symbol = aiServiceFeeConfig.Symbol,
                Owner = Context.Sender
            });
            var serviceFee = aiServiceFeeConfig.Amount.Mul(input.Number);
            Assert(balance.Balance >= serviceFee, "Check sender balance not enough.");
            AssertAllowanceInsufficient(aiServiceFeeConfig.Symbol, Context.Sender, serviceFee);
            if (serviceFee > 0)
            {
                State.TokenContract.TransferFrom.Send(new TransferFromInput
                {
                    From = Context.Sender,
                    To = State.AIServiceFeeReceiver.Value,
                    Symbol = aiServiceFeeConfig.Symbol,
                    Amount = serviceFee
                });
            }
            
            State.CreateArtInfoMap[Context.Sender][Context.OriginTransactionId.ToHex()] = new CreateArtInfo()
            {
                Promt = input.Promt,
                NegativePrompt = input.NegativePrompt,
                Model = input.Model,
                Quality = input.Quality,
                Style = input.Style,
                Size = input.Size,
                Number = input.Number,
                CostPrice = new Price()
                {
                    Symbol = aiServiceFeeConfig.Symbol,
                    Amount = serviceFee
                },
                PaintingStyle = input.PaintingStyle
            };
            
            Context.Fire(new ArtCreated()
            {
                Promt = input.Promt,
                NegativePrompt = input.NegativePrompt,
                Model = input.Model,
                Quality = input.Quality,
                Style = input.Style,
                Size = input.Size,
                Number = input.Number,
                CostPrice = new Price()
                {
                    Symbol = aiServiceFeeConfig.Symbol,
                    Amount = serviceFee
                },
                PaintingStyle = input.PaintingStyle
            });
            return new Empty();
        }
    }
}