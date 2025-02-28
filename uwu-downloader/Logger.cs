﻿using Discord.Webhook;

namespace uwu_downloader;

public static class Logger
{
    private static Configuration _config = null!;
    private static DiscordWebhookClient _webhook = null!;

    public static void Initialize(Configuration configuration)
    {
        _config = configuration;

        var webhookUrl = _config.Get("WEBHOOK_URL");
        
        if (string.IsNullOrEmpty(webhookUrl))
        {
            Logger.Info("No webhook URL provided.");
            return;
        }
        
        _webhook = new DiscordWebhookClient(webhookUrl);
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