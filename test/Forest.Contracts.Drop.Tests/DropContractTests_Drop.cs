using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.ContractTestBase.ContractTestKit;
using AElf.CSharp.Core.Extension;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json;
using Shouldly;
using Xunit;

namespace Forest.Contracts.Drop
{
    public class DropContractTests_Drop : DropContractTestBase
    {
        private const string CollectionSymbol = "TESTNFT-0";
        private const string NftSymbol1 = "TESTNFT-1";
        private const string NftSymbol2 = "TESTNFT-2";
        private const string NftSymbol3 = "TESTNFT-3";
        private const string ElfSymbol = "ELF";
        private const long InitializeElfAmount = 10000_0000_0000;

        private async Task Initialize()
        {
            var init = new InitializeInput
            {
                MaxDropDetailIndexCount = 10,
                MaxDropDetailListCount = 2,
                ProxyAccountAddress = DefaultAddress
            };
            await DropContractStub.Initialize.SendAsync(init);
        }

        private async Task CreateSeedCollection() 
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

        private async Task CreateSeed(string seed, string forNFTSymbol)
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
                // create NFT via MULTI-TOKEN-CONTRACT
                await TokenContractStub.Create.SendAsync(new CreateInput
                {
                    Symbol = NftSymbol3,
                    TokenName = NftSymbol3,
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

                // issue 10 NFTs to self
                await TokenContractStub.Issue.SendAsync(new IssueInput()
                {
                    Symbol = NftSymbol3,
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
            //claim_max <0
            createInput = new CreateDropInput
            {
                StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddSeconds(5)),
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddDays(7)),
                CollectionSymbol = CollectionSymbol,
                ClaimMax = -1,
                ClaimPrice = new Price()
                {
                    Symbol = ElfSymbol,
                    Amount = 1
                },
                IsBurn = true
            };
            createResult = await DropContractStub.CreateDrop.SendWithExceptionAsync(createInput);
            createResult.TransactionResult.Error.ShouldContain("Invalid claim max.");
            //claim_max = 0
            //claim_max <0
            createInput = new CreateDropInput
            {
                StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddSeconds(5)),
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddDays(7)),
                CollectionSymbol = CollectionSymbol,
                ClaimMax = 0,
                ClaimPrice = new Price()
                {
                    Symbol = ElfSymbol,
                    Amount = 1
                },
                IsBurn = true
            };
            createResult = await DropContractStub.CreateDrop.SendWithExceptionAsync(createInput);
            createResult.TransactionResult.Error.ShouldContain("Invalid claim max.");
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
                StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddSeconds(0)),
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
            dropInfo.MaxIndex.ShouldBe(0);
            dropInfo.ClaimMax.ShouldBe(1);
            dropInfo.ClaimAmount.ShouldBe(0);
            dropInfo.CurrentIndex.ShouldBe(0);
            dropInfo.TotalAmount.ShouldBe(0);
            return dropId;
        }
        
        [Fact]
        public async Task<Hash> CreateDrop_Max10()
        {
            await Initialize();
            await PrepareNftData();
            var createInput = new CreateDropInput
            {
                StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddSeconds(0)),
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddDays(1)),
                ClaimMax = 10,
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
            return dropId;
        }
        [Fact]
        public async Task<Hash> CreateDrop_UnStart()
        {
            await Initialize();
            await PrepareNftData();
            var createInput = new CreateDropInput
            {
                StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddDays(10)),
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddDays(11)),
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
            dropInfo.MaxIndex.ShouldBe(0);
            dropInfo.ClaimMax.ShouldBe(1);
            dropInfo.ClaimAmount.ShouldBe(0);
            dropInfo.CurrentIndex.ShouldBe(0);
            dropInfo.TotalAmount.ShouldBe(0);
            return dropId;
        }
         [Fact]
        public async Task<Hash> CreateDrop_SuccessV2()
        {
            var init = new InitializeInput
            {
                MaxDropDetailIndexCount = 2,
                MaxDropDetailListCount = 1,
                ProxyAccountAddress = DefaultAddress
            };
            await DropContractStub.Initialize.SendAsync(init);
            
            await PrepareNftData();
            var createInput = new CreateDropInput
            {
                StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddSeconds(5)),
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddSeconds(8)),
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
        public async Task AddDropNFTDetailList_ParamCheck()
        {
            var dropId = await CreateDrop_Success();
            var nftList = new DropDetailList();
            nftList.Value.Add(new DropDetailInfo()
            {
                Symbol = "TEST-1",
                TotalAmount = 10,
                ClaimAmount = 1
            });

            var result = await DropContractStub.AddDropNFTDetailList.SendWithExceptionAsync(new AddDropNFTDetailListInput()
            {
                DropId = dropId,
                Value = { nftList.Value }
            });
            result.TransactionResult.Error.ShouldContain("Not exist symbol");
            
            nftList = new DropDetailList();
            nftList.Value.Add(new DropDetailInfo()
            {
                Symbol = NftSymbol1,
                TotalAmount = -1,
                ClaimAmount = 1
            });
            result = await DropContractStub.AddDropNFTDetailList.SendWithExceptionAsync(new AddDropNFTDetailListInput()
            {
                DropId = dropId,
                Value = { nftList.Value }
            });
            result.TransactionResult.Error.ShouldContain("Invalid amount");
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
            var logDropChanged = DropChanged.Parser
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
        
        [Fact]
        public async Task AddDropNFTDetailList_ReachMaxDropDetailListCount()
        {
            var dropId = await CreateDrop_Success();
            var nftList = new DropDetailList();
            nftList.Value.Add(new DropDetailInfo()
            {
                Symbol = NftSymbol1,
                TotalAmount = 10,
                ClaimAmount = 1
            });
            var resultFirstAdd = await DropContractStub.AddDropNFTDetailList.SendAsync(new AddDropNFTDetailListInput()
            {
                DropId = dropId,
                Value = { nftList.Value }
            });
            
            nftList.Value.Add(new DropDetailInfo()
            {
                Symbol = NftSymbol2,
                TotalAmount = 10,
                ClaimAmount = 1
            });
            nftList.Value.Add(new DropDetailInfo()
            {
                Symbol = NftSymbol3,
                TotalAmount = 10,
                ClaimAmount = 1
            });
 
            var resultSecondAdd = await DropContractStub.AddDropNFTDetailList.SendWithExceptionAsync(new AddDropNFTDetailListInput()
            {
                DropId = dropId,
                Value = { nftList.Value }
            });
            resultSecondAdd.TransactionResult.Error.ShouldContain("Invalid detail list.count");
            
            //get drop info check
            var dropInfo = await DropContractStub.GetDropInfo.CallAsync(new GetDropInfoInput()
            {
                DropId = dropId
            });
            dropInfo.ShouldNotBeNull();
            dropInfo.State.ShouldBe(DropState.Create);
            dropInfo.MaxIndex.ShouldBe(1);
            dropInfo.CurrentIndex.ShouldBe(0);
            dropInfo.TotalAmount.ShouldBe(10);
            dropInfo.ClaimAmount.ShouldBe(0);
            var dropDetail = await DropContractStub.GetDropDetailList.CallAsync(new GetDropDetailListInput()
            {
                DropId = dropId,
                Index = 1
            });
            
            //check detail
            dropDetail.Value.Count.ShouldBe(1);
            dropDetail.IsFinish.ShouldBe(false);
            dropDetail.Value[0].Symbol.ShouldBe(NftSymbol1);
            dropDetail.Value[0].TotalAmount.ShouldBe(10);
            dropDetail.Value[0].ClaimAmount.ShouldBe(0);

            //log-event DropChanged
            var logDropChanged = DropChanged.Parser
                .ParseFrom(resultFirstAdd.TransactionResult.Logs.First(l => l.Name == nameof(DropChanged))
                    .NonIndexed);
            logDropChanged.DropId.ShouldBe(dropId);
            logDropChanged.MaxIndex.ShouldBe(dropInfo.MaxIndex);
            logDropChanged.CurrentIndex.ShouldBe(dropInfo.CurrentIndex);
            logDropChanged.TotalAmount.ShouldBe(dropInfo.TotalAmount);
            logDropChanged.ClaimAmount.ShouldBe(dropInfo.ClaimAmount);
            
            //log-event DropDetailAdded
            var logDropDetailAdded = DropCreated.Parser
                .ParseFrom(resultFirstAdd.TransactionResult.Logs.First(l => l.Name == nameof(DropDetailAdded))
                    .NonIndexed);
            logDropDetailAdded.DropId.ShouldBe(dropId);
            logDropDetailAdded.CollectionSymbol.ShouldBe(CollectionSymbol);
        }
        
        [Fact]
        public async Task<Hash> AddDropNFTDetailList_Batch()
        {
            var dropId = await CreateDrop_SuccessV2();
            var nftList = new DropDetailList();
            //add 1
            nftList.Value.Add(new DropDetailInfo()
            {
                Symbol = NftSymbol1,
                TotalAmount = 10,
                ClaimAmount = 1
            });
            await DropContractStub.AddDropNFTDetailList.SendAsync(new AddDropNFTDetailListInput()
            {
                DropId = dropId,
                Value = { nftList.Value }
            });
            var dropDetail1 = await DropContractStub.GetDropDetailList.CallAsync(new GetDropDetailListInput()
            {
                DropId = dropId,
                Index = 1
            });
            
            //check detail
            dropDetail1.Value.Count.ShouldBe(1);
            dropDetail1.IsFinish.ShouldBe(false);
            dropDetail1.Value[0].Symbol.ShouldBe(NftSymbol1);
            dropDetail1.Value[0].TotalAmount.ShouldBe(10);
            dropDetail1.Value[0].ClaimAmount.ShouldBe(0);
            
            //add 2
            var nftList2 = new DropDetailList();
            nftList2.Value.Add(new DropDetailInfo()
            {
                Symbol = NftSymbol2,
                TotalAmount = 10,
                ClaimAmount = 1
            });
            await DropContractStub.AddDropNFTDetailList.SendAsync(new AddDropNFTDetailListInput()
            {
                DropId = dropId,
                Value = { nftList2.Value }
            });
            var dropDetail2 = await DropContractStub.GetDropDetailList.CallAsync(new GetDropDetailListInput()
            {
                DropId = dropId,
                Index = 2
            });
            
            //check detail
            dropDetail2.Value.Count.ShouldBe(1);
            dropDetail2.IsFinish.ShouldBe(false);
            dropDetail2.Value[0].Symbol.ShouldBe(NftSymbol2);
            dropDetail2.Value[0].TotalAmount.ShouldBe(10);
            dropDetail2.Value[0].ClaimAmount.ShouldBe(0);
            //add 3
            nftList2.Value.Add(new DropDetailInfo()
            {
                Symbol = NftSymbol3,
                TotalAmount = 10,
                ClaimAmount = 1
            });
            var resultAdd2 = await DropContractStub.AddDropNFTDetailList.SendWithExceptionAsync(new AddDropNFTDetailListInput()
            {
                DropId = dropId,
                Value = { nftList2.Value }
            });
            resultAdd2.TransactionResult.Error.ShouldContain("Invalid detail list");
            //add 4
            var nftList4 = new DropDetailList();
            nftList4.Value.Add(new DropDetailInfo()
            {
                Symbol = NftSymbol3,
                TotalAmount = 10,
                ClaimAmount = 1
            });
            var resultAdd4 = await DropContractStub.AddDropNFTDetailList.SendWithExceptionAsync(new AddDropNFTDetailListInput()
            {
                DropId = dropId,
                Value = { nftList4.Value }
            });
            resultAdd4.TransactionResult.Error.ShouldContain("Invalid total amount");
            //get drop info
            var dropInfo = await DropContractStub.GetDropInfo.CallAsync(new GetDropInfoInput()
            {
                DropId = dropId
            });
            dropInfo.ShouldNotBeNull();
            dropInfo.State.ShouldBe(DropState.Create);
            dropInfo.MaxIndex.ShouldBe(2);
            dropInfo.TotalAmount.ShouldBe(20);
            return dropId;
        }

        [Fact]
        public async Task DropSubmit_Exist()
        {
            var dropId = await CreateDrop_SuccessV2();
            //add nft
            var nftList = new DropDetailList();
            nftList.Value.Add(new DropDetailInfo()
            {
                Symbol = NftSymbol1,
                TotalAmount = 10,
                ClaimAmount = 1
            });
            var resultFirstAdd = await DropContractStub.AddDropNFTDetailList.SendAsync(new AddDropNFTDetailListInput()
            {
                DropId = dropId,
                Value = { nftList.Value }
            });
            
            //submit 1
            var result = await DropContractStub.SubmitDrop.SendAsync(dropId);
            //get drop info
            var dropInfo = await DropContractStub.GetDropInfo.CallAsync(new GetDropInfoInput()
            {
                DropId = dropId
            });
            dropInfo.ShouldNotBeNull();
            dropInfo.State.ShouldBe(DropState.Submit);
            dropInfo.MaxIndex.ShouldBe(1);
            dropInfo.TotalAmount.ShouldBe(10);
            dropInfo.ExpireTime.AddHours(8).ShouldBeGreaterThan(DateTime.UtcNow.ToTimestamp());
            //submit 2
            var dropInfoException = await DropContractStub.SubmitDrop.SendWithExceptionAsync(dropId);
            dropInfoException.TransactionResult.Error.ShouldContain("Invalid drop state.");
            //expire
            Thread.Sleep(11000);
            dropInfo.ExpireTime.ShouldBeLessThanOrEqualTo(DateTime.UtcNow.ToTimestamp());
        }
        
        [Fact]
        public async Task DropSubmit_NotExist()
        {
            var dropId = await CreateDrop_SuccessV2();
            //add nft
            var nftList = new DropDetailList();
            nftList.Value.Add(new DropDetailInfo()
            {
                Symbol = NftSymbol1,
                TotalAmount = 10,
                ClaimAmount = 1
            });
            await DropContractStub.AddDropNFTDetailList.SendAsync(new AddDropNFTDetailListInput()
            {
                DropId = dropId,//HashHelper.ComputeFrom("test"),
                Value = { nftList.Value }
            });
            //submit 1
            var result = await DropContractStub.SubmitDrop.SendWithExceptionAsync(HashHelper.ComputeFrom("test"));
            result.TransactionResult.Error.ShouldContain("Invalid drop id.");
            //submit 2
            result = await DropContractUserStub.SubmitDrop.SendWithExceptionAsync(dropId);
            result.TransactionResult.Error.ShouldContain("Only owner can submit drop.");
            //submit 3
            result = await DropContractStub.SubmitDrop.SendAsync(dropId);
            //get drop info
            var dropInfo = await DropContractStub.GetDropInfo.CallAsync(new GetDropInfoInput()
            {
                DropId = dropId
            });
            dropInfo.ShouldNotBeNull();
            dropInfo.State.ShouldBe(DropState.Submit);
            //cancel
            await DropContractStub.CancelDrop.SendAsync(dropId);
            //submit 4
            result = await DropContractStub.SubmitDrop.SendWithExceptionAsync(dropId);
            result.TransactionResult.Error.ShouldContain("Invalid drop state");
        }
        
        [Fact]
        public async Task DropCancel_Submit()
        {
            var dropId = await CreateDrop_SuccessV2();
            //add nft
            var nftList = new DropDetailList();
            nftList.Value.Add(new DropDetailInfo()
            {
                Symbol = NftSymbol1,
                TotalAmount = 10,
                ClaimAmount = 1
            });
            await DropContractStub.AddDropNFTDetailList.SendAsync(new AddDropNFTDetailListInput()
            {
                DropId = dropId,//HashHelper.ComputeFrom("test"),
                Value = { nftList.Value }
            });
            //submit 
            await DropContractStub.SubmitDrop.SendAsync(dropId);
            //get drop info
            var dropInfo = await DropContractStub.GetDropInfo.CallAsync(new GetDropInfoInput()
            {
                DropId = dropId
            });
            dropInfo.ShouldNotBeNull();
            dropInfo.State.ShouldBe(DropState.Submit);
            //cancel 1
            var cancelResult = await DropContractStub.CancelDrop.SendAsync(dropId);
            //get drop info
            dropInfo = await DropContractStub.GetDropInfo.CallAsync(new GetDropInfoInput()
            {
                DropId = dropId
            });
            dropInfo.ShouldNotBeNull();
            dropInfo.State.ShouldBe(DropState.Cancel);
            
            //log check
            var log = DropStateChanged.Parser
                .ParseFrom(cancelResult.TransactionResult.Logs.First(l => l.Name == nameof(DropStateChanged))
                    .NonIndexed);
            log.DropId.ShouldBe(dropId); 
            log.State.ShouldBe(DropState.Cancel);
            
            //cancel 2
            cancelResult = await DropContractStub.CancelDrop.SendWithExceptionAsync(dropId);
            cancelResult.TransactionResult.Error.ShouldContain("Invalid drop state");
        }
        [Fact]
        public async Task DropCancel_NotExist()
        {
            var dropId = await CreateDrop_SuccessV2();
            //add nft
            var nftList = new DropDetailList();
            nftList.Value.Add(new DropDetailInfo()
            {
                Symbol = NftSymbol1,
                TotalAmount = 10,
                ClaimAmount = 1
            });
            await DropContractStub.AddDropNFTDetailList.SendAsync(new AddDropNFTDetailListInput()
            {
                DropId = dropId,//HashHelper.ComputeFrom("test"),
                Value = { nftList.Value }
            });
            //submit 
            await DropContractStub.SubmitDrop.SendAsync(dropId);
            //get drop info
            var dropInfo = await DropContractStub.GetDropInfo.CallAsync(new GetDropInfoInput()
            {
                DropId = dropId
            });
            dropInfo.ShouldNotBeNull();
            dropInfo.State.ShouldBe(DropState.Submit);
            //cancel 1
            var cancelResult = await DropContractStub.CancelDrop.SendWithExceptionAsync(HashHelper.ComputeFrom("testdropId"));
            cancelResult.TransactionResult.Error.ShouldContain("Invalid drop id");

            //cancel 2
            await DropContractStub.SetAdmin.SendAsync(User2Address);
            cancelResult = await DropContractUserStub.CancelDrop.SendWithExceptionAsync(dropId);
            cancelResult.TransactionResult.Error.ShouldContain("Only owner can submit drop");
            
            //cancel 3
            cancelResult = await DropContractStub.CancelDrop.SendAsync(dropId);
            //get drop info
            dropInfo = await DropContractStub.GetDropInfo.CallAsync(new GetDropInfoInput()
            {
                DropId = dropId
            });
            dropInfo.ShouldNotBeNull();
            dropInfo.State.ShouldBe(DropState.Cancel);
            //cancel 4
            cancelResult = await DropContractStub.CancelDrop.SendWithExceptionAsync(dropId);
            cancelResult.TransactionResult.Error.ShouldContain("Invalid drop state.");
        }
        
        [Fact]
        public async Task DropCancel_Create()
        {
            var dropId = await CreateDrop_SuccessV2();
            //get drop info
            var dropInfo = await DropContractStub.GetDropInfo.CallAsync(new GetDropInfoInput()
            {
                DropId = dropId
            });
            dropInfo.ShouldNotBeNull();
            dropInfo.State.ShouldBe(DropState.Create);
            //cancel 1
            var cancelResult = await DropContractStub.CancelDrop.SendAsync(dropId);
            //get drop info
            dropInfo = await DropContractStub.GetDropInfo.CallAsync(new GetDropInfoInput()
            {
                DropId = dropId
            });
            dropInfo.ShouldNotBeNull();
            dropInfo.State.ShouldBe(DropState.Cancel);
            
            //log check
            var log = DropStateChanged.Parser
                .ParseFrom(cancelResult.TransactionResult.Logs.First(l => l.Name == nameof(DropStateChanged))
                    .NonIndexed);
            log.DropId.ShouldBe(dropId); 
            log.State.ShouldBe(DropState.Cancel);
            
            //cancel 2
            cancelResult = await DropContractStub.CancelDrop.SendWithExceptionAsync(dropId);
            cancelResult.TransactionResult.Error.ShouldContain("Invalid drop state");
        }

        [Fact]
        public async Task GetDropInfo_NotExist()
        {
            var dropId = await CreateDrop_Success();
            //get drop info
            var dropInfo = await DropContractStub.GetDropInfo.CallAsync(new GetDropInfoInput()
            {
                DropId = HashHelper.ComputeFrom("test")
            });
            dropInfo.ShouldBe(new DropInfo());
        }

        [Fact]
        public async Task Claim_UnSubmit()
        {
            var dropId = await CreateDrop_Success();
            //get drop info
            var dropInfo = await DropContractStub.GetDropInfo.CallAsync(new GetDropInfoInput()
            {
                DropId = dropId
            });
            dropInfo.State.ShouldBe(DropState.Create);
            
            //claim 1
            var claimResult = await DropContractStub.ClaimDrop.SendWithExceptionAsync(new ClaimDropInput()
            {
                DropId = dropId,
                ClaimAmount = 1
            });
            claimResult.TransactionResult.Error.ShouldContain("Invalid drop state");
            
            //submit
            var nftList = new DropDetailList();
            nftList.Value.Add(new DropDetailInfo()
            {
                Symbol = NftSymbol1,
                TotalAmount = 10,
                ClaimAmount = 1
            });
            await DropContractStub.AddDropNFTDetailList.SendAsync(new AddDropNFTDetailListInput()
            {
                DropId = dropId,//HashHelper.ComputeFrom("test"),
                Value = { nftList.Value }
            });
            await DropContractStub.SubmitDrop.SendAsync(dropId);
            
            //cancel
            await DropContractStub.CancelDrop.SendAsync(dropId);
            
            //claim2
            claimResult = await DropContractStub.ClaimDrop.SendWithExceptionAsync(new ClaimDropInput()
            {
                DropId = dropId,
                ClaimAmount = 1
            });
            claimResult.TransactionResult.Error.ShouldContain("The event has ended");
        }

        [Fact]
        public async Task Claim_UnStart()
        {
            var dropId = await CreateDrop_UnStart();
            //add nft
            var nftList = new DropDetailList();
            nftList.Value.Add(new DropDetailInfo()
            {
                Symbol = NftSymbol1,
                TotalAmount = 10,
                ClaimAmount = 1
            });
            await DropContractStub.AddDropNFTDetailList.SendAsync(new AddDropNFTDetailListInput()
            {
                DropId = dropId,//HashHelper.ComputeFrom("test"),
                Value = { nftList.Value }
            });
            
            //submit 
            await DropContractStub.SubmitDrop.SendAsync(dropId);
            
            //get drop info
            var dropInfo = await DropContractStub.GetDropInfo.CallAsync(new GetDropInfoInput()
            {
                DropId = dropId
            });
            dropInfo.State.ShouldBe(DropState.Submit);
            
            //claim 1
            var claimResult = await DropContractStub.ClaimDrop.SendWithExceptionAsync(new ClaimDropInput()
            {
                DropId = HashHelper.ComputeFrom("test"),
                ClaimAmount = 1
            });
            claimResult.TransactionResult.Error.ShouldContain("Invalid drop id");
            
            //claim 2
            claimResult = await DropContractStub.ClaimDrop.SendWithExceptionAsync(new ClaimDropInput()
            {
                DropId = dropId,
                ClaimAmount = 0
            });
            claimResult.TransactionResult.Error.ShouldContain("Invalid input");
            
            //claim 3
            claimResult = await DropContractStub.ClaimDrop.SendWithExceptionAsync(new ClaimDropInput()
            {
                DropId = dropId,
                ClaimAmount = 1
            });
            claimResult.TransactionResult.Error.ShouldContain("The drop has not started");
        }
        
        [Fact]
        public async Task Claim_OutNumber()
        {
            var dropId = await CreateDrop_Success();
            //add nft
            var nftList = new DropDetailList();
            nftList.Value.Add(new DropDetailInfo()
            {
                Symbol = NftSymbol1,
                TotalAmount = 10,
                ClaimAmount = 0
            });
            await DropContractStub.AddDropNFTDetailList.SendAsync(new AddDropNFTDetailListInput()
            {
                DropId = dropId,//HashHelper.ComputeFrom("test"),
                Value = { nftList.Value }
            });
            
            //submit 
            await DropContractStub.SubmitDrop.SendAsync(dropId);
            
            //get drop info
            var dropInfo = await DropContractStub.GetDropInfo.CallAsync(new GetDropInfoInput()
            {
                DropId = dropId
            });
            dropInfo.State.ShouldBe(DropState.Submit);
            
            //claim 1
            var currentBlockTime = BlockTimeProvider.GetBlockTime();
            BlockTimeProvider.SetBlockTime(currentBlockTime.AddSeconds(30));
            var claimResult = await DropContractUserStub.ClaimDrop.SendWithExceptionAsync(new ClaimDropInput()
            {
                DropId = dropId,
                ClaimAmount = 10
            });
            claimResult.TransactionResult.Error.ShouldContain("Claimed exceed max amount");
        }
        
        [Fact]
        public async Task Claim_Submit()
        {
            var dropId = await CreateDrop_Success();
            //get drop info
            var dropInfo = await DropContractStub.GetDropInfo.CallAsync(new GetDropInfoInput()
            {
                DropId = dropId
            });
            dropInfo.State.ShouldBe(DropState.Create);
            
            //submit
            var nftList = new DropDetailList();
            nftList.Value.Add(new DropDetailInfo()
            {
                Symbol = NftSymbol1,
                TotalAmount = 10,
                ClaimAmount = 1
            });
            await DropContractStub.AddDropNFTDetailList.SendAsync(new AddDropNFTDetailListInput()
            {
                DropId = dropId,//HashHelper.ComputeFrom("test"),
                Value = { nftList.Value }
            });
            await DropContractStub.SubmitDrop.SendAsync(dropId);
        }
        [Fact]
        public async Task GetDropSymbolExist_Test()
        {
            var dropId = await CreateDrop_Success();
            //get drop info
            var dropInfo = await DropContractStub.GetDropInfo.CallAsync(new GetDropInfoInput()
            {
                DropId = dropId
            });
            dropInfo.State.ShouldBe(DropState.Create);
            
            //get
            var result = DropContractStub.GetDropSymbolExist.CallAsync(new GetDropSymbolExistInput()
            {
                DropId = dropId,
                Symbol = NftSymbol1
            }).Result;
            result.Value.ShouldBe(0);
            
            //submit
            var nftList = new DropDetailList();
            nftList.Value.Add(new DropDetailInfo()
            {
                Symbol = NftSymbol1,
                TotalAmount = 10,
                ClaimAmount = 1
            });
            await DropContractStub.AddDropNFTDetailList.SendAsync(new AddDropNFTDetailListInput()
            {
                DropId = dropId,//HashHelper.ComputeFrom("test"),
                Value = { nftList.Value }
            });
         
            result = DropContractStub.GetDropSymbolExist.CallAsync(new GetDropSymbolExistInput()
            {
                DropId = dropId,
                Symbol = NftSymbol1
            }).Result;
            result.Value.ShouldBeGreaterThan(0);
            
            var result2 = DropContractStub.GetDropSymbolExist.CallAsync(new GetDropSymbolExistInput()
            {
                DropId = dropId,
                Symbol = NftSymbol2
            }).Result;
            result2.Value.ShouldBe(0);
        }
        [Fact]
        public async Task Claim_PartMint()
        {
            var dropId = await CreateDrop_Max10();
            //get drop info
            var dropInfo = await DropContractStub.GetDropInfo.CallAsync(new GetDropInfoInput()
            {
                DropId = dropId
            });

            //submit
            var nftList = new DropDetailList();
            nftList.Value.Add(new DropDetailInfo()
            {
                Symbol = NftSymbol1,
                TotalAmount = 10
            });
            await DropContractStub.AddDropNFTDetailList.SendAsync(new AddDropNFTDetailListInput()
            {
                DropId = dropId,
                Value = { nftList.Value }
            });
            await DropContractStub.SubmitDrop.SendAsync(dropId);
            
            //get drop info
            dropInfo = await DropContractStub.GetDropInfo.CallAsync(new GetDropInfoInput()
            {
                DropId = dropId
            });
            dropInfo.State.ShouldBe(DropState.Submit);
            
            //claim 1
            var currentBlockTime = BlockTimeProvider.GetBlockTime();
            BlockTimeProvider.SetBlockTime(currentBlockTime.AddSeconds(30));
            await DropContractUserStub.ClaimDrop.SendAsync(new ClaimDropInput()
            {
                DropId = dropId,
                ClaimAmount = 8
            });
            var claimResult = await DropContractStub.GetClaimDropInfo.CallAsync(new GetClaimDropInfoInput()
            {
                DropId = dropId,
                Address = UserAddress
            });
            claimResult.Amount.ShouldBe(8);
            
            //claim 2
            await DropContractUser2Stub.ClaimDrop.SendAsync(new ClaimDropInput()
            {
                DropId = dropId,
                ClaimAmount = 10
            });
            claimResult = await DropContractStub.GetClaimDropInfo.CallAsync(new GetClaimDropInfoInput()
            {
                DropId = dropId,
                Address = User2Address
            });
            claimResult.Amount.ShouldBe(2);
        }

        [Fact]
        public async Task Dto_Get()
        {
            var dropInfo = new DropInfo()
            {
                ClaimMax = 10
            };
            Console.WriteLine(JsonConvert.SerializeObject(dropInfo));
            var initInput = new InitializeInput();
            Console.WriteLine(JsonConvert.SerializeObject(initInput));
            var createDropInput = new CreateDropInput();
            Console.WriteLine(JsonConvert.SerializeObject(createDropInput));
            var price = new Price();
            Console.WriteLine(JsonConvert.SerializeObject(price));
            var finishDropInput = new FinishDropInput();
            Console.WriteLine(JsonConvert.SerializeObject(finishDropInput));
            var claimDropInput = new ClaimDropInput();
            Console.WriteLine(JsonConvert.SerializeObject(claimDropInput));
            var getClaimDropInfoInput = new GetClaimDropInfoInput();
            Console.WriteLine(JsonConvert.SerializeObject(getClaimDropInfoInput));
            var getDropInfoInput = new GetDropInfoInput();
            Console.WriteLine(JsonConvert.SerializeObject(getDropInfoInput));
            var claimDetailRecord = new ClaimDetailRecord();
            Console.WriteLine(JsonConvert.SerializeObject(claimDetailRecord));
            var getDropSymbolExistInput = new GetDropSymbolExistInput();
            Console.WriteLine(JsonConvert.SerializeObject(getDropSymbolExistInput));
            var getDropIdListInput = new GetDropIdInput();
            Console.WriteLine(JsonConvert.SerializeObject(getDropIdListInput));
            var getDropListInput = new GetDropDetailListInput();
            Console.WriteLine(JsonConvert.SerializeObject(getDropListInput));
            var dropCreated = new DropCreated();
            Console.WriteLine(JsonConvert.SerializeObject(dropCreated));
            var dropChanged = new DropChanged();
            Console.WriteLine(JsonConvert.SerializeObject(dropChanged));
            var dropStateChange = new DropStateChanged();
            Console.WriteLine(JsonConvert.SerializeObject(dropStateChange));
            var dropClaimAdded = new DropClaimAdded();
            Console.WriteLine(JsonConvert.SerializeObject(dropClaimAdded));
            var dropDetailAdded = new DropDetailAdded();
            Console.WriteLine(JsonConvert.SerializeObject(dropDetailAdded));
            var dropDetailChanged = new DropDetailChanged();
            Console.WriteLine(JsonConvert.SerializeObject(dropDetailChanged));
        }

    }
}