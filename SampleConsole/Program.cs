using Nobitex.Websocket.Net;
using Nobitex.Websocket.Net.Abstractions;
using Nobitex.Websocket.Net.Services;
using Nobtiex.Websocket.Net;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        // Read API token from env for safety; you can inline a token for quick local test (not recommended).
        var apiToken = "";

        if (string.IsNullOrWhiteSpace(apiToken) || apiToken == "<YOUR_API_TOKEN_HERE>")
        {
            Console.WriteLine("Set NOBITEX_API_TOKEN environment variable or replace the placeholder in code.");
            return 1;
        }

        // Optional: user websocketAuthParam if you want to test private channels
        var websocketAuthParam = Environment.GetEnvironmentVariable("NOBITEX_WS_AUTH_PARAM"); // may be null

        // Create options
        var options = new NobitexWebsocketOptions
        {
            ApiToken = apiToken,
            ApiBaseAddress = new Uri("https://apiv2.nobitex.ir/"),
            WebsocketUrl = new Uri("wss://ws.nobitex.ir/connection/websocket"),
            WebsocketAuthParam = websocketAuthParam
        };

        // Create HttpClient for NobitexConnectionTokenProvider
        var http = new HttpClient { BaseAddress = options.ApiBaseAddress };

        // Create raw token provider (HTTP)
        var rawProvider = new NobitexConnectionTokenProvider(http, options.ApiToken);

        // Wrap with cached provider
        var tokenProvider = new CachedConnectionTokenProvider(rawProvider, TimeSpan.FromSeconds(60));

        // Create client (pass options)
        var client = new WebsocketCentrifugoClient(tokenProvider, options);

        // Create parser and orderbook helper
        var parser = new MessageParser();
        var obHelper = new OrderbookSubscriptionHelper(client, parser);

        // Wire raw handlers (optional)
        client.OnFrame += async frame =>
        {
            Console.WriteLine($"[FRAME] {frame.ToString()}");
            await Task.CompletedTask;
        };

        client.OnMessage += async p =>
        {
            if (p.TryGetProperty("channel", out var ch))
            {
                var chs = ch.GetString();
                var data = p.TryGetProperty("data", out var d) ? d.ToString() : "<no-data>";
                Console.WriteLine($"[MSG] {chs} => {Shorten(data, 400)}");
            }
            else
            {
                Console.WriteLine($"[MSG] {p}");
            }
            await Task.CompletedTask;
        };

        // Typed orderbook events
        obHelper.OnOrderbook += async ob =>
        {
            var topAsks = string.Join(", ", ob.Asks.Take(3).Select(a => $"{a.Price}:{a.Amount}"));
            var topBids = string.Join(", ", ob.Bids.Take(3).Select(b => $"{b.Price}:{b.Amount}"));
            Console.WriteLine($"[ORDERBOOK] market={ob.Market ?? "?"} asks={ob.Asks.Count} bids={ob.Bids.Count} lastPrice={ob.LastTradePrice} lastUpdate={ob.LastUpdate}");
            Console.WriteLine($"Top asks: {topAsks}");
            Console.WriteLine($"Top bids: {topBids}");
            await Task.CompletedTask;
        };
        client.OnOutgoingFrame += async json =>
        {
            Console.WriteLine($"[OUT] {Shorten(json, 1000)}");
            await Task.CompletedTask;
        };
        // Start the client background loop
        client.Start();
        Console.WriteLine("Client started. It will fetch WS token and connect.");

        // Wait for connect ack (id==1) before auto-subscribing
        var connectTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task onFrameHandler(JsonElement f)
        {
            try
            {
                if (f.TryGetProperty("result", out var _) && f.TryGetProperty("id", out var idEl) && idEl.TryGetInt64(out var idVal) && idVal == 1)
                {
                    connectTcs.TrySetResult(true);
                }
            }
            catch { }
            return Task.CompletedTask;
        }

        client.OnFrame += onFrameHandler;

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine("Ctrl+C received, shutting down...");
        };

        // Optionally auto-subscribe to an orderbook channel after connect ack
        var autoSubscribe = true;
        var autoChannel = "public:orderbook-BTCIRT";

        try
        {
            // Wait for connect ack or timeout
            var ackTask = connectTcs.Task;
            var completed = await Task.WhenAny(ackTask, Task.Delay(TimeSpan.FromSeconds(30), cts.Token));

            if (completed == ackTask && ackTask.IsCompletedSuccessfully)
            {
                Console.WriteLine("Connect acknowledged by server.");
                if (autoSubscribe)
                {
                    await obHelper.StartListening(autoChannel, cts.Token);
                    Console.WriteLine($"Auto-subscribed to {autoChannel}");
                }
            }
            else
            {
                Console.WriteLine("Did not receive connect ack within timeout; you may still subscribe interactively.");
            }

            Console.WriteLine("Tip: try `subscribe public:orderbook-BTCIRT` once connected.");
            await RunInteractiveLoop(client, obHelper, cts.Token);
        }
        catch (OperationCanceledException) { /* graceful */ }
        finally
        {
            client.OnFrame -= onFrameHandler;
            Console.WriteLine("Cleaning up...");
            await obHelper.DisposeAsync();
            await client.DisposeAsync();
            Console.WriteLine("Done.");
        }

        return 0;
    }

    private static async Task RunInteractiveLoop(IWebsocketCentrifugoClient client, OrderbookSubscriptionHelper obHelper, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Console.Write("> ");
            var line = await ReadLineAsync(token);
            if (line is null) break;
            var parts = line.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            var cmd = parts[0].ToLowerInvariant();

            try
            {
                switch (cmd)
                {
                    case "exit":
                    case "quit":
                        return;

                    case "subscribe":
                        if (parts.Length < 2) { Console.WriteLine("Usage: subscribe <channel>"); break; }
                        await obHelper.StartListening(parts[1], token);
                        Console.WriteLine($"Subscribed to {parts[1]} (typed helper)");
                        break;

                    case "unsubscribe":
                        if (parts.Length < 2) { Console.WriteLine("Usage: unsubscribe <channel>"); break; }
                        await obHelper.StopListening(parts[1], token);
                        Console.WriteLine($"Unsubscribed from {parts[1]}");
                        break;

                    case "publish":
                        if (parts.Length < 3) { Console.WriteLine("Usage: publish <channel> <json-data>"); break; }
                        object data;
                        try
                        {
                            data = JsonDocument.Parse(parts[2]).RootElement.Clone();
                        }
                        catch
                        {
                            data = parts[2];
                        }
                        await client.PublishAsync(parts[1], data, token);
                        Console.WriteLine($"Published to {parts[1]}");
                        break;

                    case "help":
                        Console.WriteLine("Commands: subscribe <channel> | unsubscribe <channel> | publish <channel> <json> | exit");
                        break;

                    default:
                        Console.WriteLine("Unknown command. Type `help`.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Command failed: {ex.Message}");
            }
        }
    }

    private static Task<string?> ReadLineAsync(CancellationToken ct) =>
        Task.Run(() => Console.ReadLine(), ct);

    private static string Shorten(string s, int max)
    {
        if (s.Length <= max) return s;
        return s.Substring(0, max) + "...";
    }
}
