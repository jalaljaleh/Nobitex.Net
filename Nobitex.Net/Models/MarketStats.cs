using System.Collections.Generic;

namespace Nobitex.Net;
public record MarketStat(
    bool IsClosed,
    string BestSell,
    string BestBuy,
    string VolumeSrc,
    string VolumeDst,
    string Latest,
    string Mark,
    string DayLow,
    string DayHigh,
    string DayOpen,
    string DayClose,
    string DayChange
);

public record MarketStats(string Status, IDictionary<string, MarketStat> Stats);
