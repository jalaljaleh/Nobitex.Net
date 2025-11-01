using Nobitex.TraderBot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nobitex.TraderBot;
public class ArbDecision
{
    public bool CanExecute { get; set; }
    public string Direction { get; set; } // "BuyIrt_SellUsdt" or "BuyUsdt_SellIrt"
    public decimal VolumeTon { get; set; }
    public decimal ExpectedProfitIrt { get; set; }
    public decimal ExpectedRoiPercent { get; set; }
    public decimal BuyPriceUsed { get; set; }      // in source market units (IRT or USDT)
    public decimal SellPriceUsed { get; set; }     // in dest market units (USDT or IRT)
    public decimal ConvertedBuyIrt { get; set; }   // cost in IRT after conversion
    public decimal ConvertedSellIrt { get; set; }  // proceeds in IRT after conversion
    public string Reason { get; set; }             // if cannot execute, why
    public string SuggestedOrderTypeBuy { get; set; }   // "market" or "limit(maker)"
    public string SuggestedOrderTypeSell { get; set; }  // "limit" or "market"


public static ArbDecision EvaluateAndDecide(
    ArbHelpers.SimpleOrderBook tonIrt,
    ArbHelpers.SimpleOrderBook tonUsdt,
    ArbHelpers.SimpleOrderBook usdIrt,
    decimal desiredVolumeTon,
    decimal irtBalance,
    decimal usdtBalance,
    decimal tonBalance,
    decimal minProfitIrt,      // absolute minimum profit in IRT required
    decimal minRoiPercent,     // minimum ROI %
    bool preferMakerOnDestination = true)
    {
        var decision = new ArbDecision { VolumeTon = desiredVolumeTon, CanExecute = false };

        // Basic depth checks and VWAPs
        try
        {
            // Direction A: Buy on IRT, Sell on USDT
            var buyVwapIrt = ArbHelpers.GetVwap(tonIrt.Asks, desiredVolumeTon);
            var sellVwapUsdt = ArbHelpers.GetVwap(tonUsdt.Bids, desiredVolumeTon);
            decimal sellIrtConvertedA = ArbHelpers.ConvertUsdtToIrt(sellVwapUsdt, usdIrt);

            // Fees - assume taker for buy (instant) and maker for sell if preferred to improve ROI
            decimal buyFeeFractionA = ArbHelpers.IrtTakerFee;
            decimal sellFeeFractionA = preferMakerOnDestination ? ArbHelpers.UsdtMakerFee : ArbHelpers.UsdtTakerFee;

            decimal netCostIrtA = buyVwapIrt * (1 + buyFeeFractionA) * desiredVolumeTon;
            decimal netProceedsIrtA = sellIrtConvertedA * (1 - sellFeeFractionA) * desiredVolumeTon;
            decimal expectedProfitA = netProceedsIrtA - netCostIrtA;

            // Direction B: Buy on USDT, Sell on IRT
            var buyVwapUsdt = ArbHelpers.GetVwap(tonUsdt.Asks, desiredVolumeTon);
            var sellVwapIrt = ArbHelpers.GetVwap(tonIrt.Bids, desiredVolumeTon);
            // convert buy cost to IRT using USD/IRT ask for conservative
            decimal usdIrtAsk = usdIrt.Asks.First().Price;
            decimal buyIrtConvertedB = buyVwapUsdt * usdIrtAsk; // per TON in IRT
            decimal buyFeeFractionB = ArbHelpers.UsdtTakerFee;
            decimal sellFeeFractionB = preferMakerOnDestination ? ArbHelpers.IrtMakerFee : ArbHelpers.IrtTakerFee;

            decimal netCostIrtB = buyIrtConvertedB * (1 + buyFeeFractionB) * desiredVolumeTon;
            decimal netProceedsIrtB = sellVwapIrt * (1 - sellFeeFractionB) * desiredVolumeTon;
            decimal expectedProfitB = netProceedsIrtB - netCostIrtB;

            // Choose best positive profit
            decimal bestProfit = expectedProfitA;
            string direction = "BuyIrt_SellUsdt";
            decimal buyUsed = buyVwapIrt;
            decimal sellUsed = sellVwapUsdt;
            decimal convertedBuy = netCostIrtA;
            decimal convertedSell = netProceedsIrtA;

            if (expectedProfitB > bestProfit)
            {
                bestProfit = expectedProfitB;
                direction = "BuyUsdt_SellIrt";
                buyUsed = buyVwapUsdt;
                sellUsed = sellVwapIrt;
                convertedBuy = netCostIrtB;
                convertedSell = netProceedsIrtB;
            }

            // ROI percent relative to locked capital (use netCost as denominator)
            decimal roiPercent = bestProfit / (Math.Max(1, Math.Abs(convertedBuy))) * 100m; // avoid div0

            // Additional safety reductions: subtract slippage buffer (configurable)
            decimal slippageBufferIrt = Math.Max(10m, Math.Abs(convertedBuy) * 0.0005m); // e.g., 0.05% or min 10 IRT
            bestProfit -= slippageBufferIrt;

            // Balance checks based on direction
            bool enoughBalance = true;
            if (direction == "BuyIrt_SellUsdt")
            {
                // need enough IRT to buy desiredVolumeTon at buyVwapIrt plus fees
                decimal requiredIrt = buyVwapIrt * (1 + buyFeeFractionA) * desiredVolumeTon;
                if (irtBalance < requiredIrt) { enoughBalance = false; decision.Reason = "Insufficient IRT balance"; }
            }
            else
            {
                // need enough USDT to buy on USDT market
                decimal requiredUsdt = buyVwapUsdt * (1 + buyFeeFractionB) * desiredVolumeTon;
                if (usdtBalance < requiredUsdt) { enoughBalance = false; decision.Reason = "Insufficient USDT balance"; }
            }

            // Final decision criteria
            if (enoughBalance && bestProfit >= minProfitIrt && roiPercent >= minRoiPercent)
            {
                decision.CanExecute = true;
                decision.Direction = direction;
                decision.ExpectedProfitIrt = Math.Round(bestProfit, 2);
                decision.ExpectedRoiPercent = Math.Round(roiPercent, 4);
                decision.BuyPriceUsed = buyUsed;
                decision.SellPriceUsed = sellUsed;
                decision.ConvertedBuyIrt = Math.Round(convertedBuy, 2);
                decision.ConvertedSellIrt = Math.Round(convertedSell, 2);
                decision.SuggestedOrderTypeBuy = "market"; // immediate fill recommended for source leg
                decision.SuggestedOrderTypeSell = preferMakerOnDestination ? "limit(maker)" : "market";
                decision.Reason = "Meets profit, ROI and balance checks";
            }
            else
            {
                if (string.IsNullOrEmpty(decision.Reason))
                    decision.Reason = $"No sufficient profit or ROI ({bestProfit:F2} IRT, ROI {roiPercent:F3}%)";
            }
        }
        catch (InvalidOperationException ex)
        {
            decision.Reason = "Depth missing or not enough liquidity: " + ex.Message;
        }
        catch (Exception ex)
        {
            decision.Reason = "Evaluation error: " + ex.Message;
        }

        return decision;
    }
}
