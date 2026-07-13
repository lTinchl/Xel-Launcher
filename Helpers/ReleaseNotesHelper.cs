using System;

namespace XelLauncher.Helpers
{
    internal static class ReleaseNotesHelper
    {
        private enum NotesLanguage
        {
            None,
            Chinese,
            English
        }

        public static string SelectLocalizedMarkdown(string markdown, bool preferEnglish)
        {
            if (string.IsNullOrWhiteSpace(markdown)) return markdown;

            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            var hasChinese = false;
            var hasEnglish = false;
            foreach (var line in lines)
            {
                var language = GetHeadingLanguage(line);
                if (language == NotesLanguage.Chinese) hasChinese = true;
                if (language == NotesLanguage.English) hasEnglish = true;
            }

            // Only filter explicitly bilingual notes. A release containing a single
            // language remains visible instead of producing an empty update panel.
            if (!hasChinese || !hasEnglish) return markdown;

            var preferred = preferEnglish ? NotesLanguage.English : NotesLanguage.Chinese;
            var start = -1;
            var end = lines.Length;
            for (var i = 0; i < lines.Length; i++)
            {
                var language = GetHeadingLanguage(lines[i]);
                if (start < 0)
                {
                    if (language == preferred) start = i;
                    continue;
                }

                if (language != NotesLanguage.None && language != preferred)
                {
                    end = i;
                    break;
                }
            }

            if (start < 0) return markdown;
            return string.Join(Environment.NewLine, lines, start, end - start).Trim();
        }

        private static NotesLanguage GetHeadingLanguage(string line)
        {
            var value = line?.Trim();
            if (string.IsNullOrEmpty(value) || !value.StartsWith("#", StringComparison.Ordinal))
                return NotesLanguage.None;

            var heading = value.TrimStart('#').Trim();
            if (heading.StartsWith("更新重点", StringComparison.OrdinalIgnoreCase) ||
                heading.StartsWith("更新内容", StringComparison.OrdinalIgnoreCase) ||
                heading.StartsWith("更新日志", StringComparison.OrdinalIgnoreCase) ||
                heading.StartsWith("主要更新", StringComparison.OrdinalIgnoreCase) ||
                heading.StartsWith("变更内容", StringComparison.OrdinalIgnoreCase))
                return NotesLanguage.Chinese;

            if (heading.StartsWith("What's Changed", StringComparison.OrdinalIgnoreCase) ||
                heading.StartsWith("What’s Changed", StringComparison.OrdinalIgnoreCase) ||
                heading.StartsWith("Release Notes", StringComparison.OrdinalIgnoreCase) ||
                heading.StartsWith("Changes", StringComparison.OrdinalIgnoreCase) ||
                heading.StartsWith("Highlights", StringComparison.OrdinalIgnoreCase))
                return NotesLanguage.English;

            return NotesLanguage.None;
        }
    }
}
