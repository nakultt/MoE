using MoE.ModelManager.Models;
using MoE.ModelManager.Services;

namespace MoE.ModelManager.Tests;

[TestClass]
public sealed class ChatHistoryServiceTests
{
    [TestMethod]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsConversation()
    {
        using TemporaryDirectory directory = new();
        ChatHistoryService service = new(directory.Path);
        ChatConversation conversation = CreateConversation("First chat", DateTimeOffset.UtcNow);

        await service.SaveAsync(conversation, CancellationToken.None);
        ChatConversation? loaded = await service.LoadAsync(conversation.Id, CancellationToken.None);

        Assert.IsNotNull(loaded);
        Assert.AreEqual("First chat", loaded.Title);
        Assert.AreEqual("Qwen3.6 35B A3B", loaded.ModelName);
        Assert.HasCount(2, loaded.Messages);
        Assert.AreEqual("Hello", loaded.Messages[0].Content);
        Assert.AreEqual("Thinking", loaded.Messages[1].Reasoning);
    }

    [TestMethod]
    public async Task ListAsync_ReturnsNewestFirst()
    {
        using TemporaryDirectory directory = new();
        ChatHistoryService service = new(directory.Path);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await service.SaveAsync(CreateConversation("Older", now.AddHours(-2)), CancellationToken.None);
        await service.SaveAsync(CreateConversation("Newer", now), CancellationToken.None);

        IReadOnlyList<ChatConversationSummary> summaries = await service.ListAsync(CancellationToken.None);

        Assert.HasCount(2, summaries);
        Assert.AreEqual("Newer", summaries[0].Title);
        Assert.AreEqual("Older", summaries[1].Title);
    }

    [TestMethod]
    public async Task ListAsync_SkipsDamagedFiles()
    {
        using TemporaryDirectory directory = new();
        ChatHistoryService service = new(directory.Path);
        await service.SaveAsync(CreateConversation("Valid", DateTimeOffset.UtcNow), CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(directory.Path, $"{ChatHistoryService.CreateId()}.json"), "{ not json");

        IReadOnlyList<ChatConversationSummary> summaries = await service.ListAsync(CancellationToken.None);

        Assert.HasCount(1, summaries);
        Assert.AreEqual("Valid", summaries[0].Title);
    }

    [TestMethod]
    public async Task ListAsync_MissingDirectory_ReturnsEmpty()
    {
        ChatHistoryService service = new(Path.Combine(Path.GetTempPath(), $"moe-missing-{Guid.NewGuid():N}"));

        IReadOnlyList<ChatConversationSummary> summaries = await service.ListAsync(CancellationToken.None);

        Assert.HasCount(0, summaries);
    }

    [TestMethod]
    public async Task DeleteAsync_RemovesOnlyThatConversation()
    {
        using TemporaryDirectory directory = new();
        ChatHistoryService service = new(directory.Path);
        ChatConversation keep = CreateConversation("Keep", DateTimeOffset.UtcNow);
        ChatConversation remove = CreateConversation("Remove", DateTimeOffset.UtcNow);
        await service.SaveAsync(keep, CancellationToken.None);
        await service.SaveAsync(remove, CancellationToken.None);

        await service.DeleteAsync(remove.Id, CancellationToken.None);

        Assert.IsNull(await service.LoadAsync(remove.Id, CancellationToken.None));
        Assert.IsNotNull(await service.LoadAsync(keep.Id, CancellationToken.None));
    }

    [TestMethod]
    public async Task DeleteAllAsync_RemovesEveryConversationButNotOtherFiles()
    {
        using TemporaryDirectory directory = new();
        ChatHistoryService service = new(directory.Path);
        await service.SaveAsync(CreateConversation("One", DateTimeOffset.UtcNow), CancellationToken.None);
        await service.SaveAsync(CreateConversation("Two", DateTimeOffset.UtcNow), CancellationToken.None);
        string unrelated = Path.Combine(directory.Path, "notes.json");
        await File.WriteAllTextAsync(unrelated, "{}");

        await service.DeleteAllAsync(CancellationToken.None);

        Assert.HasCount(0, await service.ListAsync(CancellationToken.None));
        Assert.IsTrue(File.Exists(unrelated));
    }

    [DataRow("../../escape")]
    [DataRow("C:\\Windows\\win")]
    [DataRow("not-a-guid")]
    [DataRow("")]
    [TestMethod]
    public async Task LoadAsync_InvalidId_Throws(string id)
    {
        using TemporaryDirectory directory = new();
        ChatHistoryService service = new(directory.Path);

        await Assert.ThrowsAsync<ArgumentException>(() => service.LoadAsync(id, CancellationToken.None));
    }

    [TestMethod]
    public void CreateId_ReturnsValidId()
    {
        string id = ChatHistoryService.CreateId();

        Assert.IsTrue(ChatHistoryService.IsValidId(id));
    }

    private static ChatConversation CreateConversation(string title, DateTimeOffset updatedAt)
    {
        return new ChatConversation
        {
            Id = ChatHistoryService.CreateId(),
            Title = title,
            ModelId = "qwen3.6-35b-a3b",
            ModelName = "Qwen3.6 35B A3B",
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt,
            Messages =
            [
                new StoredChatMessage { Role = "user", Author = "You", Content = "Hello", Timestamp = updatedAt },
                new StoredChatMessage
                {
                    Role = "assistant",
                    Author = "Qwen3.6 35B A3B",
                    Content = "Hi there",
                    Reasoning = "Thinking",
                    ModelId = "qwen3.6-35b-a3b",
                    Timestamp = updatedAt,
                },
            ],
        };
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"moe-history-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
