using System.Text;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

Console.WriteLine("Bot is starting...");

DotNetEnv.Env.Load();

const string DEEPSEEK_API_URL = "https://api.deepseek.com/v1/chat/completions";

string BOT_TOKEN = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")!;
string DEEPSEEK_API_KEY = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")!;

TelegramBotClient botClient = new(BOT_TOKEN);
using CancellationTokenSource cts = new();

ReceiverOptions receiverOptions = new()
{
    AllowedUpdates = []
};

User me = await botClient.GetMe();
Console.WriteLine($"Bot @{me.Username} is running!");

botClient.StartReceiving(
    updateHandler: HandleUpdateAsync,
    errorHandler: HandleErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);

while (true)
{
    Console.WriteLine("Type \"exit\" to exit...");
    if (Console.ReadLine() == "exit")
    {
        cts.Cancel();
        break;
    }
}

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    // 只处理文本消息
    if (update.Message is not { } message || message.Text is not { } messageText)
    {
        return;
    }

    long chatId = message.Chat.Id;
    string userName = message.From?.FirstName ?? "User";

    Console.WriteLine($"Received '{messageText}' from {userName} in chat {chatId}");

    await ResponseByDeepSeek(chatId, messageText, cancellationToken);
}

async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    await Task.Run(() =>
    {
        Console.WriteLine($"Error occurred: {exception.Message}");
    }, cancellationToken);
}

async Task ResponseByDeepSeek(long chatId, string messageText, CancellationToken cancellationToken)
{
    try
    {
        Message thinkingMessage = await botClient.SendMessage(chatId: chatId, text: "🤔正在思考...", cancellationToken: cancellationToken);

        string deepSeekResponse = await CallDeepSeekApi(messageText);

        Console.WriteLine($"ChatId: {chatId}, DeepSeek: {deepSeekResponse}");

        await botClient.DeleteMessage(chatId: chatId, messageId: thinkingMessage.MessageId, cancellationToken: cancellationToken);

        await botClient.SendMessage(chatId: chatId, text: deepSeekResponse, cancellationToken: cancellationToken);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        await botClient.SendMessage(
            chatId: chatId,
            text: "抱歉，处理您的请求时出错了。",
            cancellationToken: cancellationToken);
    }
}

async Task<string> CallDeepSeekApi(string userMessage)
{
    using HttpClient httpClient = new();

    var requestData = new
    {
        model = "deepseek-chat",
        messages = new[]
        {
            new { role = "user", content = userMessage }
        },
        temperature = 0.7,
        max_tokens = 2000
    };

    StringContent content = new(
        JsonSerializer.Serialize(requestData),
        Encoding.UTF8,
        "application/json");

    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {DEEPSEEK_API_KEY}");

    HttpResponseMessage response = await httpClient.PostAsync(DEEPSEEK_API_URL, content);
    response.EnsureSuccessStatusCode();

    string responseContent = await response.Content.ReadAsStringAsync();
    using JsonDocument jsonDoc = JsonDocument.Parse(responseContent);

    return jsonDoc.RootElement
        .GetProperty("choices")[0]
        .GetProperty("message")
        .GetProperty("content")
        .GetString() ?? "未能获取有效回复";
}
