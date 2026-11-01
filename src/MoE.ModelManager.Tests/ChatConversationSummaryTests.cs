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

    [TestMethod]
    public void UpdateFrom_CopiesFieldsAndRaisesChanges()
    {
        DateTimeOffset now = DateTimeOffset.Now;
        ChatConversationSummary summary = new() { Id = "a", Title = "Old", ModelName = "Qwen", UpdatedAt = now.AddHours(-1) };
        ChatConversationSummary newer = new() { Id = "a", Title = "New", ModelName = "Gemma", UpdatedAt = now };
        List<string?> changed = [];
        summary.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        summary.UpdateFrom(newer);

        Assert.AreEqual("New", summary.Title);
        Assert.AreEqual("Gemma", summary.ModelName);
        Assert.AreEqual(now, summary.UpdatedAt);
        CollectionAssert.IsSubsetOf(new[] { "Title", "Caption", "UpdatedText" }, changed);
    }
}
