using System.Globalization;
using MoE.ModelManager.Models;
using MoE.ModelManager.Services;

namespace MoE.ModelManager.Tests;

[TestClass]
public sealed class ChatTranscriptFormatterTests
{
    [TestMethod]
    public void ToMarkdown_IncludesTitleAuthorsReasoningAndAttachments()
    {
        ChatConversation conversation = new()
        {
            Id = ChatHistoryService.CreateId(),
            Title = "GPU benchmark",
            ModelName = "Qwen3.6 35B A3B",
            CreatedAt = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero),
            Messages =
            [
                new StoredChatMessage { Role = "user", Author = "You", Content = "Summarize this.", AttachmentFileName = "bench.png" },
                new StoredChatMessage { Role = "assistant", Author = "Qwen3.6 35B A3B", Content = "Best is 41 TPS.", Reasoning = "Compare rows." },
            ],
        };

        string markdown = ChatTranscriptFormatter.ToMarkdown(conversation, CultureInfo.InvariantCulture);

        StringAssert.StartsWith(markdown, "# GPU benchmark");
        StringAssert.Contains(markdown, "## You");
        StringAssert.Contains(markdown, "> Attachment: bench.png");
        StringAssert.Contains(markdown, "## Qwen3.6 35B A3B");
        StringAssert.Contains(markdown, "<details><summary>Thinking</summary>");
        StringAssert.Contains(markdown, "Compare rows.");
        StringAssert.Contains(markdown, "Best is 41 TPS.");
    }

    [TestMethod]
    public void CreateTitle_ShortPrompt_ReturnsSingleLine()
    {
        string title = ChatTranscriptFormatter.CreateTitle("  Hello\n   world  ");

        Assert.AreEqual("Hello world", title);
    }

    [TestMethod]
    public void CreateTitle_LongPrompt_TruncatesAtWordBoundary()
    {
        string prompt = "Compare the KV cache memory for an eight thousand token context at f16 versus q8_0 quantization";

        string title = ChatTranscriptFormatter.CreateTitle(prompt, 40);

        Assert.IsLessThanOrEqualTo(40, title.Length);
        StringAssert.EndsWith(title, "…");
        Assert.IsFalse(title.Contains("  ", StringComparison.Ordinal));
    }

    [DataRow("Report: Q3/Q4 <draft>?", "Report Q3 Q4 draft")]
    [DataRow("   ", "Chat")]
    [DataRow("Plain title", "Plain title")]
    [TestMethod]
    public void CreateFileName_RemovesInvalidCharacters(string title, string expected)
    {
        Assert.AreEqual(expected, ChatTranscriptFormatter.CreateFileName(title));
    }
}
