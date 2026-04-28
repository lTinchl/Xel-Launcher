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
        // ── 旧式 InjectIcon（仅对 SHBrowseForFolder 的 #32770 对话框有效，保留备用）──
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

        // ── IFileOpenDialog COM 直接调用──────────────────────────────────────────
        // 解决 Windows 11 24H2 以管理员权限运行时，FolderBrowserDialog 通过
        // IFileDialogEvents::Advise 注册回调，UIPI 阻断跨权限级别 COM 回调，
        // 导致主线程死锁、进程被 WER 强杀（app.log 无任何记录）的问题。
        // 直接使用 IFileOpenDialog 且不注册 Advise 回调，可完全绕过该路径。
        // ─────────────────────────────────────────────────────────────────────────

        // 完整 vtable 顺序：IModalWindow::Show, 然后所有 IFileDialog 方法, 最后 IFileOpenDialog 扩展
        [ComImport, Guid("D57C7288-D4AD-4768-BE02-9D969532D960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig] int Show(IntPtr hwndOwner);                                                  // IModalWindow
            void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);                                   // IFileDialog
            void SetFileTypeIndex(uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise(IntPtr pfde, out uint pdwCookie);
            void Unadvise(uint dwCookie);
            void SetOptions(uint fos);
            void GetOptions(out uint pfos);
            void SetDefaultFolder(IntPtr psi);
            void SetFolder([MarshalAs(UnmanagedType.Interface)] IShellItem psi);
            void GetFolder(out IntPtr ppsi);
            void GetCurrentSelection(out IntPtr ppsi);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetFileName(out IntPtr pszName);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            void GetResult([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);
            void AddPlace(IntPtr psi, int fdap);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            void Close(int hr);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr pFilter);
            void GetResults(out IntPtr ppenum);                                                        // IFileOpenDialog
            void GetSelectedItems(out IntPtr ppenum);
        }

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
            void GetParent(out IntPtr ppsi);
            void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IntPtr psi, uint hint, out int piOrder);
        }

        [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
        private class FileOpenDialogClass { }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHCreateItemFromParsingName(
            string pszPath, IntPtr pbc, ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

        private static readonly Guid IID_IShellItem = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");
        private const uint FOS_PICKFOLDERS    = 0x00000020;
        private const uint FOS_FORCEFILESYSTEM = 0x00000040;
        private const uint SIGDN_FILESYSPATH  = 0x80058000;

        /// <summary>
        /// 通过 IFileOpenDialog COM 接口显示文件夹选择窗口。
        /// 兼容 Windows 11 24H2 以管理员权限运行的场景。
        /// </summary>
        /// <returns>用户选择的路径，取消或出错时返回 null。</returns>
        public static string BrowseFolder(IntPtr ownerHwnd, string title, string initialDir = null)
        {
            IFileOpenDialog dialog = null;
            try
            {
                dialog = (IFileOpenDialog)new FileOpenDialogClass();
                dialog.GetOptions(out uint opts);
                dialog.SetOptions(opts | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM);
                if (!string.IsNullOrEmpty(title))
                    dialog.SetTitle(title);
                if (!string.IsNullOrEmpty(initialDir) && Directory.Exists(initialDir))
                {
                    var riid = IID_IShellItem;
                    if (SHCreateItemFromParsingName(initialDir, IntPtr.Zero, ref riid, out IShellItem folder) == 0 && folder != null)
                    {
                        dialog.SetFolder(folder);
                        Marshal.ReleaseComObject(folder);
                    }
                }
                int hr = dialog.Show(ownerHwnd);
                if (hr != 0) return null;   // 用户取消 (0x800704C7) 或其他错误
                dialog.GetResult(out IShellItem result);
                if (result == null) return null;
                result.GetDisplayName(SIGDN_FILESYSPATH, out string path);
                Marshal.ReleaseComObject(result);
                return path;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (dialog != null) Marshal.ReleaseComObject(dialog);
            }
        }
    }
}
