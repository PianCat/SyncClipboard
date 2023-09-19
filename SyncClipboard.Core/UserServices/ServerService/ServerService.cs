using Microsoft.Extensions.DependencyInjection;
using SyncClipboard.Core.Commons;
using SyncClipboard.Core.Interfaces;
using SyncClipboard.Core.Models.UserConfigs;
using SyncClipboard.Core.Utilities.Notification;

namespace SyncClipboard.Core.UserServices;

public class ServerService : Service
{
    Microsoft.AspNetCore.Builder.WebApplication? app;
    public const string SERVICE_NAME = "同步服务器";
    public const string LOG_TAG = "INNERSERVER";

    private readonly ConfigManager _configManager;
    private ServerConfig _serverConfig = new();
    private readonly ToggleMenuItem _toggleMenuItem;

    private readonly IServiceProvider _serviceProvider;
    private readonly IContextMenu _contextMenu;
    private readonly ILogger _logger;
    private readonly ITrayIcon _trayIcon;

    private NotificationManager NotificationManager => _serviceProvider.GetRequiredService<NotificationManager>();

    public ServerService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _trayIcon = serviceProvider.GetRequiredService<ITrayIcon>();
        _logger = serviceProvider.GetRequiredService<ILogger>();
        _configManager = serviceProvider.GetRequiredService<ConfigManager>();
        _contextMenu = serviceProvider.GetRequiredService<IContextMenu>();
        _toggleMenuItem = new ToggleMenuItem(
            SERVICE_NAME,
            _serverConfig.SwitchOn,
            (status) =>
            {
                _configManager.SetConfig(ConfigKey.Server, _serverConfig with { SwitchOn = status });
            }
        );
    }

    protected override void StartService()
    {
        _configManager.ListenConfig<ServerConfig>(ConfigKey.Server, ConfigChanged);
        _serverConfig = _configManager.GetConfig<ServerConfig>(ConfigKey.Server) ?? new();
        _contextMenu.AddMenuItem(_toggleMenuItem, SyncService.ContextMenuGroupName);
        RestartServer();
    }

    private void ConfigChanged(object? config)
    {
        var newConfig = config as ServerConfig;
        if (newConfig != _serverConfig)
        {
            _serverConfig = newConfig ?? new();
            RestartServer();
        }
    }

    public async void RestartServer()
    {
        _toggleMenuItem.Checked = _serverConfig.SwitchOn;
        StopSerivce();
        if (_serverConfig.SwitchOn)
        {
            try
            {
                app = await Server.Program.StartAsync(
                    new Abstract.ServerPara(
                        _serverConfig.Port,
                        Env.AppDataDirectory,
                        _serverConfig.UserName,
                        _serverConfig.Password,
                        _serverConfig.ClientMixedMode,
                        _serviceProvider
                    )
                );
                _trayIcon.SetStatusString(SERVICE_NAME, "Running.", false);
            }
            catch (Exception ex)
            {
                _logger.Write(LOG_TAG, ex.ToString());
                _trayIcon.SetStatusString(SERVICE_NAME, ex.Message, true);
                NotificationManager.SendText("服务器启动失败", ex.Message);
            }
        }
    }

    protected override void StopSerivce()
    {
        _trayIcon.SetStatusString(SERVICE_NAME, "Stopped.");
        app?.StopAsync();
    }
}
