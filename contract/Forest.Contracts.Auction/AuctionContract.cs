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
                State.AuctionController.Value.Controllers.AddRange(input.AuctionController.Distinct()
                    .Where(t => t != State.Admin.Value));
            }

            State.TokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);

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

        public override Empty AddAuctionController(AddAuctionControllerInput input)
        {
            AssertInitialized();
            AssertAdmin();
            Assert(input != null && input.Addresses != null, "Invalid input.");
            Assert(input.Addresses.Controllers != null && input.Addresses.Controllers.Count > 0,
                "Invalid input controllers");

            var addList =
                input.Addresses.Controllers.Distinct().Except(State.AuctionController.Value.Controllers).ToList();

            if (addList.Count == 0)
            {
                return new Empty();
            }

            State.AuctionController.Value.Controllers.AddRange(addList);

            Context.Fire(new AuctionControllerAdded
            {
                Addresses = new ControllerList
                {
                    Controllers = { addList }
                }
            });
            return new Empty();
        }

        public override Empty RemoveAuctionController(RemoveAuctionControllerInput input)
        {
            AssertInitialized();
            AssertAdmin();
            Assert(input != null && input.Addresses != null, "Invalid input.");
            Assert(input.Addresses.Controllers != null && input.Addresses.Controllers.Count > 0,
                "Invalid input controllers");

            var removeList = input.Addresses.Controllers.Distinct().Intersect(State.AuctionController.Value.Controllers)
                .ToList();
            if (removeList.Count == 0)
            {
                return new Empty();
            }
            
            foreach (var address in removeList)
            {
                State.AuctionController.Value.Controllers.Remove(address);
            }

            Context.Fire(new AuctionControllerRemoved
            {
                Addresses = new ControllerList
                {
                    Controllers = { removeList }
                }
            });
            return new Empty();
        }
    }
}