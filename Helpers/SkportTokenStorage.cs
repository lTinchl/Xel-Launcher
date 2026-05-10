using System;
using System.Collections.Generic;
using System.Linq;
using XelLauncher.Models;

namespace XelLauncher.Helpers
{
    public static class SkportTokenStorage
    {
        public static List<string> GetTokens(AppConfig cfg)
        {
            if (cfg == null) return new List<string>();

            if (!string.IsNullOrWhiteSpace(cfg.SkportTokensEncrypted))
            {
                try
                {
                    return SkportService.SplitTokens(SecretProtector.Unprotect(cfg.SkportTokensEncrypted));
                }
                catch (Exception ex)
                {
                    LogHelper.LogError(ex, "SkportTokenDecrypt");
                    return new List<string>();
                }
            }

            return SkportService.SplitTokens(string.Join(";", cfg.SkportTokens ?? new List<string>()));
        }

        public static void SetTokens(AppConfig cfg, IEnumerable<string> tokens)
        {
            if (cfg == null) return;
            var list = tokens?.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList() ?? new List<string>();
            cfg.SkportTokensEncrypted = list.Count == 0
                ? ""
                : SecretProtector.Protect(string.Join(";", list));
            cfg.SkportTokens = new List<string>();
        }

        public static void NormalizeBeforeSave(AppConfig cfg)
        {
            if (cfg == null) return;
            var plaintextSkportTokens = cfg.SkportTokens ?? new List<string>();
            if (plaintextSkportTokens.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(cfg.SkportTokensEncrypted))
                    SetTokens(cfg, plaintextSkportTokens);
                else
                    cfg.SkportTokens = new List<string>();
            }
        }
    }
}
