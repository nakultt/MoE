using System.Globalization;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Tests;

[TestClass]
public sealed class ChatConversationSummaryTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    [TestMethod]
    public void FormatRelative_Today_ReturnsTime()
    {
        DateTimeOffset now = new(2026, 11, 1, 15, 0, 0, TimeSpan.Zero);
        DateTimeOffset value = now.AddHours(-1);

        string text = ChatConversationSummary.FormatRelative(value, now, Culture);

        Assert.AreEqual(value.ToLocalTime().DateTime.ToString("t", Culture), text);
    }

    [TestMethod]
    public void FormatRelative_ThisWeek_ReturnsWeekday()
    {
        DateTimeOffset now = new(2026, 11, 1, 15, 0, 0, TimeSpan.Zero);
        DateTimeOffset value = now.AddDays(-3);

        string text = ChatConversationSummary.FormatRelative(value, now, Culture);

        Assert.AreEqual(value.ToLocalTime().DateTime.ToString("ddd", Culture), text);
    }

    [TestMethod]
    public void FormatRelative_Older_ReturnsShortDate()
    {
        DateTimeOffset now = new(2026, 11, 1, 15, 0, 0, TimeSpan.Zero);
        DateTimeOffset value = now.AddDays(-30);

        string text = ChatConversationSummary.FormatRelative(value, now, Culture);

        Assert.AreEqual(value.ToLocalTime().DateTime.ToString("d", Culture), text);
    }
}
