using Discord.Webhook;

namespace uwu_downloader;

public static class Logger
{
    private static Configuration _config = null!;
    private static DiscordWebhookClient _webhook = null!;

    public static void Initialize(Configuration configuration)
    {
        _config = configuration;
        
        _webhook = new DiscordWebhookClient(_config.Get("WEBHOOK_URL"));
    }
    
    public static void Info(string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
    }
    
    public static async Task WebhookInfo(string message)
    {
        await _webhook.SendMessageAsync($"[{DateTime.Now:HH:mm:ss}] {message}");
    }
}