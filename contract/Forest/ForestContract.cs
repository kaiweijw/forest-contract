using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Forest
{
    /// <summary>
    /// The C# implementation of the contract defined in forest_contract.proto that is located in the "protobuf"
    /// folder.
    /// Notice that it inherits from the protobuf generated code. 
    /// </summary>
    public partial class ForestContract : ForestContractContainer.ForestContractBase
    {
        public override Empty Initialize(InitializeInput input)
        {
            Assert(State.TokenContract.Value == null, "Already initialized.");
            State.Admin.Value = input.AdminAddress ?? Context.Sender;
            State.ServiceFeeRate.Value = input.ServiceFeeRate == 0 ? DefaultServiceFeeRate : input.ServiceFeeRate;
            State.ServiceFeeReceiver.Value = input.ServiceFeeReceiver ?? State.Admin.Value;
            State.ServiceFee.Value = input.ServiceFee == 0 ? DefaultServiceFeeAmount : input.ServiceFee;
            State.TokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
            State.WhitelistContract.Value = input.WhitelistContractAddress;
            State.GlobalTokenWhiteList.Value = new StringList
            {
                Value = {Context.Variables.NativeSymbol}
            };
            return new Empty();
        }
        
        public override Empty SetAdministrator(Address input)
        {
            AssertSenderIsAdmin();
            Assert(input != null, "Empty Address");
            State.Admin.Value = input;
            return new Empty();
        }

        public override Empty SetServiceFee(SetServiceFeeInput input)
        {
            AssertSenderIsAdmin();
            Assert(input.ServiceFeeRate >= 0, "Invalid ServiceFeeRate");
            State.ServiceFeeRate.Value = input.ServiceFeeRate;
            State.ServiceFeeReceiver.Value = input.ServiceFeeReceiver ?? State.Admin.Value;
            return new Empty();
        }

        public override Empty SetGlobalTokenWhiteList(StringList input)
        {
            AssertSenderIsAdmin();
            if (!input.Value.Contains(Context.Variables.NativeSymbol))
            {
                input.Value.Add(Context.Variables.NativeSymbol);
            }
            foreach (var symbol in input.Value)
            {
                var tokenInfo = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
                {
                    Symbol = symbol
                });
                Assert(tokenInfo?.Symbol?.Length > 0, "Invalid token : " + symbol);
            }
            State.GlobalTokenWhiteList.Value = input;
            Context.Fire(new GlobalTokenWhiteListChanged
            {
                TokenWhiteList = input
            });
            return new Empty();
        }

        public override Empty SetWhitelistContract(Address input)
        {
            AssertSenderIsAdmin();
            Assert(input != null, "Empty contract address");
            State.WhitelistContract.Value = input;
            return new Empty();
        }

        private void AssertWhitelistContractInitialized()
        {
            Assert(State.WhitelistContract.Value != null, "Whitelist Contract not initialized.");
        }

        private void AssertSenderIsAdmin()
        {
            AssertContractInitialized();
            Assert(Context.Sender == State.Admin.Value, "No permission.");
        }

        private void AssertContractInitialized()
        {
            Assert(State.Admin.Value != null, "Contract not initialized.");
        }
    }
}