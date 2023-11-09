using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.Association;
using AElf.Types;
using Forest.Contracts.SymbolRegistrar.Helper;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Forest.Contracts.SymbolRegistrar
{
    public class SymbolRegistrarContractTests_SpecialSeed : SymbolRegistrarContractTests
    {
        private async Task<List<AssociationContractImplContainer.AssociationContractImplStub>> InitListAsync()
        {
            var stubs = new List<AssociationContractImplContainer.AssociationContractImplStub>();
            stubs.Add(User1AssociationContractStub);
            stubs.Add(User2AssociationContractStub);
            stubs.Add(User3AssociationContractStub);
            return stubs;
        }

        private async Task<Dictionary<AElf.Types.Address, List<AssociationContractImplContainer.AssociationContractImplStub>>> InitializeAssociateOrganizationAsync()
        {
            var dictionary = new Dictionary<Address, List<AssociationContractImplContainer.AssociationContractImplStub>>();
            await InitializeContract();
            var associateOrganization = await User1SymbolRegistrarContractStub.GetAssociateOrganization.CallAsync(new Empty());
            var associationContractImplStubs = await InitListAsync();
            dictionary.Add(associateOrganization, associationContractImplStubs);
            return dictionary;
        }

        [Fact]
        private async Task<Dictionary<AElf.Types.Address, List<AssociationContractImplContainer.AssociationContractImplStub>>> SetSpecialSeed_byProposal()
        {
            var dictionary = await InitializeAssociateOrganizationAsync();

            // create proposal and approve
            var result = await SubmitAndApproveProposalOfDefaultAssociation(SymbolRegistrarContractAddress, "AddSpecialSeeds",
                new SpecialSeedList
                {
                    Value = { _specialUsd, _specialEth }
                }, dictionary.First().Value, dictionary.First().Key);
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);


            // logs
            var logEvent = result.TransactionResult.Logs.First(log => log.Name.Contains(nameof(SpecialSeedAdded)));
            var specialSeedAdded = SpecialSeedAdded.Parser.ParseFrom(logEvent.NonIndexed);
            specialSeedAdded.AddList.Value.Count.ShouldBe(2);

            // query seed list and verify
            var seedUsd = await AdminSymbolRegistrarContractStub.GetSpecialSeed.CallAsync(new StringValue
            {
                Value = _specialUsd.Symbol
            });
            seedUsd.Symbol.ShouldBe(_specialUsd.Symbol);


            var seedEth = await AdminSymbolRegistrarContractStub.GetSpecialSeed.CallAsync(new StringValue
            {
                Value = _specialEth.Symbol
            });
            seedEth.Symbol.ShouldBe(_specialEth.Symbol);
            return dictionary;
        }


        [Fact]
        public async Task SetSpecialSeed_notInit_associateOrganization_fail()
        {
            var stubs = new List<AssociationContractImplContainer.AssociationContractImplStub>();
            stubs.Add(User1AssociationContractStub);
            stubs.Add(User2AssociationContractStub);
            stubs.Add(User3AssociationContractStub);
            // create proposal and approve
            var result = await Assert.ThrowsAsync<Exception>(() => SubmitAndApproveProposalOfDefaultAssociation(
                SymbolRegistrarContractAddress,
                "AddSpecialSeeds",
                new SpecialSeedList
                {
                    Value = { _specialUsd, _specialEth }
                }, stubs, Admin.Address));
            result.Message.ShouldContain("No registered organization");
        }


        [Fact]
        public async Task SetSpecialSeed_update_success()
        {
            var dictionary = await InitializeAssociateOrganizationAsync();

            // create proposal and approve
            var result = await SubmitAndApproveProposalOfDefaultAssociation(SymbolRegistrarContractAddress,
                "AddSpecialSeeds", new SpecialSeedList
                {
                    Value = { _specialUsd, _specialEth }
                }, dictionary.First().Value, dictionary.First().Key);
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

            var usd = await User1SymbolRegistrarContractStub.GetSpecialSeed.CallAsync(new StringValue
            {
                Value = _specialUsd.Symbol
            });
            usd.ShouldNotBeNull();
            usd.PriceAmount.ShouldBe(100_0000_0000);
        }


        [Fact]
        public async Task SetSpecialSeed_notExists_removeSuccess()
        {
            var dictionary = await SetSpecialSeed_byProposal();
            var removeResult = await SubmitAndApproveProposalOfDefaultAssociation(SymbolRegistrarContractAddress,
                "RemoveSpecialSeeds", new RemoveSpecialSeedInput
                {
                    Symbols = { _specialBtc.Symbol }
                }, dictionary.First().Value, dictionary.First().Key);
            removeResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            removeResult.TransactionResult.Logs.Where(log => log.Name == nameof(SpecialSeedRemoved)).Count()
                .ShouldBe(0);
        }

        [Fact]
        public async Task SetSpecialSeed_removeSuccess()
        {
            var dictionary = await SetSpecialSeed_byProposal();

            // remove and add
            var addResult = await SubmitAndApproveProposalOfDefaultAssociation(SymbolRegistrarContractAddress,
                "AddSpecialSeeds",
                new SpecialSeedList
                {
                    Value = { _specialBtc }
                }, dictionary.First().Value, dictionary.First().Key);
            addResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

            var removeResult = await SubmitAndApproveProposalOfDefaultAssociation(SymbolRegistrarContractAddress,
                "RemoveSpecialSeeds", new RemoveSpecialSeedInput
                {
                    Symbols = { _specialUsd.Symbol }
                }, dictionary.First().Value, dictionary.First().Key);
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
        public async Task RemoveSpecialSeed_duplicate_success()
        {
            var dictionary = await SetSpecialSeed_byProposal();

            var removeResult = await SubmitAndApproveProposalOfDefaultAssociation(SymbolRegistrarContractAddress,
                "RemoveSpecialSeeds", new RemoveSpecialSeedInput
                {
                    Symbols = { _specialUsd.Symbol, _specialUsd.Symbol }
                }, dictionary.First().Value, dictionary.First().Key);
            removeResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var logEvent =
                removeResult.TransactionResult.Logs.First(log => log.Name.Contains(nameof(SpecialSeedRemoved)));
            var specialSeedRemoved = SpecialSeedRemoved.Parser.ParseFrom(logEvent.NonIndexed);
            specialSeedRemoved.RemoveList.Value.Count.ShouldBe(1);


            var removeResult2 = await SubmitAndApproveProposalOfDefaultAssociation(SymbolRegistrarContractAddress,
                "RemoveSpecialSeeds", new RemoveSpecialSeedInput
                {
                    Symbols = { _specialUsd.Symbol }
                }, dictionary.First().Value, dictionary.First().Key);
            removeResult2.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            removeResult2.TransactionResult.Logs.FirstOrDefault(log => log.Name.Contains(nameof(SpecialSeedRemoved)), null).ShouldBeNull();
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

            adminResult.Message.ShouldContain("No Associate permission.");
        }

        [Fact]
        public async Task SetSpecialSeed_fail()
        {
            var dictionary = await SetSpecialSeed_byProposal();

            // Price symbol not exists
            var notExits = await Assert.ThrowsAsync<Exception>(() =>
                SubmitAndApproveProposalOfDefaultAssociation(SymbolRegistrarContractAddress, "AddSpecialSeeds",
                    new SpecialSeedList
                    {
                        Value = { _specialUsd_errorPrice, _specialEth }
                    }, dictionary.First().Value, dictionary.First().Key)
            );
            notExits.Message.ShouldContain("not exists");

            // Invalid issue chain
            var invalidIssueChain = await Assert.ThrowsAsync<Exception>(() =>
                SubmitAndApproveProposalOfDefaultAssociation(SymbolRegistrarContractAddress, "AddSpecialSeeds",
                    new SpecialSeedList
                    {
                        Value = { _specialUsd, _specialEth_noIssueChainId }
                    }, dictionary.First().Value, dictionary.First().Key)
            );
            invalidIssueChain.Message.ShouldContain("Invalid issue chain");


            // long name
            var invalidSymbolLength = await Assert.ThrowsAsync<Exception>(() =>
                SubmitAndApproveProposalOfDefaultAssociation(SymbolRegistrarContractAddress, "AddSpecialSeeds",
                    new SpecialSeedList
                    {
                        Value = { _specialUsd, _specialLongName }
                    }, dictionary.First().Value, dictionary.First().Key)
            );
            invalidSymbolLength.Message.ShouldContain("Invalid symbol length");

            // invalid NFT symbol
            var invalidPriceAmount = await Assert.ThrowsAsync<Exception>(() =>
                SubmitAndApproveProposalOfDefaultAssociation(SymbolRegistrarContractAddress, "AddSpecialSeeds",
                    new SpecialSeedList
                    {
                        Value = { _specialUsd, _specialInvalidPriceAmount }
                    }, dictionary.First().Value, dictionary.First().Key)
            );
            invalidPriceAmount.Message.ShouldContain("Invalid price amount");


            // Invalid issue chain contract
            var invalidIssueChainContract = await Assert.ThrowsAsync<Exception>(() =>
                SubmitAndApproveProposalOfDefaultAssociation(SymbolRegistrarContractAddress, "AddSpecialSeeds",
                    new SpecialSeedList
                    {
                        Value = { _specialUsd, _specialEth_noIssueChainContractAddress }
                    }, dictionary.First().Value, dictionary.First().Key)
            );
            invalidIssueChainContract.Message.ShouldContain("Invalid issue chain contract");

            // duplicate symbol
            var duplicateSymbol = await Assert.ThrowsAsync<Exception>(() =>
                SubmitAndApproveProposalOfDefaultAssociation(SymbolRegistrarContractAddress, "AddSpecialSeeds",
                    new SpecialSeedList
                    {
                        Value = { _specialUsd, _specialUsd, _specialEth }
                    }, dictionary.First().Value, dictionary.First().Key)
            );
            duplicateSymbol.Message.ShouldContain("Duplicate symbol");
        }

        [Fact]
        public async Task AddSpecialSeed_maxLimitExceeded_fail()
        {
            var dictionary = await SetSpecialSeed_byProposal();

            const int length = 600;
            var batchSpecialSeedList = new SpecialSeedList();
            for (var i = 0; i < length; i++)
            {
                batchSpecialSeedList.Value.Add(SpecialSeed(BaseEncodeHelper.Base26(i), SeedType.Unique, "ELF",
                    100_0000_0000));
            }

            var maxLimitExceeded = await Assert.ThrowsAsync<Exception>(() =>
                SubmitAndApproveProposalOfDefaultAssociation(SymbolRegistrarContractAddress, "AddSpecialSeeds",
                    batchSpecialSeedList, dictionary.First().Value, dictionary.First().Key)
            );
            maxLimitExceeded.Message.ShouldContain("max limit exceeded");
        }

        [Fact]
        public async Task SetUniqueSpecialSeed_update_success()
        {
            var dictionary = await SetSpecialSeed_byProposal();

            // create proposal and approve
            var result = await SubmitAndApproveProposalOfDefaultAssociation(SymbolRegistrarContractAddress,
                "AddUniqueSeeds", new UniqueSeedList()
                {
                    Symbols = { "LUCK" }
                }, dictionary.First().Value, dictionary.First().Key);
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var log = result.TransactionResult.Logs.First(l => l.Name == nameof(SpecialSeedAdded));
            var specialSeedAdded = SpecialSeedAdded.Parser.ParseFrom(log.NonIndexed);
            specialSeedAdded.AddList.Value.Count.ShouldBe(2);
            specialSeedAdded.AddList.Value[0].SeedType.ShouldBe(SeedType.Unique);
            specialSeedAdded.AddList.Value[0].Symbol.ShouldBe("LUCK");
            specialSeedAdded.AddList.Value[0].PriceAmount.ShouldBe(0);
            specialSeedAdded.AddList.Value[0].PriceSymbol.ShouldBeEmpty();
            specialSeedAdded.AddList.Value[1].SeedType.ShouldBe(SeedType.Unique);
            specialSeedAdded.AddList.Value[1].Symbol.ShouldBe("LUCK-0");
            specialSeedAdded.AddList.Value[1].PriceAmount.ShouldBe(0);
            specialSeedAdded.AddList.Value[1].PriceSymbol.ShouldBeEmpty();

            await AdminSymbolRegistrarContractStub.SetSeedsPrice.SendAsync(new SeedsPriceInput
            {
                NftPriceList = MockPriceList(),
                FtPriceList = MockPriceList()
            });
            await AdminSymbolRegistrarContractStub.SetUniqueSeedsExternalPrice.SendAsync(new UniqueSeedsExternalPriceInput()
            {
                NftPriceList = MockPriceList(),
                FtPriceList = MockPriceList()
            });

            var luck = await User1SymbolRegistrarContractStub.GetSpecialSeed.CallAsync(new StringValue
            {
                Value = "LUCK"
            });
            luck.ShouldNotBeNull();
            luck.PriceAmount.ShouldBe(MockPriceList().Value[3].Amount * 2);
            luck.PriceSymbol.ShouldBe(MockPriceList().Value[3].Symbol);

            luck = await User1SymbolRegistrarContractStub.GetSpecialSeed.CallAsync(new StringValue
            {
                Value = "LUCK-0"
            });
            luck.ShouldNotBeNull();
            luck.PriceAmount.ShouldBe(MockPriceList().Value[5].Amount * 2);
            luck.PriceSymbol.ShouldBe(MockPriceList().Value[5].Symbol);
        }

        [Fact]
        public async Task SetUniqueSpecialSeed_fail()
        {
            var dictionary = await SetSpecialSeed_byProposal();

            // long name
            var invalidSymbolLength = await Assert.ThrowsAsync<Exception>(() =>
                SubmitAndApproveProposalOfDefaultAssociation(SymbolRegistrarContractAddress, "AddUniqueSeeds",
                    new UniqueSeedList()
                    {
                        Symbols = { _specialLongName.Symbol }
                    }, dictionary.First().Value, dictionary.First().Key)
            );
            invalidSymbolLength.Message.ShouldContain("Invalid symbol length");

            // invalid symbol
            var invalidSymbol = await Assert.ThrowsAsync<Exception>(() =>
                SubmitAndApproveProposalOfDefaultAssociation(SymbolRegistrarContractAddress, "AddUniqueSeeds",
                    new UniqueSeedList()
                    {
                        Symbols = { _specialInvalidSymbol.Symbol }
                    }, dictionary.First().Value, dictionary.First().Key)
            );
            invalidSymbol.Message.ShouldContain("Invalid symbol");

            // invalid NFT symbol
            var invalidNftSymbol = await Assert.ThrowsAsync<Exception>(() =>
                SubmitAndApproveProposalOfDefaultAssociation(SymbolRegistrarContractAddress, "AddUniqueSeeds",
                    new UniqueSeedList()
                    {
                        Symbols = { _specialInvalidNftSymbol.Symbol }
                    }, dictionary.First().Value, dictionary.First().Key)
            );
            invalidNftSymbol.Message.ShouldContain("Invalid nft symbol");
        }

        [Fact]
        public async Task AddUniqueSpecialSeed_maxLimitExceeded_fail()
        {
            var dictionary = await SetSpecialSeed_byProposal();

            const int length = 600;
            var batchSpecialSeedList = new UniqueSeedList();
            for (var i = 0; i < length; i++)
            {
                batchSpecialSeedList.Symbols.Add(BaseEncodeHelper.Base26(i));
            }

            var maxLimitExceeded = await Assert.ThrowsAsync<Exception>(() =>
                SubmitAndApproveProposalOfDefaultAssociation(SymbolRegistrarContractAddress, "AddUniqueSeeds",
                    batchSpecialSeedList, dictionary.First().Value, dictionary.First().Key)
            );
            maxLimitExceeded.Message.ShouldContain("max limit exceeded");
        }
    }
}