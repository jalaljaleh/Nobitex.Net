namespace Nobitex.Net;
public record Trade(long TimeUnixMillis, decimal Price, decimal Volume, string Type);
