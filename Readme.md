# Nobitex.Net

> کلاینت کامل، مدرن و کامنت‌گذاری‌شدهٔ **C# / .NET** برای اتصال به **REST API** و **وب‌سوکت** صرافی [نوبیتکس](https://nobitex.ir) — ساخته‌شده برای ترید خودکار (Algorithmic Trading).

[![NuGet](https://img.shields.io/nuget/v/Nobitex.Net.svg)](https://www.nuget.org/packages/Nobitex.Net/)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0-512BD4)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Docs](https://img.shields.io/badge/API%20Docs-nobitex.ir-blue)](https://old-apidocs.nobitex.ir/)

---

## ✨ امکانات

- **REST کامل**: آمار بازار، اردربوک (v3)، معاملات، کندل‌ها، سفارش‌ها، کیف پول، واریز/برداشت، پروفایل، کلیدهای API و معاملات تعهدی (Margin).
- **وب‌سوکت رسمی (Centrifugo)**: کانال‌های عمومی و خصوصی، اتصال مجدد خودکار، پینگ/پنگ خودکار، Resubscribe.
- **دو روش احراز هویت**: توکن (`Authorization: Token …`) و **کلید API با امضای Ed25519** (`Nobitex-Key` / `Nobitex-Signature` / `Nobitex-Timestamp`).
- **مدیریت محدودیت نرخ**: رعایت خودکار `backOff` در خطای `TooManyRequests` (429) + Retry با backoff نمایی برای 5xx.
- **مدل‌های strongly-typed**: تمام پاسخ‌ها به `record`/`class` تایپ‌شده تبدیل می‌شوند، مقادیر پولی به‌صورت `decimal` (ارسال به صورت string برای حفظ دقت).
- **مدیریت خطای استاندارد**: سلسله‌مراتب `NobitexException` با کد خطا (`NobitexErrorCode`).
- **پشتیبانی از Testnet**، `ILogger`, `CancellationToken`, `IHttpClientFactory` و DI.

## 📦 نصب

```bash
dotnet add package Nobitex.Net
```

```xml
<PackageReference Include="Nobitex.Net" Version="2.0.0" />
```

## 🚀 شروع سریع

```csharp
using Nobitex.Net;
using Nobitex.Net.Models;

var options = new NobitexClientOptions
{
    ApiToken = Environment.GetEnvironmentVariable("NOBITEX_TOKEN"),
    UserAgent = "TraderBot/MyBot"
};

await using var client = new NobitexClient(options);

// ۱) دادهٔ بازار (عمومی، بدون احراز هویت)
var stats = await client.MarketData.GetMarketStatsAsync("btc", "rls");
Console.WriteLine($"BTC: {stats.Stats["btc-rls"].BestSell:N0} ریال");

// ۲) اردربوک لحظه‌ای
var book = await client.MarketData.GetOrderBookAsync("BTCIRT", 50);

// ۳) ثبت سفارش خرید لیمیت
var order = await client.Trading.PlaceOrderAsync(new PlaceOrderRequest
{
    Side       = OrderSide.Buy,
    Execution  = OrderExecution.Limit,
    SrcCurrency = "btc",
    DstCurrency = "rls",
    Amount     = 0.001m,
    Price      = 4_500_000_000m,
    ClientOrderId = "my-bot-order-1"
});

Console.WriteLine($"سفارش #{order.Id} با وضعیت {order.Status}");
```

### استفاده از کلید API (توصیه‌شده)

```csharp
var options = new NobitexClientOptions
{
    ApiKey = new NobitexApiKeyCredentials(
        publicKeyBase64:  Environment.GetEnvironmentVariable("NOBITEX_KEY")!,
        privateKeyBase64: Environment.GetEnvironmentVariable("NOBITEX_SECRET")!)
};
```

### وب‌سوکت

```csharp
await using var socket = new NobitexSocketClient(new NobitexClientOptions
{
    ApiToken = Environment.GetEnvironmentVariable("NOBITEX_TOKEN")
});

await socket.ConnectAsync();

socket.OrderBookUpdated += (s, e) =>
    Console.WriteLine($"{e.Symbol} best bid={e.OrderBook.BestBid} best ask={e.OrderBook.BestAsk}");

await socket.SubscribeOrderBookAsync("BTCIRT");
await socket.SubscribePrivateOrdersAsync();   // نیاز به توکن
```

## 🧭 ساختار پروژه

```
Nobitex.Net/
├── src/Nobitex.Net/
│   ├── NobitexClient.cs                # هسته: امضا، Retry، Rate Limit، envelope
│   ├── NobitexClientOptions.cs         # تنظیمات (Token / ApiKey / Testnet / Retry)
│   ├── Apis/
│   │   ├── MarketDataApi.cs            # stats, orderbook v3, trades, candles
│   │   ├── TradingApi.cs               # ثبت/مشاهده/لغو سفارش، batch، OCO
│   │   ├── WalletApi.cs                # موجودی، واریز، برداشت، convert
│   │   ├── AccountApi.cs               # login، profile، apikeys، ws token
│   │   └── MarginApi.cs                # معاملات تعهدی و پوزیشن‌ها
│   ├── Authentication/                 # کلید API + امضای Ed25519
│   ├── WebSocket/                      # کلاینت Centrifugo، کانال‌ها، LocalOrderBook
│   ├── Models/                         # Enumها و مدل‌های strongly-typed
│   ├── Serialization/                  # JsonOptions و Converterها
│   ├── Exceptions/                     # سلسله‌مراتب خطاها + کدها
│   └── Internal/                       # RateLimiter، QueryBuilder
├── samples/Program.cs                  # بات نمونه کامل
├── tests/Nobitex.Net.Tests/            # xUnit با HttpHandler جعلی
└── .github/workflows/ci.yml
```

## ⚠️ نکات مهم

| موضوع | توضیح |
|---|---|
| واحد قیمت | بازارهای ریالی به **ریال** است (نه تومان) |
| مقدار (amount) | بر حسب `srcCurrency` |
| محدودیت سفارش | **۳۰۰ درخواست در ۱۰ دقیقه** (مشترک بین اسپات و تعهدی) |
| حداقل ارزش معامله | ۳ میلیون ریال / ۱۱ تتر |
| `clientOrderId` | برای هر کاربر بین سفارش‌های open یکتا است (حداکثر ۳۲ کاراکتر) |
| IP | استفاده از API نیازمند IP ایران است |

## 🧪 Testnet

```csharp
var options = NobitexClientOptions.ForTestnet(apiToken: "...");
```

## 📄 License

MIT © 2026 — این کتابخانه وابسته/تأییدشدهٔ رسمی نوبیتکس نیست.