using System;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace Forest.Contracts.SymbolRegistrar
{
    public class SymbolRegistrarContractTests_SeedPrice : SymbolRegistrarContractTests
    {

        [Fact]
        public async Task SetSeedsPrice_fail()
        {
            await InitializeContract();

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


    }
}