using Nobitex.Net;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Nobitex.TraderBot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var token = File.ReadAllText("W:\\projects\\nobitex.net\\token");

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

        // shared state
        static decimal? _previousBinancePrice = null;
        static TradeHistoryDto _latestNobitexTrade = null;
        static decimal? _previousObservedNobitexPrice = null;

        public async Task RunAsync()
        {
            Console.WriteLine("Logger started. Press Ctrl+C to stop.");

            // start two independent loops with different delays
            var binanceTask = Task.Run(() => BinanceLoopAsync("BTCUSDT", 1000, _cts.Token)); // 1s delay
            var nobitexTask = Task.Run(() => NobitexLoopAsync("BTCUSDT", 1000, _cts.Token));  // 0.8s delay

            // wait until cancellation
            await Task.WhenAll(binanceTask, nobitexTask);
        }

        // add a shared HttpClient
        static readonly System.Net.Http.HttpClient _http = new System.Net.Http.HttpClient();

        // helper to sleep taking request time into account
        private static Task WaitResidualAsync(int targetIntervalMs, TimeSpan requestElapsed, CancellationToken ct)
        {
            var waitMs = targetIntervalMs - (int)requestElapsed.TotalMilliseconds;
            if (waitMs < 50) waitMs = 50; // minimum backoff to avoid busy-loop; tune as needed
            return Task.Delay(waitMs, ct);
        }

        private async Task BinanceLoopAsync(string symbol, int delayMs, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    // perform request using the shared _http
                    string url = $"https://api.binance.com/api/v3/ticker/price?symbol={symbol.ToUpper()}";
                    var s = await _http.GetStringAsync(url, ct);
                    using var doc = JsonDocument.Parse(s);
                    var priceStr = doc.RootElement.GetProperty("price").GetString();
                    if (!decimal.TryParse(priceStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var binancePrice))
                    {
                        Console.WriteLine($"{DateTime.Now:HH:mm:ss:fff} BINANCE parse error");
                    }
                    else
                    {
                        bool changed = !_previousBinancePrice.HasValue || binancePrice != _previousBinancePrice.Value;
                        if (changed)
                        {
                            string changeText = "n/a";
                            if (_previousBinancePrice.HasValue && _previousBinancePrice.Value > 0)
                            {
                                var diff = binancePrice - _previousBinancePrice.Value;
                                var pct = diff / _previousBinancePrice.Value * 100m;
                                changeText = $"{(pct >= 0 ? "+" : "")}{pct:F3}%";
                            }

                            Console.WriteLine($"{DateTime.Now:HH:mm:ss:fff}  BINANCE {symbol}  Price: {binancePrice:F6}  Change: {changeText}");
                            _previousBinancePrice = binancePrice;
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine($"{DateTime.Now:HH:mm:ss:fff}  BINANCE fetch error: {ex.Message}");
                }
                finally
                {
                    sw.Stop();
                    try { await WaitResidualAsync(delayMs, sw.Elapsed, ct); } catch (OperationCanceledException) {  }
                }
            }
        }

        private async Task NobitexLoopAsync(string symbol, int delayMs, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var tradesResp = await _client.Market.GetTradesAsync(symbol); // if client supports cancellation
                    var trades = tradesResp?.Trades;
                    if (trades != null && trades.Count > 0)
                    {
                        var latest = trades.OrderByDescending(t => t.Time).First();

                        bool isNew = _latestNobitexTrade == null
                                     || latest.Time != _latestNobitexTrade.Time
                                     || latest.Price != _latestNobitexTrade.Price;

                        if (isNew)
                        {
                            _latestNobitexTrade = latest;

                            decimal nobitexPrice = 0m;
                            decimal nobitexVol = 0m;
                            decimal.TryParse(latest.Price ?? "0", System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out nobitexPrice);
                            decimal.TryParse(latest.Volume ?? "0", System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out nobitexVol);

                            string changeSincePrev = "n/a";
                            if (_previousObservedNobitexPrice.HasValue && _previousObservedNobitexPrice.Value > 0)
                            {
                                var diff = nobitexPrice - _previousObservedNobitexPrice.Value;
                                var pct = diff / _previousObservedNobitexPrice.Value * 100m;
                                changeSincePrev = $"{(pct >= 0 ? "+" : "")}{pct:F3}%";
                            }

                            string diffToBinance = "n/a";
                            if (_previousBinancePrice.HasValue && _previousBinancePrice.Value > 0)
                            {
                                var diff = nobitexPrice - _previousBinancePrice.Value;
                                var pct = diff / _previousBinancePrice.Value * 100m;
                                diffToBinance = $"{(pct >= 0 ? "+" : "")}{pct:F3}%";
                            }

                            _previousObservedNobitexPrice = nobitexPrice;

                            var time = DateTimeOffset.FromUnixTimeMilliseconds(latest.Time).LocalDateTime;
                            var side = (latest.Type ?? "").ToLowerInvariant();

                            string binanceChangeForRow = _previousBinancePrice.HasValue ? $"LastBin:{_previousBinancePrice.Value:F6}" : "LastBin:n/a";

                            Console.WriteLine($"{time:HH:mm:ss:fff} | {side,-4} | Nob:{nobitexPrice,10:F6} ({changeSincePrev,8}) | Diff vs Bin:{diffToBinance,8} | Vol:{nobitexVol,8:F6} | {binanceChangeForRow}");
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine($"{DateTime.Now:HH:mm:ss:fff}  NOBITEX fetch error: {ex.Message}");
                }
                finally
                {
                    sw.Stop();
                    try { await WaitResidualAsync(delayMs, sw.Elapsed, ct); }
                    catch (OperationCanceledException) { }
                }
            }
        }

        public async Task<decimal> GetLatestPriceAsync(string symbol)
        {
            using var http = new System.Net.Http.HttpClient();
            string url = $"https://api.binance.com/api/v3/ticker/price?symbol={symbol.ToUpper()}";
            var s = await http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(s);
            var price = doc.RootElement.GetProperty("price").GetString();
            return decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
