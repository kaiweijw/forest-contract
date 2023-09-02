using AElf;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Forest.Contracts.Auction
{
    public partial class AuctionContract : AuctionContractContainer.AuctionContractBase
    {
        public override Empty Initialize(InitializeInput input)
        {
            Assert(!State.Initialized.Value, "Already initialized.");
            
            State.GenesisContract.Value = Context.GetZeroSmartContractAddress();
            Assert(State.GenesisContract.GetContractAuthor.Call(Context.Self) == Context.Sender, "No permission.");
        
            Assert(!input.Admin.Value.IsNullOrEmpty(), "Invalid input admin.");
            State.Admin.Value = input.Admin ?? Context.Sender;
            
            State.TokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
        
            State.Initialized.Value = true;
            
            return new Empty();
        }

        public override Empty SetAdmin(Address input)
        {
            AssertAdmin();
            Assert(input != null && !input.Value.IsNullOrEmpty(), "Invalid input.");

            State.Admin.Value = input;

            return new Empty();
        }
    }
}