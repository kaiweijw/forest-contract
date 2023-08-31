using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Forest.Contracts.Auction
{
    public partial class AuctionContract : AuctionContractContainer.AuctionContractBase
    {
        public override Empty Initialize(Empty input)
        {
            return base.Initialize(input);
        }

        public override Empty SetAdmin(Address input)
        {
            return base.SetAdmin(input);
        }

        public override Empty SetPaymentReceiverAddress(Address input)
        {
            return base.SetPaymentReceiverAddress(input);
        }
    }
}