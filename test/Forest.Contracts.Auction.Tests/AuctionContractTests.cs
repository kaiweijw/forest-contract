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

            // initialize twice
            var result = await AuctionContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
            {
                Admin = DefaultAddress
            });
            result.TransactionResult.Error.ShouldContain("Already initialized.");
        }

        [Fact]
        public async Task InitializeTests_AuctionController()
        {
            await AuctionContractStub.Initialize.SendAsync(new InitializeInput
            {
                AuctionController = { DefaultAddress, UserAddress, User2Address }
            });

            var result = await AuctionContractStub.GetAuctionController.CallAsync(new Empty());
            result.Controllers.Count.ShouldBe(3);
        }

        [Fact]
        public async Task InitializeTests_Fail()
        {
            // empty address
            var result = await AuctionContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
            {
                Admin = new Address()
            });
            result.TransactionResult.Error.ShouldContain("Invalid input admin.");

            // sender != author
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
            output.AuctionId.ShouldBe(HashHelper.ConcatAndCompute(result.TransactionResult.TransactionId,
                HashHelper.ConcatAndCompute(HashHelper.ComputeFrom("SEED-1"), HashHelper.ComputeFrom(counter.Value))));

            counter = await AuctionContractStub.GetCurrentCounter.CallAsync(new StringValue
            {
                Value = "SEED-1"
            });
            counter.Value.ShouldBe(1);

            return output.AuctionId;
        }

        [Fact]
        public async Task CreateAuctionTests_Other()
        {
            await Initialize();
            await InitSeed();

            await AuctionContractStub.AddAuctionController.SendAsync(new AddAuctionControllerInput
            {
                Addresses = new ControllerList
                {
                    Controllers = { UserAddress }
                }
            });

            var counter = await AuctionContractStub.GetCurrentCounter.CallAsync(new StringValue
            {
                Value = "SEED-1"
            });
            counter.Value.ShouldBe(0);

            var result = await AuctionContractStub.CreateAuction.SendAsync(new CreateAuctionInput
            {
                Symbol = "SEED-1",
                AuctionConfig = new AuctionConfig
                {
                    Duration = 10,
                    MaxExtensionTime = 10,
                    CountdownTime = 10,
                    MinMarkup = 0,
                    StartImmediately = true
                },
                AuctionType = AuctionType.English,
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

            output.ReceivingAddress.ShouldBe(DefaultAddress);

            var currentBlockTime = BlockTimeProvider.GetBlockTime();

            output.StartTime.ShouldBe(currentBlockTime);

            await InitToken(UserAddress, 500);
            await Approve(TokenContractUserStub);

            // bid price <= start price
            result = await AuctionContractUserStub.PlaceBid.SendWithExceptionAsync(new PlaceBidInput
            {
                AuctionId = log.AuctionId,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 100
                }
            });
            result.TransactionResult.Error.ShouldContain("Bid price not high enough.");

            await AuctionContractUserStub.PlaceBid.SendAsync(new PlaceBidInput
            {
                AuctionId = log.AuctionId,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 101
                }
            });

            await InitToken(User2Address, 500);
            await Approve(TokenContractUser2Stub);

            // two bidder bid at same price
            result = await AuctionContractUser2Stub.PlaceBid.SendWithExceptionAsync(new PlaceBidInput
            {
                AuctionId = log.AuctionId,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 101
                }
            });
            result.TransactionResult.Error.ShouldContain("Bid price not high enough.");

            output = await AuctionContractStub.GetAuctionInfo.CallAsync(new GetAuctionInfoInput
            {
                AuctionId = log.AuctionId
            });

            output.EndTime.ShouldBe(output.MaxEndTime);

            BlockTimeProvider.SetBlockTime(currentBlockTime.AddSeconds(5));

            await AuctionContractUserStub.PlaceBid.SendAsync(new PlaceBidInput
            {
                AuctionId = log.AuctionId,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 102
                }
            });

            output = await AuctionContractStub.GetAuctionInfo.CallAsync(new GetAuctionInfoInput
            {
                AuctionId = log.AuctionId
            });

            output.EndTime.ShouldBe(output.MaxEndTime);

            BlockTimeProvider.SetBlockTime(currentBlockTime.AddSeconds(20));

            // bid after max extension time
            result = await AuctionContractUserStub.PlaceBid.SendWithExceptionAsync(new PlaceBidInput
            {
                AuctionId = log.AuctionId,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 103
                }
            });
            result.TransactionResult.Error.ShouldContain("Auction finished. Bid failed.");

            await AuctionContractUser2Stub.Claim.SendAsync(new ClaimInput
            {
                AuctionId = log.AuctionId
            });

            var balance = await GetBalance("SEED-1", UserAddress);
            balance.ShouldBe(1);

            await TokenContractUserStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 1,
                Spender = AuctionContractAddress,
                Symbol = "SEED-1"
            });

            result = await AuctionContractUserStub.CreateAuction.SendAsync(new CreateAuctionInput
            {
                Symbol = "SEED-1",
                AuctionConfig = new AuctionConfig
                {
                    Duration = 10,
                    MaxExtensionTime = 0,
                    CountdownTime = 0,
                    MinMarkup = 0,
                    StartImmediately = true
                },
                AuctionType = AuctionType.English,
                ReceivingAddress = ReceivingAddress,
                StartPrice = new Price
                {
                    Amount = 100,
                    Symbol = "ELF"
                }
            });

            log = AuctionCreated.Parser.ParseFrom(result.TransactionResult.Logs.First().NonIndexed);
            log.ShouldNotBeNull();
        }

        [Fact]
        public async Task CreateAuctionTests_Fail()
        {
            var result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput());
            result.TransactionResult.Error.ShouldContain("Not initialized.");

            await Initialize();

            result = await AuctionContractUserStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput());
            result.TransactionResult.Error.ShouldContain("No sale controller permission.");

            result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput());
            result.TransactionResult.Error.ShouldContain("Invalid input symbol.");

            result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput
            {
                Symbol = "SEED"
            });
            result.TransactionResult.Error.ShouldContain("Only support NFT.");

            result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput
            {
                Symbol = "SEED-0"
            });
            result.TransactionResult.Error.ShouldContain("Only support NFT.");

            result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput
            {
                Symbol = "SEED-1"
            });
            result.TransactionResult.Error.ShouldContain("Token not found.");

            await InitSeed();
            await InitSeed_Wrong();

            result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput
            {
                Symbol = "SEED-2"
            });
            result.TransactionResult.Error.ShouldContain("Only support 721 type NFT.");

            result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput
            {
                Symbol = "SEED-1"
            });
            result.TransactionResult.Error.ShouldContain("Invalid input auction type.");

            result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput
            {
                Symbol = "SEED-1",
                AuctionType = AuctionType.English
            });
            result.TransactionResult.Error.ShouldContain("Invalid input price.");

            result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput
            {
                Symbol = "SEED-1",
                AuctionType = AuctionType.English,
                ReceivingAddress = new Address()
            });
            result.TransactionResult.Error.ShouldContain("Invalid input receiving address.");

            result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput
            {
                Symbol = "SEED-1",
                AuctionType = AuctionType.English,
                ReceivingAddress = ReceivingAddress
            });
            result.TransactionResult.Error.ShouldContain("Invalid input price.");

            result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput
            {
                Symbol = "SEED-1",
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
                AuctionType = AuctionType.English,
                StartPrice = new Price
                {
                    Symbol = "ELF",
                    Amount = 100
                },
                AuctionConfig = new AuctionConfig
                {
                    Duration = 100,
                    MinMarkup = -1
                }
            });
            result.TransactionResult.Error.ShouldContain("Invalid input min markup.");

            result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput
            {
                Symbol = "SEED-1",
                AuctionType = AuctionType.English,
                StartPrice = new Price
                {
                    Symbol = "ELF",
                    Amount = 100
                },
                AuctionConfig = new AuctionConfig
                {
                    Duration = 100,
                    MinMarkup = 0,
                    MaxExtensionTime = -1
                }
            });
            result.TransactionResult.Error.ShouldContain("Invalid input max extension time.");

            result = await AuctionContractStub.CreateAuction.SendWithExceptionAsync(new CreateAuctionInput
            {
                Symbol = "SEED-1",
                AuctionType = AuctionType.English,
                StartPrice = new Price
                {
                    Symbol = "ELF",
                    Amount = 100
                },
                AuctionConfig = new AuctionConfig
                {
                    Duration = 100,
                    MinMarkup = 0,
                    MaxExtensionTime = 0,
                    CountdownTime = -1
                }
            });
            result.TransactionResult.Error.ShouldContain("Invalid input countdown time.");
        }

        [Fact]
        public async Task<Hash> PlaceBidTests()
        {
            const long userInitBalance = 500;
            const long user2InitBalance = 500;
            const long userBid = 110;
            const long userBid2 = 150;
            const long user2Bid = 200;

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

            GetBalance("ELF", UserAddress).Result.ShouldBe(userInitBalance - userBid);
            GetBalance("ELF", AuctionContractAddress).Result.ShouldBe(userBid);

            BlockTimeProvider.SetBlockTime(currentBlockTime.AddSeconds(30));
            currentBlockTime = BlockTimeProvider.GetBlockTime();

            await AuctionContractUserStub.PlaceBid.SendAsync(new PlaceBidInput
            {
                AuctionId = auctionId,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = userBid2
                }
            });

            GetBalance("ELF", UserAddress).Result.ShouldBe(userInitBalance - userBid2);
            GetBalance("ELF", AuctionContractAddress).Result.ShouldBe(userBid2);

            BlockTimeProvider.SetBlockTime(currentBlockTime.AddSeconds(30));
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
            GetBalance("ELF", User2Address).Result.ShouldBe(user2InitBalance - user2Bid);
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

            output = await AuctionContractUserStub.PlaceBid.SendWithExceptionAsync(new PlaceBidInput
            {
                AuctionId = auctionId,
                Price = new Price
                {
                    Symbol = "TEST",
                    Amount = 200
                }
            });
            output.TransactionResult.Error.ShouldContain("Invalid input price symbol.");

            await Approve(TokenContractUserStub);

            // not have enough balance
            output = await AuctionContractUserStub.PlaceBid.SendWithExceptionAsync(new PlaceBidInput
            {
                AuctionId = auctionId,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 200
                }
            });
            output.TransactionResult.Error.ShouldContain("Insufficient balance");

            await InitToken(UserAddress, 500);

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

            // place bid at end time
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
            GetBalance("ELF", ReceivingAddress).Result.ShouldBe(200);
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

            // claim during auction
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
            await AuctionContractStub.Initialize.SendAsync(new InitializeInput());
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

        private async Task InitSeed_Wrong()
        {
            var externalInfo = new ExternalInfo();
            externalInfo.Value.Add("__seed_exp_time",
                BlockTimeProvider.GetBlockTime().AddSeconds(1000).Seconds.ToString());
            externalInfo.Value.Add("__seed_owned_symbol", "TEST");

            await TokenContractStub.Create.SendAsync(new CreateInput
            {
                Owner = DefaultAddress,
                Issuer = DefaultAddress,
                Symbol = "SEED-2",
                TokenName = "SEED-TEST",
                TotalSupply = 2,
                Decimals = 0,
                IsBurnable = true,
                LockWhiteList = { TokenContractAddress },
                ExternalInfo = externalInfo
            });

            await TokenContractStub.Issue.SendAsync(new IssueInput
            {
                Amount = 1,
                Symbol = "SEED-2",
                Memo = "test",
                To = DefaultAddress
            });

            await TokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 1,
                Spender = AuctionContractAddress,
                Symbol = "SEED-2"
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

        [Fact]
        public async Task AddAuctionControllerTests()
        {
            await Initialize();
            await AuctionContractStub.AddAuctionController.SendAsync(new AddAuctionControllerInput
            {
                Addresses = new ControllerList
                {
                    Controllers = { DefaultAddress }
                }
            });
            await AuctionContractStub.AddAuctionController.SendAsync(new AddAuctionControllerInput
            {
                Addresses = new ControllerList
                {
                    Controllers = { UserAddress }
                }
            });

            var output = await AuctionContractStub.GetAuctionController.CallAsync(new Empty());
            output.Controllers.Count.ShouldBe(2);
            output.Controllers.First().ShouldBe(DefaultAddress);
            output.Controllers.Last().ShouldBe(UserAddress);
        }

        [Fact]
        public async Task AddAuctionControllerTests_Fail()
        {
            var result = await AuctionContractStub.AddAuctionController.SendWithExceptionAsync(
                new AddAuctionControllerInput
                {
                    Addresses = new ControllerList
                    {
                        Controllers = { UserAddress }
                    }
                });
            result.TransactionResult.Error.ShouldContain("Not initialized.");

            await Initialize();

            result = await AuctionContractUserStub.AddAuctionController.SendWithExceptionAsync(
                new AddAuctionControllerInput
                {
                    Addresses = new ControllerList
                    {
                        Controllers = { UserAddress }
                    }
                });
            result.TransactionResult.Error.ShouldContain("No permission.");
        }

        [Fact]
        public async Task RemoveAuctionControllerTests()
        {
            await Initialize();
            await AuctionContractStub.AddAuctionController.SendAsync(new AddAuctionControllerInput
            {
                Addresses = new ControllerList
                {
                    Controllers = { UserAddress }
                }
            });

            var output = await AuctionContractStub.GetAuctionController.CallAsync(new Empty());
            output.Controllers.Count.ShouldBe(2);
            output.Controllers.First().ShouldBe(DefaultAddress);
            output.Controllers.Last().ShouldBe(UserAddress);

            await AuctionContractStub.RemoveAuctionController.SendAsync(new RemoveAuctionControllerInput
            {
                Addresses = new ControllerList
                {
                    Controllers = { UserAddress }
                }
            });

            await AuctionContractStub.RemoveAuctionController.SendAsync(new RemoveAuctionControllerInput
            {
                Addresses = new ControllerList
                {
                    Controllers = { UserAddress }
                }
            });

            output = await AuctionContractStub.GetAuctionController.CallAsync(new Empty());
            output.Controllers.Count.ShouldBe(1);
            output.Controllers.Last().ShouldBe(DefaultAddress);
        }

        [Fact]
        public async Task RemoveAuctionControllerTests_Fail()
        {
            var result = await AuctionContractStub.RemoveAuctionController.SendWithExceptionAsync(
                new RemoveAuctionControllerInput
                {
                    Addresses = new ControllerList
                    {
                        Controllers = { UserAddress }
                    }
                });
            result.TransactionResult.Error.ShouldContain("Not initialized.");

            await Initialize();
            result = await AuctionContractUserStub.RemoveAuctionController.SendWithExceptionAsync(
                new RemoveAuctionControllerInput
                {
                    Addresses = new ControllerList
                    {
                        Controllers = { DefaultAddress }
                    }
                });
            result.TransactionResult.Error.ShouldContain("No permission.");
        }

        [Fact]
        public async Task SetAdminTests()
        {
            await Initialize();
            var output = await AuctionContractStub.GetAdmin.CallAsync(new Empty());
            output.ShouldBe(DefaultAddress);

            var result = await AuctionContractStub.SetAdmin.SendAsync(DefaultAddress);
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

            await AuctionContractStub.SetAdmin.SendAsync(UserAddress);
            output = await AuctionContractStub.GetAdmin.CallAsync(new Empty());
            output.ShouldBe(UserAddress);
        }

        [Fact]
        public async Task SetAdminTests_Fail()
        {
            await Initialize();

            var result = await AuctionContractStub.SetAdmin.SendWithExceptionAsync(new Address());
            result.TransactionResult.Error.ShouldContain("Invalid input.");

            result = await AuctionContractUserStub.SetAdmin.SendWithExceptionAsync(UserAddress);
            result.TransactionResult.Error.ShouldContain("No permission.");
        }

        [Fact]
        public async Task GetCurrentCounter_NotExists()
        {
            var output = await AuctionContractStub.GetCurrentCounter.CallAsync(new StringValue
            {
                Value = "TEST"
            });
            output.Value.ShouldBe(0);
        }
    }
}