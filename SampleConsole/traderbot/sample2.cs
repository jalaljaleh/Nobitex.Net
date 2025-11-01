
using Nobitex.Net;
using System;
using System.Drawing;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Nobitex.TraderBot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var token = File.ReadAllText("W:\\projects\\nobitex.net\\token");

            // Replace with your real API key
            var client = new NobitexClient(token);
            var p = new Program(client);
            await p.RunAsync();
        }

        private readonly NobitexClient _client;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public Program(NobitexClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                _cts.Cancel();
                Console.WriteLine("Stopping...");
            };
        }

        public async Task RunAsync()
        {
            Console.WriteLine("Arb evaluator started. Press Ctrl+C to stop.");

            Console.WriteLine($"Time      type     amount     price");


            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await OnUpdateAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Main loop error: {ex.Message}");
                }

                // throttle - adjust as needed but respect API rate limits
                try { await Task.Delay(800, _cts.Token); } catch { break; }
            }
        }
        // changes: only update stored Binance price when it actually changes,
        // and only compute binanceChange when there's a change.

        static TradeHistoryDto _latest;
        static decimal? _previousObservedPrice = null; // last Nobitex price we reported
        static decimal? _previousBinancePrice = null;  // last stored Binance price

        private async Task OnUpdateAsync()
        {
            const string target = "NOTUSDT";
            // 1) fetch Binance price (still fetch each iteration to detect change)
            decimal binancePrice;
            try
            {
                binancePrice = await GetLatestPriceAsync(target);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm:ss:ffff}  BINANCE fetch error: {ex.Message}");
                return;
            }

            // determine whether Binance price changed compared to stored value
            bool binanceChanged = !_previousBinancePrice.HasValue || binancePrice != _previousBinancePrice.Value;

            // compute percent change only if Binance actually changed
            string binanceChange = "n/a";
            if (binanceChanged && _previousBinancePrice.HasValue && _previousBinancePrice.Value > 0)
            {
                var diff = binancePrice - _previousBinancePrice.Value;
                var pct = diff / _previousBinancePrice.Value * 100m;
                binanceChange = $"{(pct >= 0 ? "+" : "")}{pct:F3}%";
            }

            // print Binance line only when changed (or first time)
            if (binanceChanged)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm:ss:ffff}  BINANCE {target}  Price: {binancePrice:F6}  Change: {binanceChange}");
                // update stored Binance price only when it changed
                _previousBinancePrice = binancePrice;
            }

            // 2) fetch Nobitex trades
            var nobitexCoin = await _client.Market.GetTradesAsync(target);

            var latest = nobitexCoin.Trades.OrderByDescending(t => t.Time).First();

            // if same as before, skip
            if (_latest != null && latest.Time == _latest.Time && latest.Price == _latest.Price)
                return;

            _latest = latest;

            // parse Nobitex price/volume
            decimal nobitexPrice = 0m;
            decimal nobitexVol = 0m;
            decimal.TryParse(latest.Price ?? "0", System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out nobitexPrice);
            decimal.TryParse(latest.Volume ?? "0", System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out nobitexVol);

            var time = DateTimeOffset.FromUnixTimeMilliseconds(latest.Time).LocalDateTime;
            var side = (latest.Type ?? "").ToLowerInvariant();

            // percent change since previous observed Nobitex price
            string changeSincePrev = "n/a";
            if (_previousObservedPrice.HasValue && _previousObservedPrice.Value > 0)
            {
                var diff = nobitexPrice - _previousObservedPrice.Value;
                var pct = diff / _previousObservedPrice.Value * 100m;
                changeSincePrev = $"{(pct >= 0 ? "+" : "")}{pct:F3}%";
            }

            // cross-market diff vs the latest stored Binance price (if available)
            string diffToBinance = "n/a";
            if (_previousBinancePrice.HasValue && _previousBinancePrice.Value > 0)
            {
                var diff = nobitexPrice - _previousBinancePrice.Value;
                var pct = diff / _previousBinancePrice.Value * 100m;
                diffToBinance = $"{(pct >= 0 ? "+" : "")}{pct:F3}%";
            }

            // update previous observed nobitex price
            _previousObservedPrice = nobitexPrice;

            // print Nobitex line and include most recent Binance change info only if Binance changed earlier this loop
            string binanceChangeForRow = binanceChanged ? binanceChange : "no change";
            Console.WriteLine($"{time:HH:mm:ss:ffff} | {side,-4} | Nob:{nobitexPrice,10:F6} ({changeSincePrev,8}) | Diff vs Bin:{diffToBinance,8} | Vol:{nobitexVol,8:F6} | BIN change: {binanceChangeForRow}");
        }



        public async Task<decimal> GetLatestPriceAsync(string symbol)
        {
            using var http = new HttpClient();
            string url = $"https://api.binance.com/api/v3/ticker/price?symbol={symbol.ToUpper()}";
            var s = await http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(s);
            var price = doc.RootElement.GetProperty("price").GetString();
            return decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture);
        }


    }
}
