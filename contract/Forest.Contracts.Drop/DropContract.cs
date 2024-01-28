using System.Linq;
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

            State.GenesisContract.Value = Context.GetZeroSmartContractAddress();
            //Assert(State.GenesisContract.GetContractAuthor.Call(Context.Self) == Context.Sender, "No permission.");
            State.TokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
            State.Admin.Value = Context.Sender;
            State.MaxDropDetailListCount.Value = input.MaxDropListCount;
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

        public override Empty SetMaxDropDetailListCount(Int32Value input)
        {
            AssertAdmin();
            Assert(input != null && input.Value > 0, "Invalid input.");

            State.MaxDropDetailListCount.Value = input.Value;
            return new Empty();
        }

    }
}