
namespace Nobitex.TraderBot;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;


public static class ArbHelpers
{
    // fees (fractions)
    public const decimal IrtMakerFee = 0.025m;
    public const decimal IrtTakerFee = 0.0025m;
    public const decimal UsdtMakerFee = 0.0010m;
    public const decimal UsdtTakerFee = 0.0013m;

    // orderbook simplified entry
    public class OBE
    {
        public decimal Price { get; set; }
        public decimal Amount { get; set; }
    }

    public class SimpleOrderBook
    {
        public List<OBE> Asks { get; set; } = new List<OBE>(); // sorted ascending price
        public List<OBE> Bids { get; set; } = new List<OBE>(); // sorted descending price
    }

    public class ArbEvaluation
    {
        public decimal VolumeTon { get; set; }
        public decimal BuyVwapIrt { get; set; }
        public decimal SellVwapUsdt { get; set; }
        public decimal SellVwapIrtConverted { get; set; }
        public decimal NetProfitIrt_BuyIrt_SellUsdt { get; set; }
        public decimal BuyVwapUsdt { get; set; }
        public decimal SellVwapIrt { get; set; }
        public decimal BuyVwapIrtConverted { get; set; }
        public decimal NetProfitIrt_BuyUsdt_SellIrt { get; set; }
    }

    // parse orderbook JSON-like objects (expects dynamic shapes with Asks/Bids arrays of { Price, Amount })
    public static SimpleOrderBook ToSimple(object obs)
    {
        // If you have strongly typed objects, replace this with direct mapping
        // Here obs is expected to be a dynamic or JObject-like object; we'll use reflection-ish access
        var book = new SimpleOrderBook();
        if (obs == null) return book;

        dynamic dyn = obs;

        IEnumerable<dynamic> asks = null;
        IEnumerable<dynamic> bids = null;
        try
        {
            asks = dyn.Asks;
            bids = dyn.Bids;
        }
        catch
        {
            return book;
        }

        foreach (var a in asks)
        {
            var p = ParseDecimal(a.Price.ToString());
            var q = ParseDecimal(a.Amount.ToString());
            book.Asks.Add(new OBE { Price = p, Amount = q });
        }
        foreach (var b in bids)
        {
            var p = ParseDecimal(b.Price.ToString());
            var q = ParseDecimal(b.Amount.ToString());
            book.Bids.Add(new OBE { Price = p, Amount = q });
        }

        // ensure sorting
        book.Asks = book.Asks.OrderBy(x => x.Price).ToList();
        book.Bids = book.Bids.OrderByDescending(x => x.Price).ToList();
        return book;
    }

    static decimal ParseDecimal(string s)
    {
        return decimal.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture);
    }

    // VWAP : walk depth until volume filled; throws if not enough depth
    public static decimal GetVwap(IList<OBE> levels, decimal volume)
    {
        if (volume <= 0) throw new ArgumentException("volume must be > 0");
        decimal taken = 0m;
        decimal cost = 0m;
        foreach (var lvl in levels)
        {
            decimal canTake = Math.Min(lvl.Amount, volume - taken);
            cost += canTake * lvl.Price;
            taken += canTake;
            if (taken >= volume) break;
        }
        if (taken < volume) throw new InvalidOperationException("Not enough book depth for requested volume");
        return cost / volume;
    }

    // Conservative USD<->IRT conversion helpers:
    // convert price in USDT to IRT using USD/IRT bid (we assume usdBook.Bids[0] exists)
    public static decimal ConvertUsdtToIrt(decimal usdtPrice, SimpleOrderBook usdBook)
    {
        if (usdBook?.Bids == null || usdBook.Bids.Count == 0) throw new InvalidOperationException("USD/IRT bids missing");
        decimal usdIrtBid = usdBook.Bids[0].Price;
        return usdtPrice * usdIrtBid;
    }
    // convert IRT price to USDT using USD/IRT ask
    public static decimal ConvertIrtToUsdt(decimal irtPrice, SimpleOrderBook usdBook)
    {
        if (usdBook?.Asks == null || usdBook.Asks.Count == 0) throw new InvalidOperationException("USD/IRT asks missing");
        decimal usdIrtAsk = usdBook.Asks[0].Price;
        return irtPrice / usdIrtAsk;
    }

    // Evaluate both directions for a target TON volume
    public static ArbEvaluation EvaluateArb(SimpleOrderBook tonIrt, SimpleOrderBook tonUsdt, SimpleOrderBook usdIrt, decimal volumeTon, decimal minProfitIrt = 0m)
    {
        var r = new ArbEvaluation { VolumeTon = volumeTon };

        // Direction A: Buy TON on IRT (use Asks), Sell TON on USDT (use Bids)
        r.BuyVwapIrt = GetVwap(tonIrt.Asks, volumeTon);
        r.SellVwapUsdt = GetVwap(tonUsdt.Bids, volumeTon);
        // convert the sell proceeds (USDT -> IRT) conservatively using USD/IRT bid
        r.SellVwapIrtConverted = ConvertUsdtToIrt(r.SellVwapUsdt, usdIrt);
        // apply fees (assume taker for both sides by default; you may adjust to maker fractions when posting)
        decimal netSellIrt = r.SellVwapIrtConverted * (1 - UsdtTakerFee); // selling on USDT (taker)
        decimal netBuyIrt = r.BuyVwapIrt * (1 + IrtTakerFee); // buying on IRT (taker)
        r.NetProfitIrt_BuyIrt_SellUsdt = netSellIrt - netBuyIrt;

        // Direction B: Buy TON on USDT (use Asks), Sell TON on IRT (use Bids)
        r.BuyVwapUsdt = GetVwap(tonUsdt.Asks, volumeTon);
        r.SellVwapIrt = GetVwap(tonIrt.Bids, volumeTon);
        // convert buy cost in USDT to IRT conservatively using USD/IRT ask
        r.BuyVwapIrtConverted = ConvertUsdtToIrt(r.BuyVwapUsdt, usdIrt); // it's okay to use USD/IRT ask vs bid here for conservative calc
                                                                         // apply fees
        decimal netBuyIrt_B = r.BuyVwapIrtConverted * (1 + UsdtTakerFee); // buying on USDT (taker)
        decimal netSellIrt_B = r.SellVwapIrt * (1 - IrtTakerFee); // selling on IRT (taker)
        r.NetProfitIrt_BuyUsdt_SellIrt = netSellIrt_B - netBuyIrt_B;

        return r;
    }
}



