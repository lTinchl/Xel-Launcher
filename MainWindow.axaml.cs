using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.VisualTree;
using SukiUI.Controls;
using XelLauncher.ViewModels;

namespace XelLauncher;

public partial class MainWindow : SukiWindow
{
    private const double BannerTapTolerance = 6D;
    private const double BannerDragActivationDistance = 4D;
    private const double SidebarDragActivationDistance = 7D;
    private const double SidebarItemHeight = 64D;
    private Control? _bannerPointerTarget;
    private Point _bannerPointerStart;
    private bool _bannerPointerMoved;
    private bool _bannerHorizontalDrag;
    private Control? _sidebarPointerTarget;
    private ContentPresenter? _sidebarDragContainer;
    private GameEntryViewModel? _sidebarPressedGame;
    private GameEntryViewModel? _sidebarDropIndicatorGame;
    private Point _sidebarPointerStart;
    private bool _sidebarDragging;
    private int _sidebarInsertionIndex = -1;

    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainWindowViewModel(this);
        DataContext = viewModel;
        viewModel.UpdateWindowWidth(Bounds.Width);
        SizeChanged += (_, _) => viewModel.UpdateWindowWidth(Bounds.Width);
    }

    private void SidebarGame_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: GameEntryViewModel game } target ||
            !e.GetCurrentPoint(target).Properties.IsLeftButtonPressed)
        {
            return;
        }

        ClearSidebarDragState();
        _sidebarPointerTarget = target;
        _sidebarPressedGame = game;
        _sidebarPointerStart = e.GetPosition(SidebarGameList);
        target.Focus();
        e.Pointer.Capture(target);
        e.Handled = true;
    }

    private void SidebarGame_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Control target || !ReferenceEquals(_sidebarPointerTarget, target)) return;

        if (!e.GetCurrentPoint(target).Properties.IsLeftButtonPressed)
        {
            ClearSidebarDragState();
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

        var position = e.GetPosition(SidebarGameList);
        var deltaX = position.X - _sidebarPointerStart.X;
        var deltaY = position.Y - _sidebarPointerStart.Y;
        if (!_sidebarDragging &&
            Math.Sqrt(deltaX * deltaX + deltaY * deltaY) < SidebarDragActivationDistance)
        {
            return;
        }

        if (!_sidebarDragging)
        {
            _sidebarDragging = true;
            if (_sidebarPressedGame != null)
            {
                _sidebarPressedGame.SidebarDragOpacity = 0.96D;
                _sidebarPressedGame.SidebarDragSurfaceOpacity = 1D;
            }

            _sidebarDragContainer = target.FindAncestorOfType<ContentPresenter>();
            if (_sidebarDragContainer != null) _sidebarDragContainer.ZIndex = 100;
        }

        if (_sidebarPressedGame != null)
        {
            _sidebarPressedGame.SidebarDragOffsetX = deltaX;
            _sidebarPressedGame.SidebarDragOffsetY = deltaY;
        }

        UpdateSidebarDropIndicator(position.Y);
        e.Handled = true;
    }

    private void SidebarGame_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not Control target ||
            !ReferenceEquals(_sidebarPointerTarget, target) ||
            e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        var game = _sidebarPressedGame;
        var wasDragging = _sidebarDragging;
        var insertionIndex = _sidebarInsertionIndex;
        var releasePosition = e.GetPosition(target);
        var releasedInside = releasePosition.X >= 0D && releasePosition.X <= target.Bounds.Width &&
                             releasePosition.Y >= 0D && releasePosition.Y <= target.Bounds.Height;

        ClearSidebarDragState();
        e.Pointer.Capture(null);
        e.Handled = true;

        if (game == null) return;

        if (wasDragging)
        {
            if (DataContext is MainWindowViewModel viewModel && insertionIndex >= 0)
            {
                viewModel.MoveSidebarGame(game, insertionIndex);
            }

            return;
        }

        if (releasedInside && game.SelectCommand.CanExecute(null))
        {
            game.SelectCommand.Execute(null);
        }
    }

    private void SidebarGame_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (ReferenceEquals(_sidebarPointerTarget, sender)) ClearSidebarDragState();
    }

    private void SidebarGame_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control { DataContext: GameEntryViewModel game } ||
            e.Key is not (Key.Enter or Key.Space) ||
            !game.SelectCommand.CanExecute(null))
        {
            return;
        }

        game.SelectCommand.Execute(null);
        e.Handled = true;
    }

    private void UpdateSidebarDropIndicator(double pointerY)
    {
        if (DataContext is not MainWindowViewModel viewModel || viewModel.Games.Count == 0) return;

        var insertionIndex = (int)Math.Floor((pointerY + SidebarItemHeight / 2D) / SidebarItemHeight);
        insertionIndex = Math.Clamp(insertionIndex, 0, viewModel.Games.Count);
        if (_sidebarInsertionIndex == insertionIndex) return;

        ClearSidebarDropIndicator();
        _sidebarInsertionIndex = insertionIndex;

        if (insertionIndex == viewModel.Games.Count)
        {
            _sidebarDropIndicatorGame = viewModel.Games[^1];
            _sidebarDropIndicatorGame.SidebarDropAfterOpacity = 1D;
        }
        else
        {
            _sidebarDropIndicatorGame = viewModel.Games[insertionIndex];
            _sidebarDropIndicatorGame.SidebarDropBeforeOpacity = 1D;
        }
    }

    private void ClearSidebarDropIndicator()
    {
        if (_sidebarDropIndicatorGame != null)
        {
            _sidebarDropIndicatorGame.SidebarDropBeforeOpacity = 0D;
            _sidebarDropIndicatorGame.SidebarDropAfterOpacity = 0D;
            _sidebarDropIndicatorGame = null;
        }

        _sidebarInsertionIndex = -1;
    }

    private void ClearSidebarDragState()
    {
        if (_sidebarPressedGame != null)
        {
            _sidebarPressedGame.SidebarDragOpacity = 1D;
            _sidebarPressedGame.SidebarDragOffsetX = 0D;
            _sidebarPressedGame.SidebarDragOffsetY = 0D;
            _sidebarPressedGame.SidebarDragSurfaceOpacity = 0D;
        }

        if (_sidebarDragContainer != null) _sidebarDragContainer.ZIndex = 0;

        ClearSidebarDropIndicator();
        _sidebarPointerTarget = null;
        _sidebarDragContainer = null;
        _sidebarPressedGame = null;
        _sidebarDragging = false;
    }

    private void BannerInteraction_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control target ||
            !e.GetCurrentPoint(target).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _bannerPointerTarget = target;
        _bannerPointerStart = e.GetPosition(target);
        _bannerPointerMoved = false;
        _bannerHorizontalDrag = false;
        if (target.DataContext is GameEntryViewModel viewModel)
        {
            viewModel.BeginBannerDrag(target.Bounds.Width);
        }

        e.Pointer.Capture(target);
        e.Handled = true;
    }

    private void BannerInteraction_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Control target || !ReferenceEquals(_bannerPointerTarget, target)) return;

        if (!e.GetCurrentPoint(target).Properties.IsLeftButtonPressed)
        {
            var interruptedViewModel = target.DataContext as GameEntryViewModel;
            ResetBannerPointerState();
            e.Pointer.Capture(null);
            if (interruptedViewModel != null) _ = interruptedViewModel.CancelBannerDragAsync();
            e.Handled = true;
            return;
        }

        var position = e.GetPosition(target);
        var deltaX = position.X - _bannerPointerStart.X;
        var deltaY = position.Y - _bannerPointerStart.Y;
        if (Math.Sqrt(deltaX * deltaX + deltaY * deltaY) > BannerTapTolerance)
        {
            _bannerPointerMoved = true;
        }

        if (!_bannerHorizontalDrag)
        {
            if (Math.Abs(deltaX) < BannerDragActivationDistance &&
                Math.Abs(deltaY) < BannerDragActivationDistance)
            {
                e.Handled = true;
                return;
            }

            if (Math.Abs(deltaX) <= Math.Abs(deltaY))
            {
                e.Handled = true;
                return;
            }

            _bannerHorizontalDrag = true;
        }

        if (target.DataContext is GameEntryViewModel viewModel)
        {
            viewModel.UpdateBannerDrag(deltaX, target.Bounds.Width);
        }

        e.Handled = true;
    }

    private async void BannerInteraction_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not Control target ||
            !ReferenceEquals(_bannerPointerTarget, target) ||
            e.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        var end = e.GetPosition(target);
        var deltaX = end.X - _bannerPointerStart.X;
        var deltaY = end.Y - _bannerPointerStart.Y;
        var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        var viewModel = target.DataContext as GameEntryViewModel;
        var wasHorizontalDrag = _bannerHorizontalDrag;
        var wasPointerMoved = _bannerPointerMoved;
        var releasedInside = end.X >= 0D && end.X <= target.Bounds.Width &&
                             end.Y >= 0D && end.Y <= target.Bounds.Height;

        ResetBannerPointerState();
        e.Pointer.Capture(null);
        e.Handled = true;

        if (viewModel == null) return;

        if (wasHorizontalDrag)
        {
            await viewModel.CompleteBannerDragAsync(deltaX, target.Bounds.Width);
            return;
        }

        viewModel.CancelBannerDrag();
        if (!wasPointerMoved && distance <= BannerTapTolerance && releasedInside &&
            viewModel.OpenBannerCommand.CanExecute(null))
        {
            viewModel.OpenBannerCommand.Execute(null);
        }
    }

    private void BannerInteraction_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!ReferenceEquals(_bannerPointerTarget, sender)) return;

        var viewModel = (sender as Control)?.DataContext as GameEntryViewModel;
        ResetBannerPointerState();
        if (viewModel != null) _ = viewModel.CancelBannerDragAsync();
    }

    private static void BannerInteraction_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (sender is Control { DataContext: GameEntryViewModel viewModel } target)
        {
            viewModel.UpdateBannerViewportWidth(target.Bounds.Width);
        }
    }

    private void BannerInteraction_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control { DataContext: GameEntryViewModel viewModel } ||
            e.Key is not (Key.Enter or Key.Space) ||
            !viewModel.OpenBannerCommand.CanExecute(null))
        {
            return;
        }

        viewModel.OpenBannerCommand.Execute(null);
        e.Handled = true;
    }

    private void ResetBannerPointerState()
    {
        _bannerPointerTarget = null;
        _bannerPointerMoved = false;
        _bannerHorizontalDrag = false;
    }
}
