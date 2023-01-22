﻿namespace SimpleBacktestLib.Internal.Margin;

internal static class MarginLogic
{

    /// <summary>
    /// Calculate unrealized balances factoring in this margin position.
    /// Returns the balances as they would be if the position were closed right now.
    /// </summary>
    /// <param name="position"></param>
    /// <param name="tickPrice"></param>
    /// <param name="baseCollateral"></param>
    /// <param name="quoteCollateral"></param>
    /// <returns></returns>
    internal static (bool IsLiquid, decimal UnrealizedBase, decimal UnrealizedQuote)
        CalculateUnrealizedBalances(MarginPosition position, decimal tickPrice, decimal baseCollateral, decimal quoteCollateral)
    {
        // Determine asset used to borrow
        AssetType borrowedAsset = GetBorrowAssetType(position.MarginDirection);
        decimal repayableAmountInBorrowedAsset = position.MarginDirection == TradeType.MarginLong ?
            ValueAssessment.CalcQuote(position.InitialTradedAmount, tickPrice) : ValueAssessment.CalcBase(position.InitialTradedAmount, tickPrice);
        decimal borrowedPlOnLoan = repayableAmountInBorrowedAsset - position.BorrowedAmount;

        // Calculate updated balances
        decimal unrealizedBase = baseCollateral;
        decimal unrealizedQuote = quoteCollateral;
        if (position.MarginDirection == TradeType.MarginLong)
            unrealizedBase += ValueAssessment.CalcBase(borrowedPlOnLoan, tickPrice);
        else if (position.MarginDirection == TradeType.MarginShort)
            unrealizedQuote += ValueAssessment.CalcQuote(borrowedPlOnLoan, tickPrice);

        // Handle negative collateral
        if (unrealizedBase < 0) // Long broke base liquidity
        {
            unrealizedQuote += ValueAssessment.CalcQuote(unrealizedBase, tickPrice);
            unrealizedBase = 0;
        }
        else if (unrealizedQuote < 0) // Short broke quote liquidity
        {
            unrealizedBase += ValueAssessment.CalcBase(unrealizedQuote, tickPrice);
            unrealizedQuote = 0;
        }

        // Determine if liquid
        decimal combinedCurrentCollateralInBorrowedAsset = ValueAssessment.GetCombinedValue(
            borrowedAsset, unrealizedBase, unrealizedQuote, tickPrice);
        bool isLiquid = combinedCurrentCollateralInBorrowedAsset > position.BorrowedAmount * position.LiquidationRatio;

        // Return parsed results
        return (isLiquid, unrealizedBase, unrealizedQuote);
    }

    
    /// <summary>
    /// Determine the asset type we need to, or have borrowed depending on the direction of our margin trade.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="ArgumentException">Must be Long or Short</exception>
    internal static AssetType GetBorrowAssetType(TradeType direction)
    {
        if (direction != TradeType.MarginLong && direction != TradeType.MarginShort)
            throw new ArgumentException("GetPosition() expects direction to be either MarginLong or MarginShort.");
        return direction == TradeType.MarginLong ? AssetType.Quote : AssetType.Base;
    }
}
