using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core.Extension;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Forest.Contracts.Auction
{
    public class AuctionContractTests : AuctionContractTestBase
    {
        [Fact]
        public async Task InitializeTests()
        {
            await Initialize();
            var admin = await AuctionContractStub.GetAdmin.CallAsync(new Empty());
            admin.ShouldBe(DefaultAddress);

            var result = await AuctionContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
            {
                Admin = DefaultAddress
            });
            result.TransactionResult.Error.ShouldContain("Already initialized.");
        }

        [Fact]
        public async Task InitializeTests_Fail()
        {
            var result = await AuctionContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
            {
                Admin = new Address()
            });
            result.TransactionResult.Error.ShouldContain("Invalid input admin.");

            result = await AuctionContractUserStub.Initialize.SendWithExceptionAsync(new InitializeInput
            {
                Admin = UserAddress
            });
            result.TransactionResult.Error.ShouldContain("No permission.");
        }

        [Fact]
        public async Task<Hash> CreateAuctionTests()
        {
            const string symbol = "SEED-1";
            const long amount = 1;
            const long duration = 100;
            const long maxExtensionTime = 100;
            const long countdownTime = 50;
            const int minMarkup = 10;
            const long startPrice = 100;

            await Initialize();
            await InitSeed();

            var counter = await AuctionContractStub.GetCurrentCounter.CallAsync(new StringValue
            {
                Value = "SEED-1"
            });
            counter.Value.ShouldBe(0);

            var result = await AuctionContractStub.CreateAuction.SendAsync(new CreateAuctionInput
            {
                Amount = amount,
                Symbol = symbol,
                AuctionConfig = new AuctionConfig
                {
                    Duration = duration,
                    MaxExtensionTime = maxExtensionTime,
                    CountdownTime = countdownTime,
                    MinMarkup = minMarkup,
                    StartImmediately = false
                },
                AuctionType = AuctionType.English,
                ReceivingAddress = ReceivingAddress,
                StartPrice = new Price
                {
                    Amount = 100,
                    Symbol = "ELF"
                }
            });
            
            var log = AuctionCreated.Parser.ParseFrom(result.TransactionResult.Logs.First().NonIndexed);
            var output = await AuctionContractStub.GetAuctionInfo.CallAsync(new GetAuctionInfoInput
            {
                AuctionId = log.AuctionId
            });

            output.AuctionId.ShouldBe(log.AuctionId);
            output.AuctionConfig.Duration.ShouldBe(duration);
            output.AuctionConfig.MaxExtensionTime.ShouldBe(maxExtensionTime);
            output.AuctionConfig.CountdownTime.ShouldBe(countdownTime);
            output.AuctionConfig.MinMarkup.ShouldBe(minMarkup);
            output.Symbol.ShouldBe(symbol);
            output.Amount.ShouldBe(amount);
            output.AuctionType.ShouldBe(AuctionType.English);
            output.ReceivingAddress.ShouldBe(ReceivingAddress);
            output.StartPrice.Symbol.ShouldBe("ELF");
            output.StartPrice.Amount.ShouldBe(startPrice);
            output.Creator.ShouldBe(DefaultAddress);
            output.StartTime.ShouldBeNull();
            output.EndTime.ShouldBeNull();
            output.MaxEndTime.ShouldBeNull();
            output.FinishTime.ShouldBeNull();
            output.LastBidInfo.ShouldBeNull();
            output.AuctionId.ShouldBe(HashHelper.ConcatAndCompute(result.TransactionResult.TransactionId, HashHelper.ConcatAndCompute(HashHelper.ComputeFrom("SEED-1"), HashHelper.ComputeFrom(counter.Value))));

            counter = await AuctionContractStub.GetCurrentCounter.CallAsync(new StringValue
            {
                Value = "SEED-1"
            });
            counter.Value.ShouldBe(1);

            return output.AuctionId;
        }
        
        [Fact]
        public async Task CreateAuctionTests_Fail()
        {
            var result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput());
            result.TransactionResult.Error.ShouldContain("Not initialized.");

            await Initialize();
            
            result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput());
            result.TransactionResult.Error.ShouldContain("Invalid input symbol.");
            
            result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput
            {
                Symbol = "SEED-1"
            });
            result.TransactionResult.Error.ShouldContain("Invalid input amount");
            
            result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput
            {
                Symbol = "SEED-1",
                Amount = 0
            });
            result.TransactionResult.Error.ShouldContain("Invalid input amount");
            
            result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput
            {
                Symbol = "SEED-1",
                Amount = 1
            });
            result.TransactionResult.Error.ShouldContain("Invalid input auction type.");
            
            result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput
            {
                Symbol = "SEED-1",
                Amount = 1,
                AuctionType = AuctionType.English
            });
            result.TransactionResult.Error.ShouldContain("Invalid input price.");
            
            result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput
            {
                Symbol = "SEED-1",
                Amount = 1,
                AuctionType = AuctionType.English,
                ReceivingAddress = new Address()
            });
            result.TransactionResult.Error.ShouldContain("Invalid input receiving address.");
            
            result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput
            {
                Symbol = "SEED-1",
                Amount = 1,
                AuctionType = AuctionType.English,
                ReceivingAddress = ReceivingAddress
            });
            result.TransactionResult.Error.ShouldContain("Invalid input price.");
            
            result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput
            {
                Symbol = "SEED-1",
                Amount = 1,
                AuctionType = AuctionType.English,
                StartPrice = new Price
                {
                    Symbol = "ELF",
                    Amount = 100
                }
            });
            result.TransactionResult.Error.ShouldContain("Invalid input auction config.");
            
            result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput
            {
                Symbol = "SEED-1",
                Amount = 1,
                AuctionType = AuctionType.English,
                StartPrice = new Price
                {
                    Symbol = "ELF",
                    Amount = 100
                },
                AuctionConfig = new AuctionConfig()
            });
            result.TransactionResult.Error.ShouldContain("Invalid input duration.");
            
            result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput
            {
                Symbol = "SEED-1",
                Amount = 1,
                AuctionType = AuctionType.English,
                StartPrice = new Price
                {
                    Symbol = "ELF",
                    Amount = 100
                },
                AuctionConfig = new AuctionConfig
                {
                    Duration = 100
                }
            });
            result.TransactionResult.Error.ShouldContain("Invalid input min markup.");
            
            result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput
            {
                Symbol = "SEED-1",
                Amount = 1,
                AuctionType = AuctionType.English,
                StartPrice = new Price
                {
                    Symbol = "ELF",
                    Amount = 100
                },
                AuctionConfig = new AuctionConfig
                {
                    Duration = 100,
                    MinMarkup = 10
                }
            });
            result.TransactionResult.Error.ShouldContain("Token is not found.");
        }

        [Fact]
        public async Task<Hash> PlaceBidTests()
        {
            const long userInitBalance = 500;
            const long user2InitBalance = 500;
            const long userBid = 200;
            const long user2Bid = 300;
            const long userAfterBalance = 300;
            const long user2AfterBalance = 200;

            const long duration = 100;
            const long maxExtensionTime = 100;
            const long countdownTime = 50;

            var auctionId = await CreateAuctionTests();

            await InitToken(UserAddress, userInitBalance);
            await InitToken(User2Address, user2InitBalance);
            await Approve(TokenContractUserStub);
            await Approve(TokenContractUser2Stub);

            GetBalance("ELF", UserAddress).Result.ShouldBe(userInitBalance);
            GetBalance("ELF", User2Address).Result.ShouldBe(user2InitBalance);
            GetBalance("ELF", AuctionContractAddress).Result.ShouldBe(0);

            await AuctionContractUserStub.PlaceBid.SendAsync(new PlaceBidInput
            {
                AuctionId = auctionId,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = userBid
                }
            });

            var output = await AuctionContractStub.GetAuctionInfo.CallAsync(new GetAuctionInfoInput
            {
                AuctionId = auctionId
            });
            var currentBlockTime = BlockTimeProvider.GetBlockTime();
            output.LastBidInfo.Bidder.ShouldBe(UserAddress);
            output.LastBidInfo.BidTime.ShouldBe(currentBlockTime);
            output.LastBidInfo.Price.ShouldBe(new Price
            {
                Symbol = "ELF",
                Amount = userBid
            });
            output.StartTime.ShouldBe(currentBlockTime);
            output.EndTime.ShouldBe(currentBlockTime.AddSeconds(duration));
            output.MaxEndTime.ShouldBe(currentBlockTime.AddSeconds(duration + maxExtensionTime));

            GetBalance("ELF", UserAddress).Result.ShouldBe(userAfterBalance);
            GetBalance("ELF", AuctionContractAddress).Result.ShouldBe(userBid);
            
            BlockTimeProvider.SetBlockTime(currentBlockTime.AddSeconds(60));
            currentBlockTime = BlockTimeProvider.GetBlockTime();

            await AuctionContractUser2Stub.PlaceBid.SendAsync(new PlaceBidInput
            {
                AuctionId = auctionId,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = user2Bid
                }
            });

            GetBalance("ELF", UserAddress).Result.ShouldBe(userInitBalance);
            GetBalance("ELF", User2Address).Result.ShouldBe(user2AfterBalance);
            GetBalance("ELF", AuctionContractAddress).Result.ShouldBe(user2Bid);
            
            output = await AuctionContractStub.GetAuctionInfo.CallAsync(new GetAuctionInfoInput
            {
                AuctionId = auctionId
            });
            output.EndTime.ShouldBe(currentBlockTime.AddSeconds(duration - 60 + countdownTime));

            return auctionId;
        }
        
        [Fact]
        public async Task PlaceBidTests_Fail()
        {
            var output = await AuctionContractUserStub.PlaceBid.SendWithExceptionAsync(new PlaceBidInput());
            output.TransactionResult.Error.ShouldContain("Invalid input auction id.");
            
            var auctionId = await CreateAuctionTests();
            
            output = await AuctionContractUserStub.PlaceBid.SendWithExceptionAsync(new PlaceBidInput
            {
                AuctionId = auctionId
            });
            output.TransactionResult.Error.ShouldContain("Invalid input price.");
            
            await InitToken(UserAddress, 500);
            await Approve(TokenContractUserStub);
            
            output = await AuctionContractUserStub.PlaceBid.SendWithExceptionAsync(new PlaceBidInput
            {
                AuctionId = auctionId,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 100
                }
            });
            output.TransactionResult.Error.ShouldContain("Bid price not high enough.");
            
            await AuctionContractUserStub.PlaceBid.SendAsync(new PlaceBidInput
            {
                AuctionId = auctionId,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 200
                }
            });

            var currentBlockTime = BlockTimeProvider.GetBlockTime();
            BlockTimeProvider.SetBlockTime(currentBlockTime.AddSeconds(100));
            output = await AuctionContractUserStub.PlaceBid.SendWithExceptionAsync(new PlaceBidInput
            {
                AuctionId = auctionId,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 300
                }
            });
            output.TransactionResult.Error.ShouldContain("Auction finished. Bid failed.");
        }

        [Fact]
        public async Task ClaimTests()
        {
            var auctionId = await PlaceBidTests();
            
            GetBalance("SEED-1", User2Address).Result.ShouldBe(0);
            GetBalance("SEED-1", AuctionContractAddress).Result.ShouldBe(1);
            GetBalance("ELF", ReceivingAddress).Result.ShouldBe(0);
            
            var output = await AuctionContractStub.GetAuctionInfo.CallAsync(new GetAuctionInfoInput
            {
                AuctionId = auctionId
            });
            output.FinishTime.ShouldBeNull();
            
            var currentBlockTime = BlockTimeProvider.GetBlockTime();
            BlockTimeProvider.SetBlockTime(currentBlockTime.AddSeconds(500));

            await AuctionContractStub.Claim.SendAsync(new ClaimInput
            {
                AuctionId = auctionId
            });
            
            currentBlockTime = BlockTimeProvider.GetBlockTime();
            output = await AuctionContractStub.GetAuctionInfo.CallAsync(new GetAuctionInfoInput
            {
                AuctionId = auctionId
            });
            output.FinishTime.ShouldBe(currentBlockTime);
            
            GetBalance("SEED-1", User2Address).Result.ShouldBe(1);
            GetBalance("SEED-1", AuctionContractAddress).Result.ShouldBe(0);
            GetBalance("ELF", ReceivingAddress).Result.ShouldBe(300);
        }
        
        [Fact]
        public async Task ClaimTests_Fail()
        {
            var result = await AuctionContractStub.Claim.SendWithExceptionAsync(new ClaimInput());
            result.TransactionResult.Error.ShouldContain("Invalid input auction id.");
            
            result = await AuctionContractStub.Claim.SendWithExceptionAsync(new ClaimInput
            {
                AuctionId = HashHelper.ComputeFrom("123")
            });
            result.TransactionResult.Error.ShouldContain("Auction not exist.");
            
            var auctionId = await CreateAuctionTests();
            result = await AuctionContractStub.Claim.SendWithExceptionAsync(new ClaimInput
            {
                AuctionId = auctionId
            });
            result.TransactionResult.Error.ShouldContain("Auction not start yet.");
            
            await InitToken(UserAddress, 500);
            await Approve(TokenContractUserStub);
            
            await AuctionContractUserStub.PlaceBid.SendAsync(new PlaceBidInput
            {
                AuctionId = auctionId,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 200
                }
            });
            
            result = await AuctionContractStub.Claim.SendWithExceptionAsync(new ClaimInput
            {
                AuctionId = auctionId
            });
            result.TransactionResult.Error.ShouldContain("Auction not end yet.");
        }

        private async Task Initialize()
        {
            await AuctionContractStub.Initialize.SendAsync(new InitializeInput
            {
                Admin = DefaultAddress
            });
        }

        private async Task InitSeed()
        {
            await TokenContractStub.Create.SendAsync(
                new CreateInput
                {
                    Owner = DefaultAddress,
                    Issuer = DefaultAddress,
                    Symbol = "SEED-0",
                    TokenName = "TOKEN SEED-0",
                    TotalSupply = 1,
                    Decimals = 0,
                    IsBurnable = false,
                    LockWhiteList = { TokenContractAddress }
                });

            var externalInfo = new ExternalInfo();
            externalInfo.Value.Add("__seed_exp_time",
                BlockTimeProvider.GetBlockTime().AddSeconds(1000).Seconds.ToString());
            externalInfo.Value.Add("__seed_owned_symbol", "LUCK");

            await TokenContractStub.Create.SendAsync(new CreateInput
            {
                Owner = DefaultAddress,
                Issuer = DefaultAddress,
                Symbol = "SEED-1",
                TokenName = "SEED-LUCK",
                TotalSupply = 1,
                Decimals = 0,
                IsBurnable = true,
                LockWhiteList = { TokenContractAddress },
                ExternalInfo = externalInfo
            });

            await TokenContractStub.Issue.SendAsync(new IssueInput
            {
                Amount = 1,
                Symbol = "SEED-1",
                Memo = "test",
                To = DefaultAddress
            });

            var output = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = DefaultAddress,
                Symbol = "SEED-1"
            });
            output.Balance.ShouldBe(1);

            await TokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 1,
                Spender = AuctionContractAddress,
                Symbol = "SEED-1"
            });
        }

        private async Task InitToken(Address address, long amount)
        {
            await TokenContractStub.Transfer.SendAsync(new TransferInput
            {
                Amount = amount,
                Symbol = "ELF",
                To = address
            });
        }

        private async Task Approve(TokenContractContainer.TokenContractStub stub)
        {
            await stub.Approve.SendAsync(new ApproveInput
            {
                Spender = AuctionContractAddress,
                Amount = 1000000000000,
                Symbol = "ELF"
            });
        }

        private async Task<long> GetBalance(string symbol, Address address)
        {
            var output = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = address,
                Symbol = symbol
            });

            return output.Balance;
        }
    }
}