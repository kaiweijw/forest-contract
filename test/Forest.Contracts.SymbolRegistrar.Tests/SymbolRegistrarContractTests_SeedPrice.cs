using System;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Forest.Contracts.SymbolRegistrar
{
    public class SymbolRegistrarContractTests_SeedPrice : SymbolRegistrarContractTests
    {

        [Fact]
        public async Task SetSeedsPrice_emptyList()
        {
            await InitializeContractIfNecessary();
            
            // nft list only
            var nftResult = await AdminSymbolRegistrarContractStub.SetSeedsPrice.SendAsync(new SeedsPriceInput
            {
                NftPriceList = MockPriceList()
            });
            
            var log = nftResult.TransactionResult.Logs.First(log => log.Name.Contains(nameof(SeedsPriceChanged)));
            var seedsPriceChanged = SeedsPriceChanged.Parser.ParseFrom(log.NonIndexed);
            seedsPriceChanged.NftPriceList?.Value?.Count.ShouldBe(30);
            seedsPriceChanged.FtPriceList?.Value?.Count.ShouldBe(0);
            
            // ft list only
            var ftResult = await AdminSymbolRegistrarContractStub.SetSeedsPrice.SendAsync(new SeedsPriceInput
            {
                FtPriceList = MockPriceList()
            });
            
            log = ftResult.TransactionResult.Logs.First(log => log.Name.Contains(nameof(SeedsPriceChanged)));
            seedsPriceChanged = SeedsPriceChanged.Parser.ParseFrom(log.NonIndexed);
            seedsPriceChanged.NftPriceList?.Value?.Count.ShouldBe(0);
            seedsPriceChanged.FtPriceList?.Value?.Count.ShouldBe(30);
            
            // nft/ft list both empty
            var emptyResult = await AdminSymbolRegistrarContractStub.SetSeedsPrice.SendAsync(new SeedsPriceInput
            {
            });
            emptyResult.TransactionResult.Logs.Count(log => log.Name.Contains(nameof(SeedsPriceChanged))).ShouldBe(0);
        }
        
        
        [Fact]
        public async Task SetSeedsPrice_fail()
        {
            await InitializeContract();

            // no permission
            {
                var priceList = MockPriceList();
                priceList.Value.RemoveAt(0);
                var invalidLength = await Assert.ThrowsAsync<Exception>(() => User1SymbolRegistrarContractStub.SetSeedsPrice.SendAsync(new SeedsPriceInput
                {
                    FtPriceList = MockPriceList(),
                    NftPriceList = MockPriceList()
                }));
                invalidLength.Message.ShouldContain("No permission");
            }
            
            // invalid input length
            {
                var priceList = MockPriceList();
                priceList.Value[0].Symbol = "NOTEXISTS";
                var invalidLength = await Assert.ThrowsAsync<Exception>(() => AdminSymbolRegistrarContractStub.SetSeedsPrice.SendAsync(new SeedsPriceInput
                {
                    FtPriceList = priceList,
                    NftPriceList = MockPriceList()
                }));
                invalidLength.Message.ShouldContain("not exists");
            }
                        
            // invalid input length
            {
                var priceList = MockPriceList();
                priceList.Value.RemoveAt(0);
                var invalidLength = await Assert.ThrowsAsync<Exception>(() => AdminSymbolRegistrarContractStub.SetSeedsPrice.SendAsync(new SeedsPriceInput
                {
                    FtPriceList = priceList,
                    NftPriceList = MockPriceList()
                }));
                invalidLength.Message.ShouldContain("price list length must be");
            }
                        
            // invalid amount_price
            {
                var priceList = MockPriceList();
                priceList.Value[0].Amount = -1;
                var invalidLength = await Assert.ThrowsAsync<Exception>(() => AdminSymbolRegistrarContractStub.SetSeedsPrice.SendAsync(new SeedsPriceInput
                {
                    FtPriceList = priceList,
                    NftPriceList = MockPriceList()
                }));
                invalidLength.Message.ShouldContain("Invalid amount");
            }
            
            
            // invalid symbol-length value
            {
                var priceList = MockPriceList();
                priceList.Value[0].SymbolLength = 50;
                var invalidSymbolLenght = await Assert.ThrowsAsync<Exception>(() => AdminSymbolRegistrarContractStub.SetSeedsPrice.SendAsync(new SeedsPriceInput
                {
                    FtPriceList = priceList,
                    NftPriceList = MockPriceList()
                }));
                invalidSymbolLenght.Message.ShouldContain("Invalid symbolLength");
            }
            
            
            // invalid symbol-length value
            {
                var priceList = MockPriceList();
                priceList.Value[0].SymbolLength = 0;
                var invalidSymbolLenght = await Assert.ThrowsAsync<Exception>(() => AdminSymbolRegistrarContractStub.SetSeedsPrice.SendAsync(new SeedsPriceInput
                {
                    FtPriceList = priceList,
                    NftPriceList = MockPriceList()
                }));
                invalidSymbolLenght.Message.ShouldContain("Invalid symbolLength");
            }
            
            // duplicate symbol-length
            {
                var priceList = MockPriceList();
                priceList.Value[0].SymbolLength = 10;
                var invalidSymbolLenght = await Assert.ThrowsAsync<Exception>(() => AdminSymbolRegistrarContractStub.SetSeedsPrice.SendAsync(new SeedsPriceInput
                {
                    FtPriceList = priceList,
                    NftPriceList = MockPriceList()
                }));
                invalidSymbolLenght.Message.ShouldContain("Duplicate symbolLength");
            }
            
        }

        [Fact]
        public async Task SetUniqueSeedsPrice_emptyList()
        {
            await InitializeContractIfNecessary();

            // nft list only
            var uniqueNftResult = await AdminSymbolRegistrarContractStub.SetUniqueSeedsExternalPrice.SendAsync(new UniqueSeedsExternalPriceInput()
            {
                NftPriceList = MockPriceList()
            });
            
            var log = uniqueNftResult.TransactionResult.Logs.First(log => log.Name.Contains(nameof(UniqueSeedsExternalPriceChanged)));
            var uniqueSeedsPriceChanged = UniqueSeedsExternalPriceChanged.Parser.ParseFrom(log.NonIndexed);
            uniqueSeedsPriceChanged.NftPriceList?.Value?.Count.ShouldBe(30);
            uniqueSeedsPriceChanged.FtPriceList?.Value?.Count.ShouldBe(0);
            
            // ft list only
            var uniqueFtResult = await AdminSymbolRegistrarContractStub.SetUniqueSeedsExternalPrice.SendAsync(new UniqueSeedsExternalPriceInput
            {
                FtPriceList = MockPriceList()
            });
            
            log = uniqueFtResult.TransactionResult.Logs.First(log => log.Name.Contains(nameof(UniqueSeedsExternalPriceChanged)));
            uniqueSeedsPriceChanged = UniqueSeedsExternalPriceChanged.Parser.ParseFrom(log.NonIndexed);
            uniqueSeedsPriceChanged.NftPriceList?.Value?.Count.ShouldBe(0);
            uniqueSeedsPriceChanged.FtPriceList?.Value?.Count.ShouldBe(30);
            
            // nft/ft list both empty
            var emptyResult = await AdminSymbolRegistrarContractStub.SetUniqueSeedsExternalPrice.SendAsync(new UniqueSeedsExternalPriceInput()
            {
            });
            emptyResult.TransactionResult.Logs.Count(log => log.Name.Contains(nameof(UniqueSeedsExternalPriceChanged))).ShouldBe(0);
            
        }
        
        
        [Fact]
        public async Task SetUniqueSeedsPrice_fail()
        {
            await InitializeContract();

            // no permission
            {
                var priceList = MockPriceList();
                priceList.Value.RemoveAt(0);
                var invalidLength = await Assert.ThrowsAsync<Exception>(() => User1SymbolRegistrarContractStub.SetUniqueSeedsExternalPrice.SendAsync(new UniqueSeedsExternalPriceInput()
                {
                    FtPriceList = MockPriceList(),
                    NftPriceList = MockPriceList()
                }));
                invalidLength.Message.ShouldContain("No permission");
            }
            
            // invalid input length
            {
                var priceList = MockPriceList();
                priceList.Value[0].Symbol = "NOTEXISTS";
                var invalidLength = await Assert.ThrowsAsync<Exception>(() => AdminSymbolRegistrarContractStub.SetUniqueSeedsExternalPrice.SendAsync(new UniqueSeedsExternalPriceInput
                {
                    FtPriceList = priceList,
                    NftPriceList = MockPriceList()
                }));
                invalidLength.Message.ShouldContain("not exists");
            }
                        
            // invalid input length
            {
                var priceList = MockPriceList();
                priceList.Value.Add(priceList.Value[0]);
                var invalidLength = await Assert.ThrowsAsync<Exception>(() => AdminSymbolRegistrarContractStub.SetUniqueSeedsExternalPrice.SendAsync(new UniqueSeedsExternalPriceInput
                {
                    FtPriceList = priceList,
                    NftPriceList = MockPriceList()
                }));
                invalidLength.Message.ShouldContain("price list length must be less");
            }
                        
            // invalid amount_price
            {
                var priceList = MockPriceList();
                priceList.Value[0].Amount = -1;
                var invalidLength = await Assert.ThrowsAsync<Exception>(() => AdminSymbolRegistrarContractStub.SetUniqueSeedsExternalPrice.SendAsync(new UniqueSeedsExternalPriceInput
                {
                    FtPriceList = priceList,
                    NftPriceList = MockPriceList()
                }));
                invalidLength.Message.ShouldContain("Invalid amount");
            }
            
            
            // invalid symbol-length value
            {
                var priceList = MockPriceList();
                priceList.Value[0].SymbolLength = 50;
                var invalidSymbolLenght = await Assert.ThrowsAsync<Exception>(() => AdminSymbolRegistrarContractStub.SetUniqueSeedsExternalPrice.SendAsync(new UniqueSeedsExternalPriceInput
                {
                    FtPriceList = priceList,
                    NftPriceList = MockPriceList()
                }));
                invalidSymbolLenght.Message.ShouldContain("Invalid symbolLength");
            }
            
            
            // invalid symbol-length value
            {
                var priceList = MockPriceList();
                priceList.Value[0].SymbolLength = 0;
                var invalidSymbolLenght = await Assert.ThrowsAsync<Exception>(() => AdminSymbolRegistrarContractStub.SetUniqueSeedsExternalPrice.SendAsync(new UniqueSeedsExternalPriceInput
                {
                    FtPriceList = priceList,
                    NftPriceList = MockPriceList()
                }));
                invalidSymbolLenght.Message.ShouldContain("Invalid symbolLength");
            }
            
            // duplicate symbol-length
            {
                var priceList = MockPriceList();
                priceList.Value[0].SymbolLength = 10;
                var invalidSymbolLenght = await Assert.ThrowsAsync<Exception>(() => AdminSymbolRegistrarContractStub.SetUniqueSeedsExternalPrice.SendAsync(new UniqueSeedsExternalPriceInput
                {
                    FtPriceList = priceList,
                    NftPriceList = MockPriceList()
                }));
                invalidSymbolLenght.Message.ShouldContain("Duplicate symbolLength");
            }
            
        }
    }
}