using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sims4Creator.Editor
{
    /// <summary>
    /// Reads Claude Code's on-disk session transcripts so the chat window can list past
    /// conversations for the current working directory and re-open them. Claude Code stores
    /// them as <c>~/.claude/projects/&lt;encoded-cwd&gt;/&lt;session-uuid&gt;.jsonl</c>, where the cwd is
    /// encoded by replacing each non-alphanumeric character with '-'. The file NAME is the
    /// session id; the lines are the conversation. There is no machine-readable list command,
    /// so we parse the folder ourselves.
    /// </summary>
    public static class ClaudeSessionStore
    {
        public struct SessionInfo
        {
            public string Id;
            public string Title;
            public DateTime Modified;
            public string Path;
        }

        public struct TranscriptItem
        {
            public string Kind; // "user" | "assistant" | "tool"
            public string Text;
            public string ToolName;
        }

        static string ConfigDir()
        {
            var cfg = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
            if (!string.IsNullOrEmpty(cfg)) return cfg;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        }

        public static string ProjectsRoot() => Path.Combine(ConfigDir(), "projects");

        /// <summary>Encode a working dir to its project-folder name: every non-alphanumeric char → '-'.</summary>
        public static string EncodeCwd(string cwd)
        {
            if (string.IsNullOrEmpty(cwd)) return "";
            cwd = cwd.TrimEnd('/', '\\', ' '); // a trailing separator (e.g. from Browse) would skew the folder name
            var sb = new StringBuilder(cwd.Length);
            foreach (char c in cwd)
                sb.Append(char.IsLetterOrDigit(c) ? c : '-');
            return sb.ToString();
        }

        /// <summary>Find the project folder for a cwd, matching case-insensitively (drive-letter case differs).</summary>
        public static string FindProjectDir(string cwd)
        {
            try
            {
                string root = ProjectsRoot();
                if (!Directory.Exists(root)) return null;
                string enc = EncodeCwd(cwd);
                foreach (var d in Directory.GetDirectories(root))
                    if (string.Equals(Path.GetFileName(d), enc, StringComparison.OrdinalIgnoreCase))
                        return d;
            }
            catch { /* ignore */ }
            return null;
        }

        public static List<SessionInfo> ListSessions(string cwd, int max)
        {
            var list = new List<SessionInfo>();
            try
            {
                string dir = FindProjectDir(cwd);
                if (dir == null) return list;

                var files = new List<FileInfo>();
                foreach (var f in Directory.GetFiles(dir, "*.jsonl")) files.Add(new FileInfo(f));
                files.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));

                int n = 0;
                foreach (var fi in files)
                {
                    if (n++ >= max) break;
                    list.Add(new SessionInfo
                    {
                        Id = Path.GetFileNameWithoutExtension(fi.Name),
                        Title = ReadTitle(fi.FullName),
                        Modified = fi.LastWriteTime,
                        Path = fi.FullName,
                    });
                }
            }
            catch { /* ignore */ }
            return list;
        }

        static string ReadTitle(string path)
        {
            try
            {
                int count = 0;
                string firstUser = null;
                foreach (var line in File.ReadLines(path))
                {
                    if (count++ > 80) break;
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (!(ClaudeJson.Deserialize(line) is Dictionary<string, object> d)) continue;

                    var type = ClaudeJson.GetString(d, "type");
                    if (type == "ai-title")
                    {
                        var t = ClaudeJson.GetString(d, "title");
                        if (!string.IsNullOrWhiteSpace(t)) return Clip(t, 64);
                    }
                    if (firstUser == null && type == "user")
                    {
                        var txt = FirstText(ClaudeJson.Get(d, "message"));
                        if (!string.IsNullOrWhiteSpace(txt)) firstUser = txt;
                    }
                }
                return firstUser != null ? Clip(firstUser, 64) : "(untitled)";
            }
            catch { return "(unreadable)"; }
        }

        public static List<TranscriptItem> LoadTranscript(string path, int maxItems)
        {
            var items = new List<TranscriptItem>();
            try
            {
                foreach (var line in File.ReadLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (!(ClaudeJson.Deserialize(line) is Dictionary<string, object> d)) continue;

                    var type = ClaudeJson.GetString(d, "type");
                    if (type != "user" && type != "assistant") continue;
                    EmitTranscript(ClaudeJson.Get(d, "message"), type, items);
                }
            }
            catch { /* ignore */ }

            if (items.Count > maxItems)
                items.RemoveRange(0, items.Count - maxItems);
            return items;
        }

        static void EmitTranscript(object message, string type, List<TranscriptItem> items)
        {
            if (!(message is Dictionary<string, object> md)) return;
            var content = ClaudeJson.Get(md, "content");

            if (content is string s)
            {
                if (!string.IsNullOrWhiteSpace(s))
                    items.Add(new TranscriptItem { Kind = type, Text = s });
                return;
            }
            if (!(content is List<object> list)) return;

            foreach (var b in list)
            {
                if (!(b is Dictionary<string, object> bd)) continue;
                switch (ClaudeJson.GetString(bd, "type"))
                {
                    case "text":
                        var t = ClaudeJson.GetString(bd, "text");
                        if (!string.IsNullOrWhiteSpace(t))
                            items.Add(new TranscriptItem { Kind = type, Text = t });
                        break;
                    case "tool_use":
                        items.Add(new TranscriptItem { Kind = "tool", ToolName = ClaudeJson.GetString(bd, "name") });
                        break;
                }
            }
        }

        static string FirstText(object message)
        {
            if (!(message is Dictionary<string, object> md)) return null;
            var content = ClaudeJson.Get(md, "content");
            if (content is string s) return s;
            if (!(content is List<object> list)) return null;
            foreach (var b in list)
                if (b is Dictionary<string, object> bd && ClaudeJson.GetString(bd, "type") == "text")
                {
                    var t = ClaudeJson.GetString(bd, "text");
                    if (!string.IsNullOrWhiteSpace(t)) return t;
                }
            return null;
        }

        static string Clip(string s, int n)
        {
            s = s.Replace("\r", " ").Replace("\n", " ").Trim();
            return s.Length <= n ? s : s.Substring(0, n) + "…";
        }
    }
}
