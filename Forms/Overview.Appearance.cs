using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using AntdUI;
using XelLauncher.Helpers;

namespace XelLauncher.Forms
{
    public partial class Overview
    {
        private const int ThemeSurfaceAnimationMs = 280;
        private Timer _themeSurfaceTimer;
        private Stopwatch _themeSurfaceWatch;
        private Color _themeSurfaceFromBackground;
        private Color _themeSurfaceFromHeader;
        private Color _themeSurfaceFromForeground;
        private Color _themeSurfaceToBackground;
        private Color _themeSurfaceToHeader;
        private Color _themeSurfaceToForeground;
        private bool _themeSurfaceTargetDark;

        private void btn_back_Click(object sender, EventArgs e)
        {
            BeginInvoke(new Action(() =>
            {
                if (windowBar.Tag is Control control)
                {
                    control.Dispose();
                    Controls.Remove(control);
                }
                windowBar.ShowBack = false;
                windowBar.SubText = "Overview";
            }));
        }

       

        private void ApplyBackgroundColor(string hex)
        {
            try
            {
                var color = System.Drawing.ColorTranslator.FromHtml(hex);
                BackColor = color;
                ApplyThemeSurfaces();
                _currentGamePage?.UpdateLaunchPanelColor();
            }
            catch { }
        }

        private void ApplyThemeSurfaces()
        {
            var target = GetThemeSurfaceTarget(AntdUI.Config.IsDark);
            ApplyThemeSurfaceColors(target.Background, target.Header, target.Foreground);
        }

        private (Color Background, Color Header, Color Foreground) GetThemeSurfaceTarget(bool isDark)
        {
            if (isDark)
                return (AppTheme.DarkBackground, AppTheme.DarkHeader, AppTheme.DarkForeground);

            var lightBackground = GetConfiguredLightBackground();
            return (lightBackground, lightBackground, AppTheme.LightForeground);
        }

        private static Color GetConfiguredLightBackground()
        {
            try
            {
                return ColorTranslator.FromHtml(ConfigHelper.Load().BackgroundColor);
            }
            catch
            {
                return AppTheme.LightBackground;
            }
        }

        private void ApplyThemeSurfaceColors(Color background, Color header, Color foreground)
        {
            var divider = Blend(background, foreground, 0.16F);
            BackColor = background;
            ForeColor = foreground;
            windowBar.BackColor = header;
            SetSurfaceColor(panelSidebar, background);
            SetSurfaceColor(panelSidebarItems, background);
            SetSurfaceColor(sidebarBottomPad, background);
            SetSurfaceColor(panelMain, _currentGamePage != null ? Color.Black : background);
            dividerSidebarV.BackColor = background;
            dividerSidebar.ColorSplit = divider;
            dividerSidebarV.ColorSplit = divider;
            btnSidebarManage.BackColor = background;
            btnSidebarManage.ForeColor = foreground;
            btnSidebarManage.Invalidate();
            foreach (var button in _sidebarBtns)
                button.Invalidate();
            panelSidebar.Invalidate(true);
            btn_mode.Invalidate();
            updateBadge.Invalidate();
        }

        private static void SetSurfaceColor(Control control, Color color)
        {
            if (control is ThemeSurfacePanel surface)
                surface.SurfaceColor = color;
            else
                control.BackColor = color;
        }

        private void AnimateThemeSurfaces(bool targetDark)
        {
            var target = GetThemeSurfaceTarget(targetDark);
            EnsureThemeSurfaceAnimator();

            _themeSurfaceTimer.Stop();
            _themeSurfaceWatch.Restart();
            _themeSurfaceTargetDark = targetDark;
            _themeSurfaceFromBackground = BackColor;
            _themeSurfaceFromHeader = windowBar.BackColor;
            _themeSurfaceFromForeground = ForeColor;
            _themeSurfaceToBackground = target.Background;
            _themeSurfaceToHeader = target.Header;
            _themeSurfaceToForeground = target.Foreground;
            _themeSurfaceTimer.Start();
        }

        private void EnsureThemeSurfaceAnimator()
        {
            if (_themeSurfaceTimer != null) return;

            _themeSurfaceWatch = new Stopwatch();
            _themeSurfaceTimer = new Timer { Interval = 15 };
            _themeSurfaceTimer.Tick += ThemeSurfaceTimer_Tick;
            Disposed += (s, e) =>
            {
                _themeSurfaceTimer?.Stop();
                _themeSurfaceTimer?.Dispose();
                _themeSurfaceWatch?.Stop();
            };
        }

        private void ThemeSurfaceTimer_Tick(object sender, EventArgs e)
        {
            float progress = Math.Min(1F, (float)(_themeSurfaceWatch.Elapsed.TotalMilliseconds / ThemeSurfaceAnimationMs));
            float eased = LinearThemeEase(progress);
            ApplyThemeSurfaceColors(
                Blend(_themeSurfaceFromBackground, _themeSurfaceToBackground, eased),
                Blend(_themeSurfaceFromHeader, _themeSurfaceToHeader, eased),
                Blend(_themeSurfaceFromForeground, _themeSurfaceToForeground, eased));

            if (progress < 1F) return;

            _themeSurfaceTimer.Stop();
            _themeSurfaceWatch.Stop();
            AntdUI.Config.IsDark = _themeSurfaceTargetDark;
            ApplyThemeSurfaceColors(_themeSurfaceToBackground, _themeSurfaceToHeader, _themeSurfaceToForeground);
            _currentGamePage?.UpdateLaunchPanelColor();
            updateBadge.Invalidate();
        }

        private void btn_mode_Click(object sender, EventArgs e)
        {
            bool targetDark = _themeSurfaceTimer?.Enabled == true ? !_themeSurfaceTargetDark : !AntdUI.Config.IsDark;
            btn_mode.Toggle = targetDark;
            btn_mode.AnimateToDarkIcon(targetDark);
            var cfg = ConfigHelper.Load();
            cfg.ThemeMode = targetDark ? "dark" : "light";
            ConfigHelper.Save(cfg);
            ApplyThemeModeImmediate(targetDark);
            updateBadge.Invalidate();
        }

        private void ApplyThemeModeImmediate(bool targetDark)
        {
            _themeSurfaceTimer?.Stop();
            _themeSurfaceWatch?.Stop();
            AntdUI.Config.IsDark = targetDark;
            ApplyThemeSurfaces();
            _currentGamePage?.UpdateLaunchPanelColor();
            updateBadge.Invalidate();
        }

        private void colorTheme_ValueChanged(object sender, AntdUI.ColorEventArgs e)
        {
            AntdUI.Style.SetPrimary(e.Value);
            var cfg = ConfigHelper.Load();
            cfg.PrimaryColor = "#" + e.Value.ToHex();
            ConfigHelper.Save(cfg);
            Refresh();
        }

        private static float LinearThemeEase(float value)
        {
            value = Math.Max(0F, Math.Min(1F, value));
            const float softness = 0.1F;
            float smooth = value * value * (3F - 2F * value);
            return value * (1F - softness) + smooth * softness;
        }

        private static Color Blend(Color from, Color to, float amount)
        {
            amount = Math.Max(0F, Math.Min(1F, amount));
            return Color.FromArgb(
                (int)Math.Round(from.A + (to.A - from.A) * amount),
                (int)Math.Round(from.R + (to.R - from.R) * amount),
                (int)Math.Round(from.G + (to.G - from.G) * amount),
                (int)Math.Round(from.B + (to.B - from.B) * amount));
        }
    }
}
