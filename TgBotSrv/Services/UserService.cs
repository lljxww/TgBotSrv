using System.Text.Json;
using TgBotSrv.Models;

namespace TgBotSrv.Services;

public class UserService
{
    private Dictionary<long, UserSettings> _userSettings;
    private const string SETTINGS_FILE = "user_settings.json";

    public UserService()
    {
        _userSettings = [];
        LoadSettings();
    }

    public UserSettings GetUserSettings(long userId)
    {
        if (!_userSettings.TryGetValue(userId, out UserSettings? value))
        {
            value = new UserSettings();
            _userSettings[userId] = value;
            SaveSettings();
        }
        return value;
    }

    public void UpdateUserSettings(long userId, UserSettings settings)
    {
        _userSettings[userId] = settings;
        SaveSettings();
    }

    public void AddMessageToHistory(long userId, string role, string content)
    {
        UserSettings settings = GetUserSettings(userId);
        settings.ChatHistory.Add(new ChatMessage { Role = role, Content = content });

        // 只保留最近的20条消息
        if (settings.ChatHistory.Count > 20)
        {
            settings.ChatHistory.RemoveAt(0);
        }

        SaveSettings();
    }

    public void AddRecord(long userId, string message)
    {
        var settings = GetUserSettings(userId);
        settings.Records.Add(message);
    }

    public List<string> GetRecords(long userId)
    {
        var settings = GetUserSettings(userId);
        return settings.Records;
    }

    public string GetRecord(long userId, int index)
    {
        var settings = GetUserSettings(userId);

        if (settings.Records.Count <= index)
        {
            return string.Empty;
        }

        return settings.Records[index];
    }

    public void ClearHistory(long userId)
    {
        UserSettings settings = GetUserSettings(userId);
        settings.ChatHistory.Clear();
        SaveSettings();
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(SETTINGS_FILE))
            {
                string json = File.ReadAllText(SETTINGS_FILE);
                Dictionary<long, UserSettings>? settings = JsonSerializer.Deserialize<Dictionary<long, UserSettings>>(json);
                if (settings != null)
                {
                    _userSettings = settings;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading settings: {ex.Message}");
        }
    }

    private void SaveSettings()
    {
        try
        {
            string json = JsonSerializer.Serialize(_userSettings);
            File.WriteAllText(SETTINGS_FILE, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings: {ex.Message}");
        }
    }
}