using System;
using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Forest.Contracts.Drop;

public partial class DropContract
{
    public override Empty CreateDrop(CreateDropInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(input.StartTime >= Context.CurrentBlockTime, "Invalid start time.");
        Assert(input.ExpireTime > input.StartTime, "Invalid expire time.");
        Assert(!string.IsNullOrWhiteSpace(input.CollectionSymbol), "Invalid collection symbol.");
        Assert(input.ClaimMax > 0, "Invalid claim max.");
        Assert(input.ClaimPrice != null && input.ClaimPrice.Amount >= 0, "Invalid claim price.");
        
        AssertSymbolExist(input.CollectionSymbol, SymbolType.NftCollection);
        
        var dropId = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(Context.OriginTransactionId), HashHelper.ComputeFrom(Context.Sender));
        var dropInfo = new DropInfo
        {
            StartTime = input.StartTime,
            ExpireTime = input.ExpireTime,
            ClaimMax = input.ClaimMax,
            ClaimPrice = input.ClaimPrice,
            MaxIndex = 0,
            CurrentIndex = 0,
            TotalAmount = 0,
            ClaimAmount = 0,
            Owner = Context.Sender,
            IsBurn = input.IsBurn,
            State = DropState.Create,
            CollectionSymbol = input.CollectionSymbol,
            CreateTime = Context.CurrentBlockTime,
            UpdateTime = Context.CurrentBlockTime
        };
        State.DropInfoMap[dropId] = dropInfo;
        
        Context.Fire(new DropCreated
        {
            DropId = dropId,
            CollectionSymbol = dropInfo.CollectionSymbol,
            StartTime = dropInfo.StartTime,
            ExpireTime = dropInfo.ExpireTime,
            ClaimMax = dropInfo.ClaimMax,
            ClaimPrice = dropInfo.ClaimPrice,
            MaxIndex = dropInfo.MaxIndex,
            CurrentIndex = dropInfo.CurrentIndex,
            TotalAmount = dropInfo.TotalAmount,
            ClaimAmount = dropInfo.ClaimAmount,
            Owner = dropInfo.Owner,
            IsBurn = dropInfo.IsBurn,
            State = dropInfo.State,
            CreateTime = dropInfo.CreateTime,
            UpdateTime = dropInfo.UpdateTime
        });

