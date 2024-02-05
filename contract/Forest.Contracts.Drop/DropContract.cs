using AElf;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Forest.Contracts.Drop
{
    public partial class DropContract : DropContractContainer.DropContractBase
    {
        public override Empty Initialize(InitializeInput input)
        {
            Assert(!State.Initialized.Value, "Already initialized.");
            Assert(input is { MaxDropDetailListCount: > 0, MaxDropDetailIndexCount: > 0 }, "Invalid input.");
            State.GenesisContract.Value = Context.GetZeroSmartContractAddress();
            Assert(State.GenesisContract.GetContractAuthor.Call(Context.Self) == Context.Sender, "No permission.");
            Assert(input.ProxyAccountAddress != null && !input.ProxyAccountAddress.Value.IsNullOrEmpty(), "ProxyAccountContractAddress required.");
            State.ProxyAccountContract.Value = input.ProxyAccountAddress;
            State.TokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
            State.Admin.Value = Context.Sender;
            State.MaxDropDetailListCount.Value = input.MaxDropDetailListCount;
            State.MaxDropDetailIndexCount.Value = input.MaxDropDetailIndexCount;
            State.Initialized.Value = true;
            
            return new Empty();
        }

        public override Empty SetAdmin(Address input)
        {
            AssertInitialized();
            AssertAdmin();
            Assert(input != null && !input.Value.IsNullOrEmpty(), "Invalid input.");

            State.Admin.Value = input;

            return new Empty();
        }

        public override Empty SetMaxDropDetailListCount(Int32Value input)
        {
            AssertInitialized();
            AssertAdmin();
            Assert(input is { Value: > 0 }, "Invalid input.");

            State.MaxDropDetailListCount.Value = input.Value;
            return new Empty();
        }
        
        public override Empty SetMaxDropDetailIndexCount(Int32Value input)
        {
            AssertInitialized();
            AssertAdmin();
            Assert(input is { Value: > 0 }, "Invalid input.");

            State.MaxDropDetailIndexCount.Value = input.Value;
            return new Empty();
        }
        
        public override Empty SetProxyAccountContractAddress(Address input)
        {
            AssertAdmin();
            AssertInitialized();
            Assert(input != null && !input.Value.IsNullOrEmpty(), "Invalid param");
            State.ProxyAccountContract.Value = input;
            return new Empty();
        }

    }
}