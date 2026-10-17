using System.Net;
using GB.MikroAdmin.Core.Services;
using Xunit;

namespace GB.MikroAdmin.Tests;

public sealed class TicketHtmlWriterTests
{
    [Fact]
    public void Html_shows_network_price_and_duration_next_to_the_qr()
    {
        var batch = new TicketBatchExport(
            new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero),
            "limitado", TimeSpan.FromHours(1),
            [new CreatedHotspotUser("1249904", "1249904")],
            Brand: "NextGen", Ssid: "GB_nextGen_PA01", Price: 15m);

        var html = TicketHtmlWriter.ToHtml(batch);

        Assert.Contains("Red: GB_nextGen_PA01", html);
        Assert.Contains(WebUtility.HtmlEncode("$ 15.00 · Duración: 1 h"), html);
        Assert.Contains("data:image/png;base64,", html);
    }

    [Fact]
    public void Html_shows_duration_even_without_optional_data()
    {
        var batch = new TicketBatchExport(
            DateTimeOffset.Now, "limitado", TimeSpan.FromMinutes(30),
            [new CreatedHotspotUser("1249904", "1249904")]);

        var html = TicketHtmlWriter.ToHtml(batch);

        Assert.DoesNotContain("Red:", html);
        Assert.DoesNotContain("$ ", html);
        Assert.Contains(WebUtility.HtmlEncode("Duración: 30 min"), html);
    }
}
