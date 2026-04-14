using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace XelLauncher.Helpers
{
    public static class DialogHelper
    {
        [DllImport("user32.dll")] static extern bool EnumThreadWindows(int dwThreadId, EnumWindowsProc lpfn, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public static void InjectIcon(Icon icon)
        {
            var timer = new System.Windows.Forms.Timer { Interval = 50 };
            timer.Tick += (s, e) =>
            {
                IntPtr found = IntPtr.Zero;
                EnumThreadWindows(AppDomain.GetCurrentThreadId(), (hwnd, _) =>
                {
                    var sb = new StringBuilder(256);
                    GetClassName(hwnd, sb, 256);
                    if (sb.ToString() == "#32770") { found = hwnd; return false; }
                    return true;
                }, IntPtr.Zero);
                if (found == IntPtr.Zero) return;
                timer.Stop();
                var hIcon = icon.Handle;
                SendMessage(found, 0x0080, (IntPtr)1, hIcon);
                SendMessage(found, 0x0080, (IntPtr)0, hIcon);
            };
            timer.Start();
        }
    }
}
