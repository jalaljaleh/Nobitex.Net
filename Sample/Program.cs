using Nobitex.Net;
using Nobitex.Net.Models;

namespace Sample
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

            new Program().MainAsync().GetAwaiter().GetResult();
        }

        async Task MainAsync()
        {
            var options = new NobitexClientOptions
            {
                ApiToken = Environment.GetEnvironmentVariable("NOBITEX_TOKEN"),  // از پنل نوبیتکس
                UserAgent = "TraderBot/MyFirstBot",
                EnableClientRateLimiting = true
            };

            await using var client = new NobitexClient(options);

            // 1) Market statistics (public - no authentication required)
            var stats = await client.MarketData.GetMarketStatsAsync(srcCurrency: "btc", dstCurrency: "rls");
            var btc = stats.Stats["btc-rls"];
            Console.WriteLine($"BTC best ask: {btc.BestSell:N0} | best bid: {btc.BestBuy:N0} | 24h: {btc.DayChange}%");

            // 2) Order book (public)
            var book = await client.MarketData.GetOrderBookAsync("BTCIRT", size: 50);
            Console.WriteLine($"Top bid: {book.Bids[0].Price} | Top ask: {book.Asks[0].Price}");

            //// 3) Wallet balance
            //var balance = await client.Wallet.GetBalanceAsync("rls");
            //Console.WriteLine($"IRR balance: {balance:N0}");

            //// 4) Place a limit buy order
            //var request = new PlaceOrderRequest
            //{
            //    Side = OrderSide.Buy,
            //    Execution = OrderExecution.Limit,
            //    SrcCurrency = "btc",
            //    DstCurrency = "rls",
            //    Amount = 0.0005m,
            //    Price = 4_400_000_000m,
            //    ClientOrderId = $"bot-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"
            //};

            //var order = await client.Trading.PlaceOrderAsync(request);
            //Console.WriteLine($"Order #{order.Id} -> {order.Status}, unmatched: {order.UnmatchedAmount}");

            //// 5) Poll the order until it is finished (or cancelled)
            //while (order.Status is OrderStatus.Active or OrderStatus.New)
            //{
            //    await Task.Delay(TimeSpan.FromSeconds(3));
            //    order = await client.Trading.GetOrderAsync(order.Id);
            //    Console.WriteLine($"  matched {order.MatchedAmount}/{order.Amount}");
            //}

            //// 6) Report
            //var fills = await client.Trading.GetMyTradesAsync("btc", "rls");
            //foreach (var trade in fills.Trades)
            //{
            //    Console.WriteLine($"{trade.Timestamp:HH:mm:ss} {trade.Type,-4} {trade.Amount} @ {trade.Price:N0}");
            //}
        }
    }
}
