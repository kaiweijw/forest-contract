using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.Types;
using Google.Protobuf;
using Shouldly;
using Xunit;

namespace Forest.Contracts.Inscription;

public partial class InscriptionContractTests
{
    public async Task ACS2_GetResourceInfo_Inscribe_Test()
    {
        await IssueTest_Success();
        var delegations1 = new Dictionary<string, long>
        {
            ["ELF"] = 1000,
        };
        var delegateInfo1 = new DelegateInfo
        {
            ContractAddress = InscriptionContractAddress,
            MethodName = nameof(InscriptionContractStub.Inscribe),
            Delegations =
            {
                delegations1
            },
            IsUnlimitedDelegate = false
        };
        await TokenContractImplStub.SetTransactionFeeDelegateInfos.SendAsync(new SetTransactionFeeDelegateInfosInput
        {
            DelegatorAddress = Accounts[0].Address,
            DelegateInfoList = { delegateInfo1 }
        });
        await TokenContractImplUserStub.SetTransactionFeeDelegateInfos.SendAsync(new SetTransactionFeeDelegateInfosInput
        {
            DelegatorAddress = Accounts[0].Address,
            DelegateInfoList = { delegateInfo1 }
        });
        await TokenContractImplUser2Stub.SetTransactionFeeDelegateInfos.SendAsync(new SetTransactionFeeDelegateInfosInput
        {
            DelegatorAddress = UserAddress,
            DelegateInfoList = { delegateInfo1 }
        });
        await TokenContractImplUser2Stub.SetTransactionFeeDelegateInfos.SendAsync(new SetTransactionFeeDelegateInfosInput
        {
            DelegatorAddress = Accounts[0].Address,
            DelegateInfoList = { delegateInfo1 }
        });
        var transaction = GenerateInscribeTransaction(DefaultAddress, nameof(InscriptionContractStub.Inscribe),
            new InscribedInput
            {
                Tick = "ELFS",
                Amt = 100
            });

        var result = await Acs2BaseStub.GetResourceInfo.CallAsync(transaction);
        result.NonParallelizable.ShouldBeFalse();
        result.WritePaths.Count.ShouldBeGreaterThan(0);
    }
    
    private Transaction GenerateInscribeTransaction(Address from, string method, IMessage input)
    {
        return new Transaction
        {
            From = from,
            To = InscriptionContractAddress,
            MethodName = method,
            Params = ByteString.CopyFrom(input.ToByteArray())
        };
    }
    
}