using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TgBotSrv.Models;

namespace TgBotSrv.Services;

public class CommandService(UserService userService, ITelegramBotClient botClient)
{
    private readonly UserService _userService = userService;
    private readonly ITelegramBotClient _botClient = botClient;

    public async Task HandleCommand(Message message, CancellationToken cancellationToken)
    {
        string? command = message.Text?.Split(' ')[0].ToLower();

        switch (command)
        {
            case "/start":
                await HandleStartCommand(message, cancellationToken);
                break;
            case "/help":
                await HandleHelpCommand(message, cancellationToken);
                break;
            case "/clear":
                await HandleClearCommand(message, cancellationToken);
                break;
            case "/settings":
                await HandleSettingsCommand(message, cancellationToken);
                break;
            case "/language":
                await HandleLanguageCommand(message, cancellationToken);
                break;
            case "/record":
                await HandleRecordCommand(message, cancellationToken);
                break;
            case "/getrecords":
                await HandleGetRecordsCommand(message, cancellationToken);
                break;
            default:
                await _botClient.SendMessage(
                    chatId: message.Chat.Id,
                    text: "未知命令。使用 /help 查看可用命令。",
                    cancellationToken: cancellationToken);
                break;
        }
    }

    private async Task HandleGetRecordsCommand(Message message, CancellationToken cancellationToken)
    {
        long userId = message.From?.Id ?? 0;
        var records = _userService.GetRecords(userId);

        if (records.Count == 0)
        {
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "未获取到任何信息, 使用/record进行记录, 例如：/record 一只棕色的狐狸",
                cancellationToken: cancellationToken);

            return;
        }

        var responseMessage = new StringBuilder($"已记录的信息如下:{Environment.NewLine}");

        int i = 1;
        foreach (var record in records)
        {
            responseMessage.Append($"{i++} - {record}{Environment.NewLine}");
        }

        await _botClient.SendMessage(
            chatId: message.Chat.Id,
            text: responseMessage.ToString().TrimEnd('\r', '\n'),
            cancellationToken: cancellationToken);
    }

    private async Task HandleRecordCommand(Message message, CancellationToken cancellationToken)
    {
        long userId = message.From?.Id ?? 0;

        string[]? args = message.Text?.Split(' ');
        if (args?.Length != 2)
        {
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "请给出要记录的信息，例如：/record 一只棕色的狐狸",
                cancellationToken: cancellationToken);
            return;
        }

        _userService.AddRecord(userId, args[1]);

        await _botClient.SendMessage(
            chatId: message.Chat.Id,
            text: "✅ 记录成功, 使用 /getrecords 获取所有信息",
            cancellationToken: cancellationToken);
    }

    private async Task HandleStartCommand(Message message, CancellationToken cancellationToken)
    {
        string welcomeMessage = @"👋 欢迎使用AI助手！

使用以下命令：
/help - 显示帮助信息
/clear - 清除对话历史
/settings - 查看当前设置
/language - 设置语言
/record - 记录信息
/getrecords - 查询所有已记录的信息

直接发送消息即可开始对话！";

        await _botClient.SendMessage(
            chatId: message.Chat.Id,
            text: welcomeMessage,
            parseMode: ParseMode.Html,
            cancellationToken: cancellationToken);
    }

    private async Task HandleHelpCommand(Message message, CancellationToken cancellationToken)
    {
        string helpMessage = @"📚 命令列表：

/start - 显示欢迎信息
/help - 显示此帮助信息
/clear - 清除对话历史
/settings - 查看当前设置
/language - 设置语言
/record - 记录信息
/getrecords - 查询所有已记录的信息

💡 提示：直接发送消息即可与AI对话！";

        await _botClient.SendMessage(
            chatId: message.Chat.Id,
            text: helpMessage,
            parseMode: ParseMode.Html,
            cancellationToken: cancellationToken);
    }

    private async Task HandleClearCommand(Message message, CancellationToken cancellationToken)
    {
        long userId = message.From?.Id ?? 0;
        _userService.ClearHistory(userId);

        await _botClient.SendMessage(
            chatId: message.Chat.Id,
            text: "✅ 对话历史已清除！",
            cancellationToken: cancellationToken);
    }

    private async Task HandleSettingsCommand(Message message, CancellationToken cancellationToken)
    {
        long userId = message.From?.Id ?? 0;
        UserSettings settings = _userService.GetUserSettings(userId);

        string settingsMessage = $@"⚙️ 当前设置：

语言：{settings.Language}
温度：{settings.Temperature}
最大回复长度：{settings.MaxTokens}
回复风格：{settings.ResponseStyle}

使用 /language 命令可以更改语言设置。";

        await _botClient.SendMessage(
            chatId: message.Chat.Id,
            text: settingsMessage,
            parseMode: ParseMode.Html,
            cancellationToken: cancellationToken);
    }

    private async Task HandleLanguageCommand(Message message, CancellationToken cancellationToken)
    {
        string[]? args = message.Text?.Split(' ');
        if (args?.Length != 2)
        {
            await _botClient.SendMessage(
                chatId: message.Chat.Id,
                text: "请指定语言代码，例如：/language zh-CN",
                cancellationToken: cancellationToken);
            return;
        }

        long userId = message.From?.Id ?? 0;
        UserSettings settings = _userService.GetUserSettings(userId);
        settings.Language = args[1];
        _userService.UpdateUserSettings(userId, settings);

        await _botClient.SendMessage(
            chatId: message.Chat.Id,
            text: $"✅ 语言已设置为：{settings.Language}",
            cancellationToken: cancellationToken);
    }
}