using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using XelLauncher.Helpers;
using XelLauncher.Models;

namespace XelLauncher.ViewModels;

public sealed class GameEntryViewModel : ViewModelBase
{
    private const double NoticeCollapseBeforeTouchWidth = 1230D;
    private const double NoticeRestoreExpandedWidth = 1280D;
    private readonly GameEntry _entry;
    private readonly Window _owner;
    private readonly AppConfig _config;
    private readonly Action _save;
    private readonly Action<GameEntryViewModel> _select;
    private bool _isSelected;
    private double _topContentOffsetY;
    private double _bottomContentOffsetY;
    private double _contentOpacity;
    private bool _areNoticesCollapsed;
    private bool _areNoticesResponsiveCollapsed;
    private readonly List<LauncherBannerAsset> _bannerItems = [];
    private readonly DispatcherTimer _bannerTimer;
    private int _selectedBannerIndex;
    private bool _isBannerAnimating;
    private bool _isBannerDragging;
    private int _bannerAnimationVersion;
    private int _bannerAnimationCommitDirection;
    private int _incomingBannerIndex = -1;
    private int _bannerDragDirection;
    private double _bannerViewportWidth = 280D;
    private double _bannerOffsetX;
    private double _incomingBannerOffsetX;
    private bool _isIncomingBannerVisible;
    private string _statusMessage = "";
    private readonly List<NoticeItemViewModel> _allNotices = [];
    private string _selectedNoticeCategory = "";
    private Bitmap? _backgroundBitmap;
    private Task? _backgroundRefreshTask;
    private double _sidebarDragOpacity = 1D;
    private double _sidebarDragOffsetX;
    private double _sidebarDragOffsetY;
    private double _sidebarDragSurfaceOpacity;
    private double _sidebarDropBeforeOpacity;
    private double _sidebarDropAfterOpacity;

