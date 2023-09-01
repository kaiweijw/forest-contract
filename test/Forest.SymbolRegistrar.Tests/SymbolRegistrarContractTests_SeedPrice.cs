using System;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Forest.SymbolRegistrar
{
    public class SymbolRegistrarContractTests_SeedPrice : SymbolRegistrarContractTests
    {
        
        private static PriceList MockPriceList()
        {
            var priceList = new PriceList();
            for (var i = 0 ; i < 30 ; i ++)
            {
                priceList.Value.Add(new PriceItem
                {
                    SymbolLength = i + 1,
                    Symbol = "ELF",
                    Amount = 50_0000_0000 - i * 1_0000_0000
                });
            }
            return priceList;
        }
        

        [Fact]
        public async Task SetSeedsPrice_success()
        {
            await InitializeContract();
            
            var result = await AdminSaleContractStub.SetSeedsPrice.SendAsync(new SeedsPriceInput
            {
                FtPriceList = MockPriceList(),
                NftPriceList = MockPriceList()
            });
            
            var log = result.TransactionResult.Logs.First(log => log.Name.Contains(nameof(SeedsPriceChanged)));
            var seedsPriceChanged = SeedsPriceChanged.Parser.ParseFrom(log.NonIndexed);
            seedsPriceChanged.NftPriceList.Value.Count.ShouldBe(30);
            seedsPriceChanged.FtPriceList.Value.Count.ShouldBe(30);
            
            var priceList = await AdminSaleContractStub.GetSeedsPrice.CallAsync(new Empty());
            priceList.FtPriceList.Value.Count.ShouldBe(30);
            priceList.NftPriceList.Value.Count.ShouldBe(30);

        }

        [Fact]
        public async Task SetSeedsPrice_fail()
        {
            await InitializeContract();

            // invalid input length
            {
                var priceList = MockPriceList();
                priceList.Value.RemoveAt(0);
                var invalidLength = await Assert.ThrowsAsync<Exception>(() => AdminSaleContractStub.SetSeedsPrice.SendAsync(new SeedsPriceInput
                {
                    FtPriceList = priceList,
                    NftPriceList = MockPriceList()
                }));
                invalidLength.Message.ShouldContain("price list length must be");
            }
            
            
            // invalid symbol-length value
            {
                var priceList = MockPriceList();
                priceList.Value[0].SymbolLength = 50;
                var invalidSymbolLenght = await Assert.ThrowsAsync<Exception>(() => AdminSaleContractStub.SetSeedsPrice.SendAsync(new SeedsPriceInput
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
                var invalidSymbolLenght = await Assert.ThrowsAsync<Exception>(() => AdminSaleContractStub.SetSeedsPrice.SendAsync(new SeedsPriceInput
                {
                    FtPriceList = priceList,
                    NftPriceList = MockPriceList()
                }));
                invalidSymbolLenght.Message.ShouldContain("Duplicate symbolLength");
            }
            
        }


    }
}