        return new Empty();
    }

    public override Empty AddDropNFTDetailList(AddDropNFTDetailListInput input)
    {
        Assert(input != null, "Invalid input.");
        var inputDropDetailList = new DropDetailList();
        inputDropDetailList.Value.AddRange(input.Value.Distinct());
        
        var dropInfo = State.DropInfoMap[input.DropId];
        Assert(dropInfo != null, "Invalid drop id.");
        var collectionSymbol = dropInfo.CollectionSymbol;
        AssertDropDetailList(inputDropDetailList, dropInfo.CollectionSymbol, input.DropId, input.Index, out var totalAmount);
        var initMaxIndex = State.MaxDropDetailIndexCount.Value;
        Assert(input.Index<=dropInfo.MaxIndex+1 && input.Index <= initMaxIndex, "Invalid index.");

        dropInfo.TotalAmount += totalAmount;
        dropInfo.MaxIndex = Math.Max(dropInfo.MaxIndex, input.Index);
        var dropDetailList = State.DropDetailListMap[input.DropId][input.Index];
        if (dropDetailList != null)
        {
            dropDetailList = new DropDetailList();
        }
        else
        {
            Assert((dropDetailList.Value.Count + input.Value.Distinct().Count()) < State.MaxDropDetailListCount.Value, "Drop detail list is full.");
        }
        dropDetailList.Value.AddRange(input.Value.Distinct());

        State.DropDetailListMap[input.DropId][input.Index] = dropDetailList;
        State.DropInfoMap[input.DropId] = dropInfo;

        //Fire drop-detail added
        Context.Fire(new DropDetailAdded
        {
            DropId = input.DropId,
            CollectionSymbol = collectionSymbol,
            DetailList = dropDetailList
        });
        
        //Fire drop changed
        Context.Fire(new DropChanged
        {
            DropId = input.DropId,
            MaxIndex = dropInfo.MaxIndex,
            CurrentIndex = dropInfo.CurrentIndex,
            TotalAmount = dropInfo.TotalAmount,
            ClaimAmount = dropInfo.ClaimAmount,
            UpdateTime = Context.CurrentBlockTime
        });
        
        return new Empty();
    }

    public override Empty SubmitDrop(Hash dropId)
    {
        Assert(dropId != null, "Invalid input.");
        var dropInfo = State.DropInfoMap[dropId];
        Assert(dropInfo != null, "Invalid drop id.");
        Assert(dropInfo.Owner == Context.Sender, "Only owner can submit drop.");
        Assert(dropInfo.State == DropState.Create, "Invalid drop state.");
        dropInfo.State = DropState.Submit;
        dropInfo.UpdateTime = Context.CurrentBlockTime;
        State.DropInfoMap[dropId] = dropInfo;
        
        Context.Fire(new DropStateChanged
        {
            DropId = dropId,
            State = dropInfo.State,
            UpdateTime = Context.CurrentBlockTime
        });
        return new Empty();
    }
    
    public override Empty CancelDrop(Hash dropId)
    {
        Assert(dropId != null, "Invalid input.");
        var dropInfo = State.DropInfoMap[dropId];
        Assert(dropInfo != null, "Invalid drop id.");
        Assert(dropInfo.Owner == Context.Sender || dropInfo.Owner == State.Admin.Value, "Only owner can submit drop.");
        Assert(dropInfo.State == DropState.Submit, "Invalid drop state.");
        
        dropInfo.State = DropState.Cancel;
        dropInfo.UpdateTime = Context.CurrentBlockTime;
        State.DropInfoMap[dropId] = dropInfo;
        
        Context.Fire(new DropStateChanged
        {
            DropId = dropId,
            State = dropInfo.State,
            UpdateTime = Context.CurrentBlockTime
        });
        return new Empty();
    }
    
    public override Empty FinishDrop(FinishDropInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(input.DropId != null && input.Index > 0, "Invalid input.");
        var dropInfo = State.DropInfoMap[input.DropId];
        Assert(dropInfo != null, "Invalid drop id.");
        Assert(dropInfo.State == DropState.Finish, "Drop already finished.");
        Assert(dropInfo.ExpireTime <= Context.CurrentBlockTime, "Drop not expired.");

        var dropDetailInfo = State.DropDetailListMap[input.DropId][input.Index];
        Assert(dropDetailInfo.IsFinish == false, $"Drop detail index:{input.Index} already finished.");

        foreach (var detail in dropDetailInfo.Value)
        {
            if (detail.TotalAmount == detail.ClaimAmount) continue;
            if (dropInfo.IsBurn)
            {
                Context.SendInline(State.TokenContract.Value, nameof(State.TokenContract.Burn), new BurnInput
                {
                    Symbol = detail.Symbol,
                    Amount = detail.TotalAmount - detail.ClaimAmount,
                });
            }
            else
            {
                Context.SendInline(State.TokenContract.Value, nameof(State.TokenContract.Transfer), new TransferInput
                {
                    To = dropInfo.Owner,
                    Symbol = detail.Symbol,
                    Amount = detail.TotalAmount - detail.ClaimAmount
                });
            }
        }

        //change drop detail state to finish
        dropDetailInfo.IsFinish = true;
        State.DropDetailListMap[input.DropId][input.Index] = dropDetailInfo;
        Context.Fire(new DropDetailChanged
        {
            DropId = input.DropId,
            IsFinish = true,
            Index = input.Index
        });
        
        //change drop state to finish
        var isAllFinish = true;
        for (int i = 0; i < dropInfo.MaxIndex; i++)
        {
            if (i == input.Index) continue;
            
            var detail = State.DropDetailListMap[input.DropId][i];
            if(detail == null) continue;
            
            isAllFinish = isAllFinish && detail.IsFinish;
            if(!isAllFinish) break;
        }

        if (isAllFinish)
        {
            dropInfo.State = DropState.Finish;
            dropInfo.UpdateTime = Context.CurrentBlockTime;
            State.DropInfoMap[input.DropId] = dropInfo;
            Context.Fire(new DropStateChanged
            {
                DropId = input.DropId,
                State = dropInfo.State,
                UpdateTime = Context.CurrentBlockTime
            });
        }
        return new Empty();
    }

    public override Empty ClaimDrop(ClaimDropInput input)
    {
        Assert(input != null && input.DropId != null && input.ClaimAmount > 0, "Invalid input.");
        var dropInfo = State.DropInfoMap[input.DropId];
        Assert(dropInfo != null, "Invalid drop id.");
        Assert(dropInfo.State == DropState.Submit, "Invalid drop state.");
        Assert(dropInfo.ClaimAmount < dropInfo.TotalAmount, "All NFT already claimed.");
        Assert(dropInfo.ExpireTime > Context.CurrentBlockTime, "Drop already expired.");
        var claimDropDetail = State.ClaimDropMap[input.DropId][Context.Sender];
        Assert(claimDropDetail == null || (claimDropDetail.Amount + input.ClaimAmount) <= dropInfo.ClaimMax,
            "Claimed exceed max amount.");

        var actualClaimAmount = 0l;
        var unClaimAmount = input.ClaimAmount;
        var currentIndex = dropInfo.CurrentIndex;
        var claimDetailRecordList = new List<ClaimDetailRecord>();
        while (actualClaimAmount < input.ClaimAmount && currentIndex <= dropInfo.MaxIndex)
        {
            var currentClaimDropDetailList = State.DropDetailListMap[input.DropId][currentIndex];

            foreach (var detailInfo in currentClaimDropDetailList.Value)
            {
                var currentClaimAmount = 0l;
                if (detailInfo.ClaimAmount == detailInfo.TotalAmount) continue;
                if ((detailInfo.TotalAmount - detailInfo.ClaimAmount) >= unClaimAmount)
                {
                    currentIndex = (detailInfo.TotalAmount - detailInfo.ClaimAmount) == unClaimAmount
                        ? currentIndex + 1
                        : currentIndex;
                    currentClaimAmount = unClaimAmount;
                    detailInfo.ClaimAmount += unClaimAmount;
                    actualClaimAmount += unClaimAmount;
                    unClaimAmount = 0;
                }
                else
                {
                    currentClaimAmount = detailInfo.TotalAmount - detailInfo.ClaimAmount;
                    detailInfo.ClaimAmount = detailInfo.TotalAmount;
                    actualClaimAmount += currentClaimAmount;
                    currentIndex++;
                }

                //transfer nft
                Context.SendInline(State.TokenContract.Value, nameof(State.TokenContract.Transfer), new TransferInput
                {
                    To = Context.Sender,
                    Symbol = detailInfo.Symbol,
                    Amount = currentClaimAmount
                });
                claimDetailRecordList.Add(new ClaimDetailRecord
                {
                    Symbol = detailInfo.Symbol,
                    Amount = currentClaimAmount
                });
            }
            
            //update claim drop detail
            State.DropDetailListMap[input.DropId][currentIndex] = currentClaimDropDetailList;
        }

        //update drop info  
        dropInfo.ClaimAmount += actualClaimAmount;
        dropInfo.CurrentIndex = Math.Min(currentIndex, dropInfo.MaxIndex);
        dropInfo.UpdateTime = Context.CurrentBlockTime;
        State.DropInfoMap[input.DropId] = dropInfo;

        //transfer token
        if (dropInfo.ClaimPrice.Amount > 0)
        {
            State.TokenContract.TransferFrom.Send(new TransferFromInput
            {
                From = Context.Sender,
                To = dropInfo.Owner,
                Symbol = dropInfo.ClaimPrice.Symbol,
                Amount = dropInfo.ClaimPrice.Amount * actualClaimAmount
            });
        }

        //Fire event drop changed
        Context.Fire(new DropChanged
        {
            DropId = input.DropId,
            MaxIndex = dropInfo.MaxIndex,
            CurrentIndex = dropInfo.CurrentIndex,
            TotalAmount = dropInfo.TotalAmount,
            ClaimAmount = dropInfo.ClaimAmount,
            UpdateTime = Context.CurrentBlockTime
        });
        
        //Fire event claim drop
        Context.Fire(new DropClaimAdded
        {
            DropId = input.DropId,
            CurrentAmount = actualClaimAmount,
            TotalAmount = dropInfo.ClaimAmount,
            ClaimDetailRecord = new ClaimDetailRecordList()
            {
                Value = {claimDetailRecordList}
            }
        });
        
        return new Empty();
    }
}