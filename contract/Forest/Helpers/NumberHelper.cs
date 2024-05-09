using System;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;

namespace Forest.Helpers;

public static class NumberHelper
{
    internal static long DivideByPowerOfTen(long quantity, int tokenDecimals)
    {
        if (quantity == ForestContract.NumberZero || tokenDecimals == ForestContract.NumberZero)
        {
            return quantity;
        }

        long divisor = ForestContract.NumberOne;
        for (int i = ForestContract.NumberZero; i < tokenDecimals; i++)
        {
            divisor *= ForestContract.NumberTen;
        }

        if (quantity % divisor != ForestContract.NumberZero)
        {
            throw new AssertionException(
                $"The calculated quantity is not an integer when rounded to {tokenDecimals} decimal places.");
        }

        return quantity / divisor;
    }

    public static long GetPowerOfTen(long number, int decimals)
    {
        if (number == ForestContract.NumberZero || decimals == ForestContract.NumberZero)
        {
            return number;
        }

        long result = number;
        for (var i = ForestContract.NumberZero; i < decimals; i++)
        {
            result *= ForestContract.NumberTen;
        }

        return result;
    }
}