    public GameEntryViewModel(GameEntry entry, Window owner, AppConfig config, Action save, Action<GameEntryViewModel> select)
    {
        _entry = entry;
        _owner = owner;
        _config = config;
        _save = save;
        _select = select;
        IconBitmap = LoadAssetBitmap($"avares://XelLauncher/Resources/Icon/Generated/{_entry.IconName}.png");
        LogoBitmap = IconBitmap;
        _backgroundBitmap = LoadBitmap(FindCachedCover(_entry.IconName)) ?? LoadAssetBitmap(GetFallbackCoverUri());
        BannerBitmap = LoadBitmap(FindCachedBanner(_entry.IconName)) ?? BackgroundBitmap;
        Notices = [];
        NoticeTabs = [];
        SelectCommand = new RelayCommand(() => _select(this));
        BrowseCommand = new RelayCommand(Browse);
        OpenFolderCommand = new RelayCommand(OpenFolder, () => Directory.Exists(RootPath));
        LaunchCommand = new RelayCommand(Launch, () => Directory.Exists(RootPath));
        ToggleNoticesCommand = new RelayCommand(ToggleNotices);
        PreviousBannerCommand = new RelayCommand(SelectPreviousBanner, () => CanSwitchBannerInteractively);
        NextBannerCommand = new RelayCommand(SelectNextBanner, () => CanSwitchBannerInteractively);
        OpenBannerCommand = new RelayCommand(OpenSelectedBanner, CanOpenSelectedBanner);
        BannerDots = [];
        _bannerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4.2) };
        _bannerTimer.Tick += (_, _) => SelectNextBanner();
        if (!ApplyConfiguredLauncherNotice())
        {
            LoadCachedLauncherNotice();
        }
        _ = RefreshLauncherNoticeAsync();
    }

    public string DisplayName => _entry.IconName switch
    {
        "Arknights" => "明日方舟（官服）",
        "BiliArknights" => "明日方舟（B服）",
        "Endfield" => "明日方舟：终末地（官服）",
        "BiliEndfield" => "明日方舟：终末地（B服）",
        "GlobalEndfield" => "明日方舟：终末地（国际服）",
        "PlayEndfield" => "明日方舟：终末地（Google Play）",
        _ => string.IsNullOrWhiteSpace(_entry.Name) ? _entry.IconName : _entry.Name,
    };

    public string ServerKind => _entry.IconName.Contains("Bili", StringComparison.OrdinalIgnoreCase)
        ? "Bilibili"
        : _entry.IconName.Contains("Global", StringComparison.OrdinalIgnoreCase) ||
          _entry.IconName.Contains("Play", StringComparison.OrdinalIgnoreCase)
            ? "Global"
            : "Official";

    public string IconName => _entry.IconName;

    internal GameEntry Entry => _entry;

    public Bitmap? IconBitmap { get; }

    public Bitmap? LogoBitmap { get; }

    public Bitmap? BackgroundBitmap => _backgroundBitmap;

    public Bitmap? BannerBitmap { get; private set; }

    public Bitmap? IncomingBannerBitmap { get; private set; }

    public double BannerOffsetX
    {
        get => _bannerOffsetX;
        private set => SetProperty(ref _bannerOffsetX, value);
    }

    public double IncomingBannerOffsetX
    {
        get => _incomingBannerOffsetX;
        private set => SetProperty(ref _incomingBannerOffsetX, value);
    }

    public bool IsIncomingBannerVisible
    {
        get => _isIncomingBannerVisible;
        private set => SetProperty(ref _isIncomingBannerVisible, value);
    }

    public bool IsEndfield => _entry.IconName is "Endfield" or "BiliEndfield" or "GlobalEndfield" or "PlayEndfield";

    public ObservableCollection<NoticeItemViewModel> Notices { get; }

    public ObservableCollection<NoticeTabViewModel> NoticeTabs { get; }

    public ObservableCollection<BannerDotViewModel> BannerDots { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value)) return;
            OnPropertyChanged(nameof(SelectionBorderThickness));
            OnPropertyChanged(nameof(SelectionOpacity));
            OnPropertyChanged(nameof(SelectedFrameOpacity));
        }
    }

    public Thickness SelectionBorderThickness => IsSelected ? new Thickness(2) : new Thickness(0);

    public double SelectionOpacity => IsSelected ? 1 : 0.78;

    public double SelectedFrameOpacity => IsSelected ? 1 : 0;

    public double SidebarDragOpacity
    {
        get => _sidebarDragOpacity;
        set => SetProperty(ref _sidebarDragOpacity, value);
    }

    public double SidebarDragOffsetX
    {
        get => _sidebarDragOffsetX;
        set => SetProperty(ref _sidebarDragOffsetX, value);
    }

    public double SidebarDragOffsetY
    {
        get => _sidebarDragOffsetY;
        set => SetProperty(ref _sidebarDragOffsetY, value);
    }

    public double SidebarDragSurfaceOpacity
    {
        get => _sidebarDragSurfaceOpacity;
        set => SetProperty(ref _sidebarDragSurfaceOpacity, value);
    }

    public double SidebarDropBeforeOpacity
    {
        get => _sidebarDropBeforeOpacity;
        set => SetProperty(ref _sidebarDropBeforeOpacity, value);
    }

    public double SidebarDropAfterOpacity
    {
        get => _sidebarDropAfterOpacity;
        set => SetProperty(ref _sidebarDropAfterOpacity, value);
    }

    public double TopContentOffsetY
    {
        get => _topContentOffsetY;
        set => SetProperty(ref _topContentOffsetY, value);
    }

    public double BottomContentOffsetY
    {
        get => _bottomContentOffsetY;
        set => SetProperty(ref _bottomContentOffsetY, value);
    }

    public double ContentOpacity
    {
        get => _contentOpacity;
        set => SetProperty(ref _contentOpacity, value);
    }

    public string RootPath
    {
        get => _entry.RootPath;
        set
        {
            if (_entry.RootPath == value) return;
            _entry.RootPath = value;
            OnPropertyChanged();
            RaiseCommandStates();
        }
    }

    public bool SyncLaunchEnabled
    {
        get => _entry.SyncLaunchEnabled;
        set
        {
            if (_entry.SyncLaunchEnabled == value) return;
            _entry.SyncLaunchEnabled = value;
            OnPropertyChanged();
        }
    }

    public bool AccountSwitchEnabled
    {
        get => _entry.AccountSwitchEnabled;
        set
        {
            if (_entry.AccountSwitchEnabled == value) return;
            _entry.AccountSwitchEnabled = value;
            OnPropertyChanged();
        }
    }

    public string LocalVersionLabel => string.IsNullOrWhiteSpace(_entry.LocalVersion)
        ? "本地版本：未知"
        : $"本地版本：{_entry.LocalVersion}";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool AreNoticesCollapsed
    {
        get => _areNoticesCollapsed;
        set
        {
            if (!SetProperty(ref _areNoticesCollapsed, value)) return;
            OnPropertyChanged(nameof(NoticesPanelHeight));
            OnPropertyChanged(nameof(NoticesPanelWidth));
            OnPropertyChanged(nameof(NoticesPanelMinWidth));
            OnPropertyChanged(nameof(NoticesPanelMaxWidth));
            OnPropertyChanged(nameof(LauncherWindowMinWidth));
            OnPropertyChanged(nameof(NoticesCollapseArrowAngle));
            OnPropertyChanged(nameof(NoticesCollapseToolTip));
        }
    }

    public double NoticesPanelHeight => AreNoticesCollapsed ? 48D : 152D;

    public double NoticesPanelWidth => AreNoticesCollapsed ? 220D : 650D;

    public double NoticesPanelMinWidth => AreNoticesCollapsed ? 190D : 360D;

    public double NoticesPanelMaxWidth => AreNoticesCollapsed ? 220D : 560D;

    public double LauncherWindowMinWidth => AreNoticesCollapsed ? 820D : 1110D;

    public double NoticesCollapseArrowAngle => AreNoticesCollapsed ? 180D : 0D;

    public string NoticesCollapseToolTip => AreNoticesCollapsed ? "展开公告" : "收起公告";

    public RelayCommand BrowseCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand LaunchCommand { get; }
    public RelayCommand SelectCommand { get; }
    public RelayCommand ToggleNoticesCommand { get; }
    public RelayCommand PreviousBannerCommand { get; }
    public RelayCommand NextBannerCommand { get; }
    public RelayCommand OpenBannerCommand { get; }

    public bool CanSwitchBanner => _bannerItems.Count > 1;

    public void UpdateBannerViewportWidth(double width)
    {
        if (double.IsFinite(width) && width > 0D)
        {
            _bannerViewportWidth = width;
        }
    }

    public bool BeginBannerDrag(double viewportWidth)
    {
        UpdateBannerViewportWidth(viewportWidth);
        if (!CanSwitchBanner) return false;

        if (_isBannerAnimating)
        {
            CompleteBannerMotionImmediately();
        }
        else
        {
            ++_bannerAnimationVersion;
            ResetBannerVisualState();
        }

        _bannerTimer.Stop();
        _isBannerDragging = true;
        _bannerDragDirection = 0;
        return true;
    }

    public void UpdateBannerDrag(double offsetX, double viewportWidth)
    {
        UpdateBannerViewportWidth(viewportWidth);
        if (!_isBannerDragging || !CanSwitchBanner) return;

        var travel = GetBannerTravel();
        var clampedOffset = Math.Clamp(offsetX, -travel, travel);
        if (Math.Abs(clampedOffset) < 0.01D)
        {
            BannerOffsetX = 0D;
            return;
        }

        var direction = clampedOffset < 0D ? 1 : -1;
        var targetIndex = WrapBannerIndex(_selectedBannerIndex + direction);
        if (!PrepareIncomingBanner(targetIndex)) return;

        _bannerDragDirection = direction;
        BannerOffsetX = clampedOffset;
        IncomingBannerOffsetX = clampedOffset + direction * travel;
    }

    public async Task CompleteBannerDragAsync(double offsetX, double viewportWidth)
    {
        UpdateBannerViewportWidth(viewportWidth);
        if (!_isBannerDragging) return;

        _isBannerDragging = false;
        var travel = GetBannerTravel();
        var clampedOffset = Math.Clamp(offsetX, -travel, travel);
        var threshold = Math.Max(42D, travel / 4D);
        var commitDirection = clampedOffset <= -threshold
            ? 1
            : clampedOffset >= threshold
                ? -1
                : 0;

        if (commitDirection != 0)
        {
            var targetIndex = WrapBannerIndex(_selectedBannerIndex + commitDirection);
            if (!PrepareIncomingBanner(targetIndex)) commitDirection = 0;
            else _bannerDragDirection = commitDirection;
        }

        if (_bannerDragDirection == 0)
        {
            ResetBannerVisualState();
            UpdateBannerTimer();
            return;
        }

        _isBannerAnimating = true;
        _bannerAnimationCommitDirection = commitDirection;
        var animationVersion = ++_bannerAnimationVersion;
        var targetOffset = commitDirection == 0 ? 0D : -commitDirection * travel;
        var completed = await AnimateBannerOffsetAsync(
            clampedOffset,
            targetOffset,
            travel,
            _bannerDragDirection,
            animationVersion);

        if (!completed) return;

        if (commitDirection != 0 && _incomingBannerIndex >= 0 && IncomingBannerBitmap != null)
        {
            _selectedBannerIndex = _incomingBannerIndex;
            BannerBitmap = IncomingBannerBitmap;
            OnPropertyChanged(nameof(BannerBitmap));
            UpdateBannerDots(_selectedBannerIndex);
            OpenBannerCommand.RaiseCanExecuteChanged();
        }

        FinishBannerMotion();
    }

    public async Task CancelBannerDragAsync()
    {
        if (!_isBannerDragging) return;

        _isBannerDragging = false;
        if (_bannerDragDirection == 0)
        {
            ResetBannerVisualState();
            UpdateBannerTimer();
            return;
        }

        _isBannerAnimating = true;
        _bannerAnimationCommitDirection = 0;
        var animationVersion = ++_bannerAnimationVersion;
        var completed = await AnimateBannerOffsetAsync(
            BannerOffsetX,
            0D,
            GetBannerTravel(),
            _bannerDragDirection,
            animationVersion);

        if (completed) FinishBannerMotion();
    }

    public void CancelBannerDrag()
    {
        if (!_isBannerDragging) return;

        ++_bannerAnimationVersion;
        _isBannerDragging = false;
        _isBannerAnimating = false;
        _bannerAnimationCommitDirection = 0;
        ResetBannerVisualState();
        UpdateBannerTimer();
    }

    public Task RefreshBackgroundAsync() => _backgroundRefreshTask ??= RefreshBackgroundCoreAsync();

    private async Task RefreshBackgroundCoreAsync()
    {
        var imagePath = await LauncherBackgroundService.RefreshAsync(IconName).ConfigureAwait(false);
        var bitmap = LoadBitmap(imagePath);
        if (bitmap == null) return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var oldBitmap = _backgroundBitmap;
            var bannerUsedBackground = ReferenceEquals(BannerBitmap, oldBitmap);
            _backgroundBitmap = bitmap;
            OnPropertyChanged(nameof(BackgroundBitmap));

            if (bannerUsedBackground)
            {
                BannerBitmap = bitmap;
                OnPropertyChanged(nameof(BannerBitmap));
            }

            if (oldBitmap != null && !ReferenceEquals(oldBitmap, IconBitmap))
            {
                oldBitmap.Dispose();
            }
        });
    }

    private bool CanSwitchBannerInteractively => CanSwitchBanner;

    private void ToggleNotices()
    {
        var isExpanding = AreNoticesCollapsed;
        _areNoticesResponsiveCollapsed = false;
        if (isExpanding)
        {
            EnsureWindowWidthForExpandedNotices();
        }

        AreNoticesCollapsed = !AreNoticesCollapsed;
    }

    public void ApplyResponsiveNoticeCollapse(double windowWidth)
    {
        if (windowWidth <= 0) return;

        if (!AreNoticesCollapsed && windowWidth < NoticeCollapseBeforeTouchWidth)
        {
            _areNoticesResponsiveCollapsed = true;
            AreNoticesCollapsed = true;
            return;
        }

        if (_areNoticesResponsiveCollapsed && AreNoticesCollapsed && windowWidth >= NoticeRestoreExpandedWidth)
        {
            _areNoticesResponsiveCollapsed = false;
            AreNoticesCollapsed = false;
        }
    }

    private void EnsureWindowWidthForExpandedNotices()
    {
        if (_owner.WindowState != WindowState.Normal || _owner.Width >= NoticeRestoreExpandedWidth) return;

        _owner.Width = NoticeRestoreExpandedWidth;
    }

    private void SelectPreviousBanner()
    {
        if (!CanSwitchBannerInteractively) return;
        var index = _selectedBannerIndex - 1;
        if (index < 0) index = _bannerItems.Count - 1;
        _ = AnimateToBannerAsync(index, -1);
    }

    private void SelectNextBanner()
    {
        if (!CanSwitchBannerInteractively) return;
        _ = AnimateToBannerAsync((_selectedBannerIndex + 1) % _bannerItems.Count, 1);
    }

    private void OpenSelectedBanner()
    {
        var jumpUrl = GetSelectedBannerJumpUrl();
        if (!string.IsNullOrWhiteSpace(jumpUrl))
        {
            StartShell(jumpUrl);
        }
    }

    private bool CanOpenSelectedBanner() => !string.IsNullOrWhiteSpace(GetSelectedBannerJumpUrl());

    private string GetSelectedBannerJumpUrl() =>
        _selectedBannerIndex >= 0 && _selectedBannerIndex < _bannerItems.Count
            ? _bannerItems[_selectedBannerIndex].JumpUrl
            : "";

    private bool ApplyConfiguredLauncherNotice()
    {
        if (!_config.LauncherNoticeCache.TryGetValue(GetNoticeCacheKey(IconName), out var cached))
        {
            return false;
        }

        var content = new LauncherNoticeContent(
            cached.Banners.Select(x => new LauncherBannerItem(x.ImageUrl, x.JumpUrl)).ToArray(),
            cached.Notices.Select(x => new LauncherNoticeItem(x.Category, x.Title, x.Date, x.JumpUrl)).ToArray());

        ApplyLauncherNoticePayload(LauncherNoticeService.CreatePayload(IconName, content));
        return cached.Banners.Count > 0 || cached.Notices.Count > 0;
    }

    private void LoadCachedLauncherNotice()
    {
        ApplyLauncherNoticePayload(LauncherNoticeService.LoadCached(IconName));
    }

    private async Task RefreshLauncherNoticeAsync()
    {
        var payload = await LauncherNoticeService.RefreshAsync(IconName).ConfigureAwait(false);
        if (payload == null) return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ApplyLauncherNoticePayload(payload);
            SaveConfiguredLauncherNotice(payload.Content);
        });
    }

    private void SaveConfiguredLauncherNotice(LauncherNoticeContent content)
    {
        _config.LauncherNoticeCache[GetNoticeCacheKey(IconName)] = new CachedLauncherNoticeContent
        {
            CachedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            Banners = content.Banners
                .Take(6)
                .Select(x => new CachedLauncherBanner
                {
                    ImageUrl = x.ImageUrl,
                    JumpUrl = x.JumpUrl,
                })
                .ToList(),
            Notices = content.Notices
                .Where(x => !string.IsNullOrWhiteSpace(x.Title))
                .Take(20)
                .Select(x => new CachedLauncherNotice
                {
                    Category = x.Category,
                    Title = x.Title,
                    Date = x.Date,
                    JumpUrl = x.JumpUrl,
                })
                .ToList(),
        };

        _save();
    }

    private void ApplyLauncherNoticePayload(LauncherNoticePayload? payload)
    {
        if (payload == null) return;

        if (payload.BannerAssets.Count > 0)
        {
            _bannerItems.Clear();
            _bannerItems.AddRange(payload.BannerAssets.Where(x => File.Exists(x.ImagePath)));
            if (_selectedBannerIndex >= _bannerItems.Count) _selectedBannerIndex = 0;
            if (!_isBannerAnimating && !_isBannerDragging)
            {
                SetSelectedBannerImmediate();
            }
            else
            {
                RebuildBannerDots(_selectedBannerIndex);
                OnPropertyChanged(nameof(CanSwitchBanner));
                PreviousBannerCommand.RaiseCanExecuteChanged();
                NextBannerCommand.RaiseCanExecuteChanged();
                OpenBannerCommand.RaiseCanExecuteChanged();
            }
        }

        var notices = payload.Content.Notices
            .Where(x => !string.IsNullOrWhiteSpace(x.Title))
            .Take(20)
            .Select(x => new NoticeItemViewModel(x.Category, x.Title.TrimStart(), x.Date, x.JumpUrl))
            .ToArray();

        if (notices.Length == 0) return;

        _allNotices.Clear();
        _allNotices.AddRange(notices);
        RebuildNoticeTabs();
        ApplyNoticeCategory();
    }

    private void RebuildNoticeTabs()
    {
        var categories = _allNotices
            .Select(x => x.Category)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(GetNoticeCategoryOrder)
            .ThenBy(x => x, StringComparer.Ordinal)
            .ToArray();

        if (categories.Length == 0)
        {
            NoticeTabs.Clear();
            _selectedNoticeCategory = "";
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedNoticeCategory) || !categories.Contains(_selectedNoticeCategory))
        {
            _selectedNoticeCategory = categories[0];
        }

        NoticeTabs.Clear();
        foreach (var category in categories)
        {
            NoticeTabs.Add(new NoticeTabViewModel(category, () => SelectNoticeCategory(category))
            {
                IsSelected = string.Equals(category, _selectedNoticeCategory, StringComparison.Ordinal),
            });
        }
    }

    private void SelectNoticeCategory(string category)
    {
        if (string.Equals(_selectedNoticeCategory, category, StringComparison.Ordinal)) return;

        _selectedNoticeCategory = category;
        foreach (var tab in NoticeTabs)
        {
            tab.IsSelected = string.Equals(tab.Category, _selectedNoticeCategory, StringComparison.Ordinal);
        }

        ApplyNoticeCategory();
    }

    private void ApplyNoticeCategory()
    {
        Notices.Clear();

        var category = _selectedNoticeCategory;
        foreach (var item in _allNotices.Where(x => string.Equals(x.Category, category, StringComparison.Ordinal)))
        {
            Notices.Add(item);
        }
    }

    private static int GetNoticeCategoryOrder(string category) => category switch
    {
        "\u516c\u544a" => 0,
        "\u65b0\u95fb" => 1,
        "\u8d44\u8baf" => 2,
        _ => 10,
    };

    private static string GetNoticeCacheKey(string iconName) => iconName switch
    {
        "BiliArknights" => "Arknights",
        "BiliEndfield" => "Endfield",
        "PlayEndfield" => "GlobalEndfield",
        _ => iconName,
    };

    private void SetSelectedBannerImmediate()
    {
        if (_bannerItems.Count == 0) return;

        var bitmap = LoadBitmap(_bannerItems[_selectedBannerIndex].ImagePath);
        if (bitmap != null)
        {
            BannerBitmap = bitmap;
            OnPropertyChanged(nameof(BannerBitmap));
        }

        ResetBannerVisualState();
        RebuildBannerDots(_selectedBannerIndex);
        OnPropertyChanged(nameof(CanSwitchBanner));
        PreviousBannerCommand.RaiseCanExecuteChanged();
        NextBannerCommand.RaiseCanExecuteChanged();
        OpenBannerCommand.RaiseCanExecuteChanged();
        UpdateBannerTimer();
    }

    private async Task AnimateToBannerAsync(int nextIndex, int direction)
    {
        if (_isBannerAnimating || _isBannerDragging || nextIndex == _selectedBannerIndex || _bannerItems.Count == 0) return;
        if (!PrepareIncomingBanner(nextIndex)) return;

        var travel = GetBannerTravel();
        direction = direction < 0 ? -1 : 1;
        _bannerDragDirection = direction;
        _isBannerAnimating = true;
        _bannerAnimationCommitDirection = direction;
        _bannerTimer.Stop();
        BannerOffsetX = 0D;
        IncomingBannerOffsetX = direction * travel;
        var animationVersion = ++_bannerAnimationVersion;
        var completed = await AnimateBannerOffsetAsync(
            0D,
            -direction * travel,
            travel,
            direction,
            animationVersion);

        if (!completed) return;

        _selectedBannerIndex = nextIndex;
        BannerBitmap = IncomingBannerBitmap;
        OnPropertyChanged(nameof(BannerBitmap));
        UpdateBannerDots(_selectedBannerIndex);
        OpenBannerCommand.RaiseCanExecuteChanged();
        FinishBannerMotion();
    }

    private bool PrepareIncomingBanner(int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= _bannerItems.Count) return false;
        if (_incomingBannerIndex != targetIndex || IncomingBannerBitmap == null)
        {
            var bitmap = LoadBitmap(_bannerItems[targetIndex].ImagePath);
            if (bitmap == null) return false;

            IncomingBannerBitmap = bitmap;
            _incomingBannerIndex = targetIndex;
            OnPropertyChanged(nameof(IncomingBannerBitmap));
        }

        IsIncomingBannerVisible = true;
        return true;
    }

    private async Task<bool> AnimateBannerOffsetAsync(
        double startOffset,
        double targetOffset,
        double travel,
        int direction,
        int animationVersion)
    {
        const double durationMilliseconds = 280D;
        TimeSpan? startTimestamp = null;

        while (true)
        {
            var timestamp = await NextAnimationFrameAsync();
            if (animationVersion != _bannerAnimationVersion) return false;

            startTimestamp ??= timestamp;
            var progress = Math.Clamp(
                (timestamp - startTimestamp.Value).TotalMilliseconds / durationMilliseconds,
                0D,
                1D);
            var easedProgress = progress * progress * (3D - 2D * progress);
            var offset = startOffset + (targetOffset - startOffset) * easedProgress;
            BannerOffsetX = offset;
            IncomingBannerOffsetX = offset + direction * travel;

            if (progress >= 1D) return true;
        }
    }

    private Task<TimeSpan> NextAnimationFrameAsync()
    {
        var completion = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
        _owner.RequestAnimationFrame(timestamp => completion.TrySetResult(timestamp));
        return completion.Task;
    }

    private double GetBannerTravel() => Math.Max(1D, _bannerViewportWidth);

    private int WrapBannerIndex(int index)
    {
        if (_bannerItems.Count == 0) return 0;
        index %= _bannerItems.Count;
        return index < 0 ? index + _bannerItems.Count : index;
    }

    private void FinishBannerMotion()
    {
        _isBannerAnimating = false;
        _isBannerDragging = false;
        _bannerAnimationCommitDirection = 0;
        ResetBannerVisualState();
        UpdateBannerTimer();
    }

    private void CompleteBannerMotionImmediately()
    {
        if (!_isBannerAnimating) return;

        ++_bannerAnimationVersion;
        if (_bannerAnimationCommitDirection != 0 &&
            _incomingBannerIndex >= 0 &&
            IncomingBannerBitmap != null)
        {
            _selectedBannerIndex = _incomingBannerIndex;
            BannerBitmap = IncomingBannerBitmap;
            OnPropertyChanged(nameof(BannerBitmap));
            UpdateBannerDots(_selectedBannerIndex);
            OpenBannerCommand.RaiseCanExecuteChanged();
        }

        FinishBannerMotion();
    }

    private void ResetBannerVisualState()
    {
        BannerOffsetX = 0D;
        ClearIncomingBanner();
        _bannerDragDirection = 0;
    }

    private void ClearIncomingBanner()
    {
        IncomingBannerOffsetX = 0D;
        IsIncomingBannerVisible = false;
        IncomingBannerBitmap = null;
        _incomingBannerIndex = -1;
        OnPropertyChanged(nameof(IncomingBannerBitmap));
    }

    private void RebuildBannerDots(int selectedIndex)
    {
        if (BannerDots.Count != _bannerItems.Count)
        {
            BannerDots.Clear();
            for (var i = 0; i < _bannerItems.Count; i++)
            {
                BannerDots.Add(new BannerDotViewModel(i == selectedIndex));
            }

            return;
        }

        UpdateBannerDots(selectedIndex);
    }

    private void UpdateBannerDots(int selectedIndex)
    {
        for (var i = 0; i < BannerDots.Count; i++)
        {
            BannerDots[i].IsSelected = i == selectedIndex;
        }
    }

    private void UpdateBannerTimer()
    {
        _bannerTimer.Stop();
        if (CanSwitchBanner)
        {
            _bannerTimer.Start();
        }
    }

    private string GetFallbackCoverUri() => IsEndfield
        ? "avares://XelLauncher/Resources/Icon/End.jpg"
        : "avares://XelLauncher/Resources/Icon/Arknights.jpg";

    private static Bitmap? LoadAssetBitmap(string uri)
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri(uri));
            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex, $"LoadAssetBitmap({uri})");
            return null;
        }
    }

    private static Bitmap? LoadBitmap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            return new Bitmap(path);
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex, $"LoadBitmap({path})");
            return null;
        }
    }

    private static string? FindCachedCover(string iconName)
    {
        var dir = GetCoverDirectory(iconName);
        if (!Directory.Exists(dir)) return null;

        return Directory
            .EnumerateFiles(dir, "client-cover-*.*")
            .Where(IsSupportedImagePath)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static string? FindCachedBanner(string iconName)
    {
        var dir = GetCoverDirectory(iconName);
        if (!Directory.Exists(dir)) return null;

        return Directory
            .EnumerateFiles(dir, "notice-banner-*.*")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static bool IsSupportedImagePath(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp";

    private static string GetCoverDirectory(string iconName)
    {
        var normalized = iconName switch
        {
            "BiliArknights" => "Arknights",
            "BiliEndfield" => "Endfield",
            "PlayEndfield" => "GlobalEndfield",
            _ => iconName,
        };

        return Path.Combine(ConfigHelper.ConfigDir, "GameCovers", normalized);
    }

    private async void Browse()
    {
        try
        {
            var folders = await _owner.StorageProvider.OpenFolderPickerAsync(new()
            {
                Title = $"选择 {DisplayName} 游戏目录",
                AllowMultiple = false,
            });

            if (folders.Count == 0) return;
            RootPath = folders[0].Path.LocalPath;
            _save();
            StatusMessage = "目录已保存";
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex, "BrowseGameRoot");
            StatusMessage = "选择目录失败";
        }
    }

    private void OpenFolder()
    {
        StartShell(RootPath);
    }

    private void Launch()
    {
        var exe = FindLikelyExecutable(RootPath);
        StartShell(exe ?? RootPath);
    }

    private static string? FindLikelyExecutable(string root)
    {
        if (!Directory.Exists(root)) return null;
        foreach (var pattern in new[] { "Arknights.exe", "Endfield.exe", "*.exe" })
        {
            var match = Directory.GetFiles(root, pattern, SearchOption.TopDirectoryOnly);
            if (match.Length > 0) return match[0];
        }

        return null;
    }

    private static void StartShell(string path)
    {
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void RaiseCommandStates()
    {
        OpenFolderCommand.RaiseCanExecuteChanged();
        LaunchCommand.RaiseCanExecuteChanged();
    }
}

public sealed class BannerDotViewModel : ViewModelBase
{
    private bool _isSelected;

    public BannerDotViewModel(bool isSelected)
    {
        _isSelected = isSelected;
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value)) return;
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(Opacity));
        }
    }

    public double Width => IsSelected ? 18D : 8D;
    public double Opacity => IsSelected ? 0.92D : 0.42D;
}

public sealed class NoticeTabViewModel : ViewModelBase
{
    private bool _isSelected;

    public NoticeTabViewModel(string category, Action select)
    {
        Category = category;
        SelectCommand = new RelayCommand(select);
    }

    public string Category { get; }

    public RelayCommand SelectCommand { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value)) return;
            OnPropertyChanged(nameof(TextOpacity));
            OnPropertyChanged(nameof(TextWeight));
            OnPropertyChanged(nameof(IndicatorWidth));
            OnPropertyChanged(nameof(IndicatorOpacity));
        }
    }

    public double TextOpacity => IsSelected ? 1D : 0.72D;

    public FontWeight TextWeight => IsSelected ? FontWeight.SemiBold : FontWeight.Regular;

    public double IndicatorWidth => IsSelected ? 24D : 0D;

    public double IndicatorOpacity => IsSelected ? 1D : 0D;
}
