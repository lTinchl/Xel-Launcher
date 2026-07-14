using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using SukiUI.Controls;
using XelLauncher.ViewModels;

namespace XelLauncher;

public partial class MainWindow : SukiWindow
{
    private const double BannerTapTolerance = 6D;
    private const double BannerDragActivationDistance = 4D;
    private Control? _bannerPointerTarget;
    private Point _bannerPointerStart;
    private bool _bannerPointerMoved;
    private bool _bannerHorizontalDrag;

    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainWindowViewModel(this);
        DataContext = viewModel;
        viewModel.UpdateWindowWidth(Bounds.Width);
        SizeChanged += (_, _) => viewModel.UpdateWindowWidth(Bounds.Width);
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
