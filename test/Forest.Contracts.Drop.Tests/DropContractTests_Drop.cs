/*using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Moq;
using Shouldly;
using Xunit;

namespace Forest.Contracts.Drop
{
    public class DropContractTests_Drop : DropContractTestBase
    {
        private const string CollectionSymbol = "TESTNFT-0";
        private const string NftSymbol1 = "TESTNFT-1";
        private const string NftSymbol2 = "TESTNFT-2";
        private const string ElfSymbol = "ELF";
        private const int ServiceFeeRate = 1000; // 10%
        private const long InitializeElfAmount = 10000_0000_0000;
        private async Task Initialize()
        {
            var init = new InitializeInput
            {
                MaxDropDetailIndexCount = 10,
                MaxDropDetailListCount = 100,
                ProxyAccountAddress = DefaultAddress
            };
            await DropContractStub.Initialize.SendAsync(init);
        }
        protected async Task CreateSeedCollection() 
        {
            await TokenContractStub.Create.SendAsync(new CreateInput()
            {
                Symbol = "SEED-0",
                TokenName = "SEED—collection",
                TotalSupply = 1,
                Decimals = 0,
                Issuer = DefaultAddress,
                IsBurnable = false,
                IssueChainId = 0,
                ExternalInfo = new ExternalInfo()
            });
        }

        protected async Task CreateSeed(string seed, string forNFTSymbol)
        {
            await TokenContractStub.Create.SendAsync(new CreateInput()
            {
                Symbol = seed,
                TokenName = seed,
                TotalSupply = 1,
                Decimals = 0,
                Issuer = DefaultAddress,
                IsBurnable = true,
                IssueChainId = 0,
                ExternalInfo = new ExternalInfo()
                {
                    Value = { 
                        new Dictionary<string, string>()
                        {
                            ["__seed_owned_symbol"] = forNFTSymbol,
                            ["__seed_exp_time"] = "9992145642"
                        }
                    }
                }
            });

        }
        private async Task PrepareNftData()
        {
            #region prepare SEED

            await CreateSeedCollection();
            await CreateSeed("SEED-1", CollectionSymbol);
            await TokenContractStub.Issue.SendAsync(new IssueInput()
                { Symbol = "SEED-1", To = DefaultAddress, Amount = 1 });
            await TokenContractStub.Approve.SendAsync(new ApproveInput()
                { Spender = TokenContractAddress, Symbol = "SEED-1", Amount = 1 });

            #endregion

            #region create NFTs

            {
                // create collections via MULTI-TOKEN-CONTRACT
                await TokenContractStub.Create.SendAsync(new CreateInput
                {
                    Symbol = CollectionSymbol,
                    TokenName = "TESTNFT—collection",
                    TotalSupply = 100,
                    Decimals = 0,
                    Issuer = DefaultAddress,
                    IsBurnable = false,
                    IssueChainId = 0,
                    ExternalInfo = new ExternalInfo()
                });

                // create NFT via MULTI-TOKEN-CONTRACT
                await TokenContractStub.Create.SendAsync(new CreateInput
                {
                    Symbol = NftSymbol1,
                    TokenName = NftSymbol1,
                    TotalSupply = 100,
                    Decimals = 0,
                    Issuer = DefaultAddress,
                    IsBurnable = false,
                    IssueChainId = 0,
                    ExternalInfo = new ExternalInfo()
                });

                // create NFT via MULTI-TOKEN-CONTRACT
                await TokenContractStub.Create.SendAsync(new CreateInput
                {
                    Symbol = NftSymbol2,
                    TokenName = NftSymbol2,
                    TotalSupply = 100,
                    Decimals = 0,
                    Issuer = DefaultAddress,
                    IsBurnable = false,
                    IssueChainId = 0,
                    ExternalInfo = new ExternalInfo()
                });
            }

            #endregion

            #region issue NFTs and check

            {
                // issue 10 NFTs to self
                await TokenContractStub.Issue.SendAsync(new IssueInput()
                {
                    Symbol = NftSymbol1,
                    Amount = 10,
                    To = DropContractAddress
                });
                
                // issue 10 NFTs to self
                await TokenContractStub.Issue.SendAsync(new IssueInput()
                {
                    Symbol = NftSymbol2,
                    Amount = 10,
                    To = DropContractAddress
                });

                // got 100-totalSupply and 10-supply
                var tokenInfo = await TokenContractStub.GetTokenInfo.SendAsync(new GetTokenInfoInput()
                {
                    Symbol = NftSymbol1,
                });

                tokenInfo.Output.TotalSupply.ShouldBe(100);
                tokenInfo.Output.Supply.ShouldBe(10);


                var nftBalance = await TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
                {
                    Symbol = NftSymbol1,
                    Owner = DropContractAddress
                });
                nftBalance.Output.Balance.ShouldBe(10);
            }

            #endregion

            #region prepare ELF token

            {
                // transfer thousand ELF to seller
                await TokenContractStub.Transfer.SendAsync(new TransferInput()
                {
                    To = UserAddress,
                    Symbol = ElfSymbol,
                    Amount = InitializeElfAmount
                });

                // transfer thousand ELF to buyer
                await TokenContractStub.Transfer.SendAsync(new TransferInput()
                {
                    To = UserAddress,
                    Symbol = ElfSymbol,
                    Amount = InitializeElfAmount
                });
            }

            #endregion

            #region approve transfer

            {
                // approve contract handle NFT of seller   
                await TokenContractStub.Approve.SendAsync(new ApproveInput()
                {
                    Symbol = NftSymbol1,
                    Amount = 5,
                    Spender = DropContractAddress
                });

                // approve contract handle NFT2 of seller   
                await TokenContractStub.Approve.SendAsync(new ApproveInput()
                {
                    Symbol = NftSymbol2,
                    Amount = 5,
                    Spender = DropContractAddress
                });

                // approve contract handle ELF of buyer   
                await TokenContractStub.Approve.SendAsync(new ApproveInput()
                {
                    Symbol = ElfSymbol,
                    Amount = InitializeElfAmount,
                    Spender = DropContractAddress
                });
            }

            #endregion
        }
        
        [Fact]
        public async Task CreateDrop_Fail()
        {
            await Initialize();
            await PrepareNftData();
            // invalid start time
            var createInput = new CreateDropInput
            {
                StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddSeconds(-5))
            };
            var createResult = await DropContractStub.CreateDrop.SendWithExceptionAsync(createInput);
            createResult.TransactionResult.Error.ShouldContain("Invalid start time.");
            // invalid expire time
            createInput = new CreateDropInput
            {
                StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddSeconds(5)),
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddSeconds(4)),
                CollectionSymbol = CollectionSymbol,                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           ClaimMax = 1,
                ClaimPrice = new Price()
                {
                    Symbol = ElfSymbol,
                    Amount = -1
                },
                IsBurn = true
            };
            createResult = await DropContractStub.CreateDrop.SendWithExceptionAsync(createInput);
            createResult.TransactionResult.Error.ShouldContain("Invalid expire time.");
            
            createInput = new CreateDropInput
            {
                StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddSeconds(5)),
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddDays(7)),
                CollectionSymbol = ""
            };
            createResult = await DropContractStub.CreateDrop.SendWithExceptionAsync(createInput);
            createResult.TransactionResult.Error.ShouldContain("Invalid collection symbol.");
            
            createInput = new CreateDropInput
            {
                StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddSeconds(5)),
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddDays(7)),
                CollectionSymbol = CollectionSymbol,
                ClaimMax = 1,
                ClaimPrice = new Price()
                {
                    Symbol = ElfSymbol,
                    Amount = -1
                },
                IsBurn = true
            };
            createResult = await DropContractStub.CreateDrop.SendWithExceptionAsync(createInput);
            createResult.TransactionResult.Error.ShouldContain("Invalid claim price.");
            
            createInput = new CreateDropInput
            {
                StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddSeconds(5)),
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddDays(7)),
                CollectionSymbol = "TEST-0",
                ClaimMax = 1,
                ClaimPrice = new Price()
                {
                    Symbol = ElfSymbol,
                    Amount = 0
                },
                IsBurn = true
            };
            createResult = await DropContractStub.CreateDrop.SendWithExceptionAsync(createInput);
            createResult.TransactionResult.Error.ShouldContain("Not exist symbol.");
        }

        [Fact]
        public async Task<Hash> CreateDrop_Success()
        {
            await Initialize();
            await PrepareNftData();
            var createInput = new CreateDropInput
            {
                StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddSeconds(5)),
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddDays(1)),
                ClaimMax = 1,
                ClaimPrice = new Price()
                {
                    Symbol = ElfSymbol,
                    Amount = 0
                },
                IsBurn = true,
                CollectionSymbol = CollectionSymbol
            };
            //create drop
            var createResult = await DropContractStub.CreateDrop.SendAsync(createInput);
            var txId = createResult.TransactionResult.TransactionId;
            var dropId = await DropContractStub.GetDropId.CallAsync(new GetDropIdInput()
            {
                TransactionId = txId,
                Address = DefaultAddress
            });
            
            //log check
            var log = DropCreated.Parser
                .ParseFrom(createResult.TransactionResult.Logs.First(l => l.Name == nameof(DropCreated))
                    .NonIndexed);
            log.DropId.ShouldBe(dropId);
            log.CollectionSymbol.ShouldBe(CollectionSymbol);
            log.ClaimMax.ShouldBe(1);
            log.MaxIndex.ShouldBe(0);
            log.TotalAmount.ShouldBe(0);
            log.CurrentIndex.ShouldBe(0);
            log.ClaimAmount.ShouldBe(0);
            log.Owner.ShouldBe(DefaultAddress);
            log.State.ShouldBe(DropState.Create);
            log.IsBurn.ShouldBe(true);
            
            //get drop info
            var dropInfo = await DropContractStub.GetDropInfo.CallAsync(new GetDropInfoInput()
            {
                DropId = dropId
            });
            dropInfo.ShouldNotBeNull();
            dropInfo.State.ShouldBe(DropState.Create);
            return dropId;
        }

        [Fact]
        public async Task AddDropNFTDetailList_Success()
        {
            var dropId = await CreateDrop_Success();
            var nftList = new DropDetailList();
            nftList.Value.Add(new DropDetailInfo()
            {
                Symbol = NftSymbol1,
                TotalAmount = 10,
                ClaimAmount = 1
            });
            nftList.Value.Add(new DropDetailInfo()
            {
                Symbol = NftSymbol2,
                TotalAmount = 10,
                ClaimAmount = 1
            });
 
            var result = await DropContractStub.AddDropNFTDetailList.SendAsync(new AddDropNFTDetailListInput()
            {
                DropId = dropId,
                Value = { nftList.Value }
            });
            
            //get drop info check
            var dropInfo = await DropContractStub.GetDropInfo.CallAsync(new GetDropInfoInput()
            {
                DropId = dropId
            });
            dropInfo.ShouldNotBeNull();
            dropInfo.State.ShouldBe(DropState.Create);
            dropInfo.MaxIndex.ShouldBe(1);
            dropInfo.CurrentIndex.ShouldBe(0);
            dropInfo.TotalAmount.ShouldBe(20);
            dropInfo.ClaimAmount.ShouldBe(0);
            var dropDetail = await DropContractStub.GetDropDetailList.CallAsync(new GetDropDetailListInput()
            {
                DropId = dropId,
                Index = 1
            });
            
            //check detail
            dropDetail.Value.Count.ShouldBe(2);
            dropDetail.IsFinish.ShouldBe(false);
            dropDetail.Value[0].Symbol.ShouldBe(NftSymbol1);
            dropDetail.Value[0].TotalAmount.ShouldBe(10);
            dropDetail.Value[0].ClaimAmount.ShouldBe(0);
            dropDetail.Value[1].Symbol.ShouldBe(NftSymbol2);
            dropDetail.Value[1].TotalAmount.ShouldBe(10);
            dropDetail.Value[1].ClaimAmount.ShouldBe(0);
           
            //log-event DropChanged
            var logDropChanged = DropCreated.Parser
                .ParseFrom(result.TransactionResult.Logs.First(l => l.Name == nameof(DropChanged))
                    .NonIndexed);
            logDropChanged.DropId.ShouldBe(dropId);
            logDropChanged.MaxIndex.ShouldBe(dropInfo.MaxIndex);
            logDropChanged.CurrentIndex.ShouldBe(dropInfo.CurrentIndex);
            logDropChanged.TotalAmount.ShouldBe(dropInfo.TotalAmount);
            logDropChanged.ClaimAmount.ShouldBe(dropInfo.ClaimAmount);
            
            //log-event DropDetailAdded
            var logDropDetailAdded = DropCreated.Parser
                .ParseFrom(result.TransactionResult.Logs.First(l => l.Name == nameof(DropDetailAdded))
                    .NonIndexed);
            logDropDetailAdded.DropId.ShouldBe(dropId);
            logDropDetailAdded.CollectionSymbol.ShouldBe(CollectionSymbol);
        }
    }
}*/