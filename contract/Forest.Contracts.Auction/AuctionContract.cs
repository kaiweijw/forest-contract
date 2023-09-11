using System.Linq;
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

            Assert(input.Admin == null || !input.Admin.Value.IsNullOrEmpty(), "Invalid input admin.");

            State.Admin.Value = input.Admin ?? Context.Sender;
            State.AuctionController.Value = new ControllerList
            {
                Controllers = { State.Admin.Value }
            };

            if (input.AuctionController.Count > 0)
            {
                State.AuctionController.Value.Controllers.AddRange(input.AuctionController.Distinct().Where(t => t != State.Admin.Value));
            }

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

        public override Empty AddAuctionController(AddAuctionControllerInput input)
        {
            AssertAdmin();
            Assert(input != null && input.Addresses != null, "Invalid input.");
            Assert(input.Addresses.Controllers != null && input.Addresses.Controllers.Count > 0,
                "Invalid input controllers");
            if (State.AuctionController.Value == null)
            {
                State.AuctionController.Value = new ControllerList();
            }

            if (State.AuctionController.Value.Equals(input.Addresses))
            {
                return new Empty();
            }

            var controllerList = State.AuctionController.Value.Clone();
            controllerList.Controllers.AddRange(input.Addresses.Controllers);

            State.AuctionController.Value = new ControllerList
            {
                Controllers = { controllerList.Controllers.Distinct() }
            };

            Context.Fire(new AuctionControllerAdded
            {
                Addresses = State.AuctionController.Value
            });
            return new Empty();
        }

        public override Empty RemoveAuctionController(RemoveAuctionControllerInput input)
        {
            AssertAdmin();
            Assert(input != null && input.Addresses != null, "Invalid input.");
            Assert(input.Addresses.Controllers != null && input.Addresses.Controllers.Count > 0,
                "Invalid input controllers");

            var removeList = input.Addresses.Controllers.Intersect(State.AuctionController.Value.Controllers)
                .ToList();
            if (removeList.Count == 0)
            {
                return new Empty();
            }

            var controllerList = State.AuctionController.Value.Clone();
            foreach (var address in removeList)
            {
                controllerList.Controllers.Remove(address);
            }

            State.AuctionController.Value = new ControllerList
            {
                Controllers = { controllerList.Controllers }
            };

            Context.Fire(new AuctionControllerRemoved
            {
                Addresses = State.AuctionController.Value
            });
            return new Empty();
        }
    }
}