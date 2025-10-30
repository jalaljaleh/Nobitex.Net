namespace Nobitex.Net;
public class NobitexOptions
{
    public string BaseUrl { get; set; } = "https://apiv2.nobitex.ir";
    public string? Token { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public string? UserAgent { get; set; } = "Nobitex.Net/1.0";
}
