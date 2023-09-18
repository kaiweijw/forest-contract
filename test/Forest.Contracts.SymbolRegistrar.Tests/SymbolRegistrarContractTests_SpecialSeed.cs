using System;
using System.Linq;
using System.Threading.Tasks;
using AElf.Types;
using Forest.Contracts.SymbolRegistrar.Helper;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Forest.Contracts.SymbolRegistrar
{
    public class SymbolRegistrarContractTests_SpecialSeed : SymbolRegistrarContractTests
    {
        [Fact]
        public async Task SetSpecialSeed_notInit_notAuthor_fail()
        {
            // create proposal and approve
            var result = await Assert.ThrowsAsync<Exception>(() => SubmitAndApproveProposalOfDefaultParliament(
                SymbolRegistrarContractAddress,
                "AddSpecialSeeds",
                new SpecialSeedList
                {
                    Value = { _specialUsd, _specialEth }
                }));
            result.Message.ShouldContain("No permission");
        }

        [Fact]
        public async Task SetSpecialSeed_update_success()
        {
            await InitializeContract();

            // create proposal and approve
            var result = await SubmitAndApproveProposalOfDefaultParliament(SymbolRegistrarContractAddress,
                "AddSpecialSeeds", new SpecialSeedList
                {
                    Value = { _specialUsd, _specialEth }
                });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

            var usd = await User1SymbolRegistrarContractStub.GetSpecialSeed.CallAsync(new StringValue
            {
                Value = _specialUsd.Symbol
            });
            usd.ShouldNotBeNull();
            usd.PriceAmount.ShouldBe(100_0000_0000);

            _specialUsd.PriceAmount = 200_0000_0000;
            result = await SubmitAndApproveProposalOfDefaultParliament(SymbolRegistrarContractAddress,
                "AddSpecialSeeds", new SpecialSeedList
                {
                    Value = { _specialUsd, _specialEth }
                });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            usd = await User1SymbolRegistrarContractStub.GetSpecialSeed.CallAsync(new StringValue
            {
                Value = _specialUsd.Symbol
            });
            usd.ShouldNotBeNull();
            usd.PriceAmount.ShouldBe(200_0000_0000);
        }

        [Fact]
        public async Task SetSpecialSeed_notParliament_fail()
        {
            await InitializeContract();

            var result = await Assert.ThrowsAsync<Exception>(() =>
                User1SymbolRegistrarContractStub.AddSpecialSeeds.SendAsync(new SpecialSeedList
                {
                    Value = { _specialUsd, _specialEth }
                }));
            result.Message.ShouldContain("No permission");
        }


        [Fact]
        public async Task SetSpecialSeed_notExists_removeSuccess()
        {
            await SetSpecialSeed_byProposal();

            var removeResult = await SubmitAndApproveProposalOfDefaultParliament(SymbolRegistrarContractAddress,
                "RemoveSpecialSeeds", new RemoveSpecialSeedInput
                {
                    Symbols = { _specialBtc.Symbol }
                });
            removeResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            removeResult.TransactionResult.Logs.Where(log => log.Name == nameof(SpecialSeedRemoved)).Count()
                .ShouldBe(0);
        }

        [Fact]
        public async Task SetSpecialSeed_removeSuccess()
        {
            await SetSpecialSeed_byProposal();

            // remove and add
            var addResult = await SubmitAndApproveProposalOfDefaultParliament(SymbolRegistrarContractAddress,
                "AddSpecialSeeds",
                new SpecialSeedList
                {
                    Value = { _specialBtc }
                });
            addResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

            var removeResult = await SubmitAndApproveProposalOfDefaultParliament(SymbolRegistrarContractAddress,
                "RemoveSpecialSeeds", new RemoveSpecialSeedInput
                {
                    Symbols = { _specialUsd.Symbol }
                });
            removeResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

            // logs
            var logEvent =
                removeResult.TransactionResult.Logs.First(log => log.Name.Contains(nameof(SpecialSeedRemoved)));
            var specialSeedRemoved = SpecialSeedRemoved.Parser.ParseFrom(logEvent.NonIndexed);
            specialSeedRemoved.RemoveList.Value.Count.ShouldBe(1);

            // query seed list and verify
            var seedUsd = await AdminSymbolRegistrarContractStub.GetSpecialSeed.CallAsync(new StringValue
            {
                Value = _specialUsd.Symbol
            });
            seedUsd.Symbol.ShouldBe(string.Empty);


            var seedEth = await AdminSymbolRegistrarContractStub.GetSpecialSeed.CallAsync(new StringValue
            {
                Value = _specialEth.Symbol
            });
            seedEth.Symbol.ShouldBe(_specialEth.Symbol);

            var seedBtc = await AdminSymbolRegistrarContractStub.GetSpecialSeed.CallAsync(new StringValue
            {
                Value = _specialBtc.Symbol
            });
            seedBtc.Symbol.ShouldBe(_specialBtc.Symbol);
        }


        [Fact]
        public async Task SetSpecialSeed_remove_notInit_success()
        {
            var removeResult = await AdminSymbolRegistrarContractStub.RemoveSpecialSeeds.SendAsync(
                new RemoveSpecialSeedInput
                {
                    Symbols = { _specialUsd.Symbol }
                });

            removeResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
        }


        [Fact]
        public async Task SetSpecialSeed_remove_noPermission()
        {
            var removeResult = await Assert.ThrowsAsync<Exception>(() =>
                User1SymbolRegistrarContractStub.RemoveSpecialSeeds.SendAsync(
                    new RemoveSpecialSeedInput
                    {
                        Symbols = { _specialUsd.Symbol }
                    }));
            removeResult.Message.ShouldContain("No permission");

            await InitializeContract();
            var adminResult = await Assert.ThrowsAsync<Exception>(() =>
                AdminSymbolRegistrarContractStub.RemoveSpecialSeeds.SendAsync(
                    new RemoveSpecialSeedInput
                    {
                        Symbols = { _specialUsd.Symbol }
                    }));

            adminResult.Message.ShouldContain("No permission");
        }

        [Fact]
        public async Task SetSpecialSeed_fail()
        {
            await InitializeContract();

            // Price symbol not exists
            var notExits = await Assert.ThrowsAsync<Exception>(() =>
                SubmitAndApproveProposalOfDefaultParliament(SymbolRegistrarContractAddress, "AddSpecialSeeds",
                    new SpecialSeedList
                    {
                        Value = { _specialUsd_errorPrice, _specialEth }
                    })
            );
            notExits.Message.ShouldContain("not exists");

            // Invalid issue chain
            var invalidIssueChain = await Assert.ThrowsAsync<Exception>(() =>
                SubmitAndApproveProposalOfDefaultParliament(SymbolRegistrarContractAddress, "AddSpecialSeeds",
                    new SpecialSeedList
                    {
                        Value = { _specialUsd, _specialEth_noIssueChainId }
                    })
            );
            invalidIssueChain.Message.ShouldContain("Invalid issue chain");


            // long name
            var invalidSymbolLength = await Assert.ThrowsAsync<Exception>(() =>
                SubmitAndApproveProposalOfDefaultParliament(SymbolRegistrarContractAddress, "AddSpecialSeeds",
                    new SpecialSeedList
                    {
                        Value = { _specialUsd, _specialLongName }
                    })
            );
            invalidSymbolLength.Message.ShouldContain("Invalid symbol length");

            // invalid symbol
            var invalidSymbol = await Assert.ThrowsAsync<Exception>(() =>
                SubmitAndApproveProposalOfDefaultParliament(SymbolRegistrarContractAddress, "AddSpecialSeeds",
                    new SpecialSeedList
                    {
                        Value = { _specialUsd, _specialInvalidSymbol }
                    })
            );
            invalidSymbol.Message.ShouldContain("Invalid symbol");

            // invalid NFT symbol
            var invalidNftSymbol = await Assert.ThrowsAsync<Exception>(() =>
                SubmitAndApproveProposalOfDefaultParliament(SymbolRegistrarContractAddress, "AddSpecialSeeds",
                    new SpecialSeedList
                    {
                        Value = { _specialUsd, _specialInvalidNftSymbol }
                    })
            );
            invalidNftSymbol.Message.ShouldContain("Invalid nft symbol");


            // invalid NFT symbol
            var invalidPriceAmount = await Assert.ThrowsAsync<Exception>(() =>
                SubmitAndApproveProposalOfDefaultParliament(SymbolRegistrarContractAddress, "AddSpecialSeeds",
                    new SpecialSeedList
                    {
                        Value = { _specialUsd, _specialInvalidPriceAmount }
                    })
            );
            invalidPriceAmount.Message.ShouldContain("Invalid price amount");


            // Invalid issue chain contract
            var invalidIssueChainContract = await Assert.ThrowsAsync<Exception>(() =>
                SubmitAndApproveProposalOfDefaultParliament(SymbolRegistrarContractAddress, "AddSpecialSeeds",
                    new SpecialSeedList
                    {
                        Value = { _specialUsd, _specialEth_noIssueChainContractAddress }
                    })
            );
            invalidIssueChainContract.Message.ShouldContain("Invalid issue chain contract");

            // duplicate symbol
            var duplicateSymbol = await Assert.ThrowsAsync<Exception>(() =>
                SubmitAndApproveProposalOfDefaultParliament(SymbolRegistrarContractAddress, "AddSpecialSeeds",
                    new SpecialSeedList
                    {
                        Value = { _specialUsd, _specialUsd, _specialEth }
                    })
            );
            duplicateSymbol.Message.ShouldContain("Duplicate symbol");
        }

        [Fact]
        public async Task AddSpecialSeed_maxLimitExceeded_fail()
        {
            await InitializeContract();

            const int length = 600;
            var batchSpecialSeedList = new SpecialSeedList();
            for (var i = 0; i < length; i++)
            {
                batchSpecialSeedList.Value.Add(SpecialSeed(BaseEncodeHelper.Base26(i), SeedType.Unique, "ELF",
                    100_0000_0000));
            }

            var maxLimitExceeded = await Assert.ThrowsAsync<Exception>(() =>
                SubmitAndApproveProposalOfDefaultParliament(SymbolRegistrarContractAddress, "AddSpecialSeeds",
                    batchSpecialSeedList)
            );
            maxLimitExceeded.Message.ShouldContain("max limit exceeded");
        }
    }
}