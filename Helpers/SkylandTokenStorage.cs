using System;
using System.Collections.Generic;
using System.Linq;
using XelLauncher.Models;

namespace XelLauncher.Helpers
{
    public static class SkylandTokenStorage
    {
        public static List<string> GetTokens(AppConfig cfg)
        {
            if (!string.IsNullOrWhiteSpace(cfg.SkylandTokensEncrypted))
            {
                try
                {
                    return SkylandService.SplitTokens(SecretProtector.Unprotect(cfg.SkylandTokensEncrypted));
                }
                catch (Exception ex)
                {
                    LogHelper.LogError(ex, "SkylandTokenDecrypt");
                    return new List<string>();
                }
            }

            return SkylandService.SplitTokens(string.Join(";", cfg.SkylandTokens ?? new List<string>()));
        }

        public static void SetTokens(AppConfig cfg, IEnumerable<string> tokens)
        {
            var list = tokens?.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList() ?? new List<string>();
            cfg.SkylandTokensEncrypted = list.Count == 0
                ? ""
                : SecretProtector.Protect(string.Join(";", list));
            cfg.SkylandTokens = new List<string>();
        }

        public static void NormalizeBeforeSave(AppConfig cfg)
        {
            if (cfg == null) return;

            var plaintextTokens = cfg.SkylandTokens ?? new List<string>();
            if (plaintextTokens.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(cfg.SkylandTokensEncrypted))
                    SetTokens(cfg, plaintextTokens);
                else
                    cfg.SkylandTokens = new List<string>();
            }
        }
    }
}
