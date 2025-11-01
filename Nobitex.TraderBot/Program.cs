using Newtonsoft.Json;
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
                try { await Task.Delay(100, _cts.Token); } catch { break; }
            }
        }
        static TradeHistoryDto _latest;
        private async Task OnUpdateAsync()
        {
            var binance = await GetLatestPriceAsync("DOGEUSDT");
            var tradesResp = await _client.Market.GetTradesAsync("DOGEUSDT");

            var trades = tradesResp?.Trades;
            if (trades == null || trades.Count == 0)
            {
                Console.WriteLine($"{DateTime.Now:HH:mm:ss}: no trades");
                return;
            }

            // pick the most recent trade (highest Time)
            var latest = trades.OrderByDescending(t => t.Time).First();
            
            var time = DateTimeOffset.FromUnixTimeMilliseconds(latest.Time).LocalDateTime;
            Console.WriteLine($"{time:HH:mm:ss}                      {binance}");
            if (_latest == latest) return;

            _latest = latest;

            // convert fields
            var side = (latest.Type ?? "").ToLower(); // "buy" or "sell"
            var price = latest.Price ?? "0";
            var c = latest.Volume ?? "0";
            Console.WriteLine($"{time:HH:mm:ss}  {side}                {price}  ");
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
