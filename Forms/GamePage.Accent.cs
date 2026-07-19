using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Hi3Helper.Plugin.Core.Management;
using XelLauncher.Helpers;
using XelLauncher.Models;
namespace XelLauncher.Forms
{
    public partial class GamePage : UserControl
    {
        public void UpdateLaunchPanelColor()
        {
            panelLaunch.BackExtend = "";
            panelLaunch.Back = Color.Transparent;
            panelLaunch.BackColor = Color.Transparent;

            if (AntdUI.Config.IsDark)
            {
                if (_toolSidebar != null) _toolSidebar.Back = Color.FromArgb(188, 34, 37, 43);
            }
            else if (_toolSidebar != null) _toolSidebar.Back = Color.FromArgb(188, 34, 37, 43);

            ApplyCoverAccentToLaunchControls();
            if (_gameInfoBadge != null && !_gameInfoBadge.IsDisposed)
                _gameInfoBadge.AccentColor = GetCoverAccentPalette().PrimaryHover;
        }

        private void ApplyCoverAccentToLaunchControls()
        {
            var palette = GetCoverAccentPalette();

            GameStart.BackExtend = $"135, {ToHex(palette.Primary)}, {ToHex(palette.PrimaryHover)}";
            GameStart.BackHover = palette.PrimaryHover;
            GameStart.BackActive = palette.PrimaryActive;

            floatMenu.BackExtend = $"135, {ToHex(palette.PrimaryHover)}, {ToHex(palette.PrimaryActive)}";
            floatMenu.BackHover = palette.Primary;
            floatMenu.BackActive = palette.PrimaryActive;

            if (_noticePanel != null)
            {
                _noticePanel.ToggleBackColor = Color.FromArgb(218, palette.PrimaryActive);
                _noticePanel.Invalidate();
            }

            btnAccountManage.BackExtend = $"135, {ToHex(palette.PrimaryActive)}, {ToHex(palette.Primary)}";
            btnAccountManage.BackHover = palette.PrimaryHover;
            btnAccountManage.BackActive = palette.PrimaryActive;
            btnAccountManage.BorderWidth = 0;
            btnAccountManage.WaveSize = 0;

            accountSelect.BackExtend = $"135, {ToHex(palette.Muted)}, {ToHex(palette.MutedHover)}";
            accountSelect.BackColor = palette.Muted;
            accountSelect.BorderColor = Color.FromArgb(92, palette.PrimaryHover);
            accountSelect.BorderHover = Color.FromArgb(150, palette.PrimaryHover);
            accountSelect.BorderActive = palette.PrimaryHover;
            accountSelect.ForeColor = Color.FromArgb(238, 245, 245, 245);
            accountSelect.PlaceholderColor = Color.FromArgb(190, 245, 245, 245);
            accountSelect.SelectionColor = Color.FromArgb(80, palette.PrimaryHover);
            ApplyGlobalAccentPalette(palette);
            GameStart.LoadingWaveColor = Color.FromArgb(64, 255, 255, 255);
        }

        private static void ApplyGlobalAccentPalette((Color Primary, Color PrimaryHover, Color PrimaryActive, Color Muted, Color MutedHover, Color MutedActive, Color Danger, Color DangerHover, Color Text) palette)
        {
            AntdUI.Style.SetPrimary(palette.Primary);

            AntdUI.Style.Set(AntdUI.Colour.Primary.ToString(), palette.Primary, nameof(AntdUI.Button));
            AntdUI.Style.Set(AntdUI.Colour.PrimaryHover.ToString(), palette.PrimaryHover, nameof(AntdUI.Button));
            AntdUI.Style.Set(AntdUI.Colour.PrimaryActive.ToString(), palette.PrimaryActive, nameof(AntdUI.Button));
            AntdUI.Style.Set(AntdUI.Colour.PrimaryColor.ToString(), palette.Text, nameof(AntdUI.Button));

            AntdUI.Style.Set(AntdUI.Colour.Primary.ToString(), palette.Primary, nameof(AntdUI.Switch));
            AntdUI.Style.Set(AntdUI.Colour.PrimaryHover.ToString(), palette.PrimaryHover, nameof(AntdUI.Switch));
            AntdUI.Style.Set(AntdUI.Colour.PrimaryActive.ToString(), palette.PrimaryActive, nameof(AntdUI.Switch));

            AntdUI.Style.Set(AntdUI.Colour.TextQuaternary.ToString(), Color.FromArgb(188, 245, 245, 245), nameof(AntdUI.Select));
        }

        public (Color Primary, Color PrimaryHover, Color PrimaryActive, Color Muted, Color MutedHover, Color MutedActive, Color Danger, Color DangerHover, Color Text) GetCoverAccentPalette()
        {
            if (_coverAccentPaletteValid)
                return _coverAccentPalette;

            var accent = PickCoverAccentColor(_coverImage);
            var primary = NormalizeAccent(accent, 0.56f, 0.30f, 0.48f);
            var primaryHover = ShiftLightness(primary, 0.07f);
            var primaryActive = ShiftLightness(primary, -0.16f);
            var muted = ShiftLightness(primary, -0.34f);
            var mutedHover = ShiftLightness(primary, -0.26f);
            var mutedActive = ShiftLightness(primary, -0.42f);
            var danger = ShiftHue(primary, -14F, 0.30f, 0.48f);
            var dangerHover = ShiftLightness(danger, 0.07f);
            _coverAccentPalette = (primary, primaryHover, primaryActive, muted, mutedHover, mutedActive, danger, dangerHover, Color.FromArgb(245, 255, 255, 255));
            _coverAccentPaletteValid = true;
            return _coverAccentPalette;
        }
    }
}
