using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using XelLauncher.Helpers;
using XelLauncher.Models;

namespace XelLauncher.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly AppConfig _config;
    private readonly Window _owner;
    private const double SwitchContentOffset = 180D;
    private const int BackgroundHoldFrameCount = 2;
    private const int SwitchAnimationFrameCount = 60;
    private string _statusMessage = "就绪";
    private GameEntryViewModel? _selectedGame;
    private double _windowWidth;
    private CancellationTokenSource? _switchCancellation;
    private int _switchVersion;

    public MainWindowViewModel(Window owner)
    {
        _owner = owner;
        _config = ConfigHelper.Load();
        _config.ThemeMode = "dark";
        if (_config.Games.Count == 0)
        {
            _config.Games = new AppConfig().Games;
        }

        Games = new ObservableCollection<GameEntryViewModel>(
            _config.Games.Select(x => new GameEntryViewModel(x, owner, _config, SaveConfigOnly, SelectGame)));
        Notices = new ObservableCollection<NoticeItemViewModel>();
        SaveCommand = new RelayCommand(Save);
        SelectInitialGame(Games.FirstOrDefault());
    }

    public ObservableCollection<GameEntryViewModel> Games { get; }
    public ObservableCollection<BackgroundTransitionViewModel> BackgroundLayers { get; } = [];
    public ObservableCollection<NoticeItemViewModel> Notices { get; }

    public string VersionLabel =>
        $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.3.0"}";

    public string ConfigPath => ConfigHelper.ConfigFile;

    public string AccountSummary =>
        $"方舟账号 {_config.Accounts.Count} 个，终末地账号 {_config.EndfieldAccounts.Count} 个，国际服账号 {_config.GlobalEndfieldAccounts.Count} 个。账号切换 UI 待迁移，已有配置不会被丢弃。";

    public GameEntryViewModel? SelectedGame
    {
        get => _selectedGame;
        set
        {
            if (value == null || ReferenceEquals(_selectedGame, value)) return;
            SelectGame(value);
        }
    }

    public string SelectedServerKind => SelectedGame?.ServerKind ?? "";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ThemeMode
    {
        get => _config.ThemeMode;
        set
        {
            if (value == "dark" && _config.ThemeMode == "dark") return;
            _config.ThemeMode = "dark";
            OnPropertyChanged();
        }
    }

    public bool CloseAfterLaunch
    {
        get => _config.CloseAfterLaunch;
        set
        {
            if (_config.CloseAfterLaunch == value) return;
            _config.CloseAfterLaunch = value;
            OnPropertyChanged();
        }
    }

    public bool UseExternalBrowser
    {
        get => _config.UseExternalBrowser;
        set
        {
            if (_config.UseExternalBrowser == value) return;
            _config.UseExternalBrowser = value;
            OnPropertyChanged();
        }
    }

    public bool CheckGameUpdates
    {
        get => _config.CheckGameUpdates;
        set
        {
            if (_config.CheckGameUpdates == value) return;
            _config.CheckGameUpdates = value;
            OnPropertyChanged();
        }
    }

    public bool ArchiveLauncherImages
    {
        get => _config.ArchiveLauncherImages;
        set
        {
            if (_config.ArchiveLauncherImages == value) return;
            _config.ArchiveLauncherImages = value;
            OnPropertyChanged();
        }
    }

    public RelayCommand SaveCommand { get; }

    private void SelectInitialGame(GameEntryViewModel? game)
    {
        if (game == null) return;

        foreach (var item in Games)
        {
            item.IsSelected = ReferenceEquals(item, game);
            item.ContentOpacity = ReferenceEquals(item, game) ? 1D : 0D;
            item.TopContentOffsetY = ReferenceEquals(item, game) ? 0D : -SwitchContentOffset;
            item.BottomContentOffsetY = ReferenceEquals(item, game) ? 0D : SwitchContentOffset;
        }

        SetProperty(ref _selectedGame, game, nameof(SelectedGame));
        OnPropertyChanged(nameof(SelectedServerKind));
        game.ApplyResponsiveNoticeCollapse(_windowWidth);
        _ = game.RefreshBackgroundAsync();
    }

    private void SelectGame(GameEntryViewModel? game)
    {
        if (game == null || ReferenceEquals(_selectedGame, game)) return;

        _switchCancellation?.Cancel();
        _switchCancellation?.Dispose();
        _switchCancellation = new CancellationTokenSource();
        var switchVersion = ++_switchVersion;
        _ = SwitchGameAsync(game, switchVersion, _switchCancellation.Token);
    }

    private async Task SwitchGameAsync(
        GameEntryViewModel game,
        int switchVersion,
        CancellationToken cancellationToken)
    {
        var oldGame = _selectedGame;
        if (oldGame == null) return;

        oldGame.ContentOpacity = 0D;
        oldGame.TopContentOffsetY = -SwitchContentOffset;
        oldGame.BottomContentOffsetY = SwitchContentOffset;

        for (var index = BackgroundLayers.Count - 1; index >= 0; index--)
        {
            if (BackgroundLayers[index].Opacity <= 0.001D)
            {
                BackgroundLayers.RemoveAt(index);
            }
        }

        BackgroundLayers.Insert(0, new BackgroundTransitionViewModel(oldGame));
        var fadingLayers = BackgroundLayers.ToArray();
        var startingOpacities = fadingLayers.Select(layer => layer.Opacity).ToArray();

        foreach (var item in Games)
        {
            item.IsSelected = ReferenceEquals(item, game);
        }

        game.ContentOpacity = 0D;
        game.TopContentOffsetY = -SwitchContentOffset;
        game.BottomContentOffsetY = SwitchContentOffset;
        SetProperty(ref _selectedGame, game, nameof(SelectedGame));
        OnPropertyChanged(nameof(SelectedServerKind));
        game.ApplyResponsiveNoticeCollapse(_windowWidth);
        _ = game.RefreshBackgroundAsync();

        try
        {
            await WaitForAnimationFramesAsync(BackgroundHoldFrameCount, cancellationToken);
            await AnimateFramesAsync(
                SwitchAnimationFrameCount,
                progress =>
                {
                    var easedProgress = EaseOutCubic(progress);
                    for (var index = 0; index < fadingLayers.Length; index++)
                    {
                        fadingLayers[index].Opacity = startingOpacities[index] * (1D - easedProgress);
                    }

                    game.ContentOpacity = easedProgress;
                    game.TopContentOffsetY = -SwitchContentOffset * (1D - easedProgress);
                    game.BottomContentOffsetY = SwitchContentOffset * (1D - easedProgress);
                },
                cancellationToken);

            if (switchVersion == _switchVersion)
            {
                BackgroundLayers.Clear();
                game.ContentOpacity = 1D;
                game.TopContentOffsetY = 0D;
                game.BottomContentOffsetY = 0D;
            }
        }
        catch (OperationCanceledException)
        {
            // A newer selection keeps the current visual state and continues from it.
        }
    }

    private async Task WaitForAnimationFramesAsync(int frameCount, CancellationToken cancellationToken)
    {
        for (var frame = 0; frame < frameCount; frame++)
        {
            await NextAnimationFrameAsync(cancellationToken);
        }
    }

    private async Task AnimateFramesAsync(
        int frameCount,
        Action<double> update,
        CancellationToken cancellationToken)
    {
        for (var frame = 1; frame <= frameCount; frame++)
        {
            await NextAnimationFrameAsync(cancellationToken);
            update(frame / (double)frameCount);
        }
    }

    private Task<TimeSpan> NextAnimationFrameAsync(CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = cancellationToken.Register(
            () => completion.TrySetCanceled(cancellationToken));

        _owner.RequestAnimationFrame(timestamp =>
        {
            registration.Dispose();
            completion.TrySetResult(timestamp);
        });

        return completion.Task;
    }

    private static double EaseOutCubic(double value)
    {
        var inverse = 1D - Math.Clamp(value, 0D, 1D);
        return 1D - inverse * inverse * inverse;
    }

    private void Save()
    {
        try
        {
            SaveConfigOnly();
            StatusMessage = $"已保存 {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex, "ConfigSave");
            StatusMessage = "保存失败，详见日志";
        }
    }

    private void SaveConfigOnly()
    {
        ConfigHelper.Save(_config);
    }

    public void UpdateWindowWidth(double width)
    {
        _windowWidth = width;
        SelectedGame?.ApplyResponsiveNoticeCollapse(width);
    }
}

public sealed class BackgroundTransitionViewModel : ViewModelBase
{
    private double _opacity = 1D;

    public BackgroundTransitionViewModel(GameEntryViewModel game)
    {
        Game = game;
    }

    public GameEntryViewModel Game { get; }

    public double Opacity
    {
        get => _opacity;
        set => SetProperty(ref _opacity, value);
    }
}

public sealed class NoticeItemViewModel
{
    private readonly string _jumpUrl;

    public NoticeItemViewModel(string category, string title, string date, string jumpUrl = "")
    {
        Category = string.IsNullOrWhiteSpace(category) ? "\u516c\u544a" : category.Trim();
        Title = title;
        Date = date;
        _jumpUrl = jumpUrl;
        OpenCommand = new RelayCommand(Open, () => !string.IsNullOrWhiteSpace(_jumpUrl));
    }

    public string Category { get; }
    public string CategoryLabel => string.Concat("[", Category, "]");
    public string Title { get; }
    public string Date { get; }
    public RelayCommand OpenCommand { get; }

    private void Open()
    {
        if (string.IsNullOrWhiteSpace(_jumpUrl)) return;
        Process.Start(new ProcessStartInfo(_jumpUrl) { UseShellExecute = true });
    }
}
