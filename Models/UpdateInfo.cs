// Models/UpdateInfo.cs
namespace XelLauncher.Models
{
    public class UpdateInfo
    {
        /// <summary>最新版本号，如 "0.1.6"（已去掉 v 前缀）</summary>
        public string LatestVersion { get; set; }
        /// <summary>GitHub Release body（更新日志）</summary>
        public string Changelog { get; set; }
        /// <summary>安装版 .exe 的直链，可为 null（Asset 不存在时）</summary>
        public string SetupDownloadUrl { get; set; }
        /// <summary>便携版 .zip 的直链，可为 null（Asset 不存在时）</summary>
        public string PortableDownloadUrl { get; set; }
        /// <summary>GitHub Release 页面链接（分流备用）</summary>
        public string ReleasePageUrl { get; set; }
    }
}
