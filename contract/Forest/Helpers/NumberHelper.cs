using System;
using AElf.Sdk.CSharp;

namespace Forest.Helpers;

public static class NumberHelper
{
    internal static long DivideByPowerOfTen(long quantity, int tokenDecimals)
    {
        if (quantity == ForestContract.NumberZero || tokenDecimals == ForestContract.NumberZero)
        {
            return ForestContract.NumberZero;
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
}