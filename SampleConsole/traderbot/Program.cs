
using Nobitex.Net;
using Nobitex.TraderBot;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SampleConsole
{
    public class Test
    {
        public async Task Testt(string[] args)
        {
            var token = File.ReadAllText("W:\\projects\\nobitex.net\\token");

            // Replace with your real API key
            var client = new NobitexClient(token);
            var p = new Test(client);
            await p.RunAsync();
        }

        private readonly NobitexClient _client;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public Test(NobitexClient client)
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
                try { await Task.Delay(4000, _cts.Token); } catch { break; }
            }
        }

        private async Task OnUpdateAsync()
        {
            var tonIrtRaw = await _client.Market.GetOrderbookAsync("TONIRT");
            var tonUsdtRaw = await _client.Market.GetOrderbookAsync("TONUSDT");
            var usdIrtRaw = await _client.Market.GetOrderbookAsync("USDTIRT");

            var tonIrt = ArbHelpers.ToSimple(tonIrtRaw);
            var tonUsdt = ArbHelpers.ToSimple(tonUsdtRaw);
            var usdIrt = ArbHelpers.ToSimple(usdIrtRaw);

            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + ": ");
            decimal irtBalance = await GetIrtBalanceAsync();
            decimal usdtBalance = await GetUsdtBalanceAsync();
            decimal tonBalance = await GetTonBalanceAsync();

            decimal testVolumeTon = 0.1m;

            try
            {
                var decision = ArbDecision.EvaluateAndDecide(
                    tonIrt, tonUsdt, usdIrt,
                    testVolumeTon, irtBalance, usdtBalance, tonBalance,
                    minProfitIrt: 0m,    // changed to zero
                    minRoiPercent: 0m,
                    preferMakerOnDestination: true);

                decimal profitIrt = decision.ExpectedProfitIrt;
                decimal capitalIrt = decision.ConvertedBuyIrt > 0 ? decision.ConvertedBuyIrt : 1m;
                decimal profitPercent = (profitIrt / capitalIrt) * 100m;
                if (decision.CanExecute != true)
                {
                    return;
                }
                Console.WriteLine($"Decision: CanExecute={decision.CanExecute} Direction={decision.Direction} Reason={decision.Reason}");
                Console.WriteLine($"Profit: {profitIrt:F2} IRT  |  Profit%: {profitPercent:F4}%");
                Console.WriteLine($"BuyPrice={decision.BuyPriceUsed}  SellPrice={decision.SellPriceUsed}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Eval error: " + ex.Message);
            }
            Console.WriteLine("-----------------------------------------------------------------------------");

        }


        // ---- Mock balance getters (replace with real implementations) ----
        private Task<decimal> GetIrtBalanceAsync() => Task.FromResult(10_000_000m);
        private Task<decimal> GetUsdtBalanceAsync() => Task.FromResult(10_000m);
        private Task<decimal> GetTonBalanceAsync() => Task.FromResult(100m);
    }
}
