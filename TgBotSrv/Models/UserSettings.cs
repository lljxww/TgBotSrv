namespace TgBotSrv.Models;

public class UserSettings
{
    public string Language { get; set; } = "zh-CN";
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2000;
    public string ResponseStyle { get; set; } = "default";
    public List<ChatMessage> ChatHistory { get; set; } = [];
}

public class ChatMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}