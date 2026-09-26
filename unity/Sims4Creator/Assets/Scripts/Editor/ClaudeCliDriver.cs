using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace Sims4Creator.Editor
{
    public enum ClaudeEventKind { Info, Init, Assistant, Tool, ToolResult, Result, Error, Exited, PartialStart, Partial, PartialEnd }

    /// <summary>One parsed event surfaced from the CLI stream, consumed on the UI thread.</summary>
    public sealed class ClaudeEvent
    {
        public ClaudeEventKind Kind;
        public string Text;        // primary text payload
        public string ToolName;    // for Tool
        public string SessionId;   // for Init
        public string Model;       // for Init
        public string AuthSource;  // for Init (apiKeySource)
        public bool Success;       // for Result / Exited
        public bool Stopped;       // for Exited — true when the turn was killed by a user Stop
        public int ExitCode;       // for Exited
        public double DurationMs;  // for Result
        public double CostUsd;     // for Result
    }

    public sealed class ClaudeChatConfig
    {
        public string ClaudePath;
        public string WorkingDirectory;
        public string PermissionMode = "bypassPermissions";
        public string Model = "";          // "" = CLI default
        public string Effort = "";         // "" = CLI default (low|medium|high|xhigh|max)
        public bool IncludePartial = true; // token-by-token streaming
        public string ExtraArgs = "";
        public bool ClearApiKey = true;
        public string AppendSystemPrompt = ""; // --append-system-prompt value (widget vocabulary)
    }

    /// <summary>
    /// Drives the real <c>claude</c> CLI as a child process in headless stream-json mode,
    /// one process per turn. The prompt is fed over <b>stdin</b> (no command-line escaping
    /// or length limits for arbitrary multi-line text); stdout is read line-by-line on a
    /// background thread, parsed as NDJSON, and surfaced as <see cref="ClaudeEvent"/>s on a
    /// thread-safe queue that the Editor window drains on the main thread.
    ///
    /// This uses the user's logged-in Claude Code SUBSCRIPTION (it invokes the actual binary
    /// the user authenticated with via <c>/login</c>), and strips the metered-billing env vars
    /// (<c>ANTHROPIC_API_KEY</c>, <c>ANTHROPIC_AUTH_TOKEN</c>, cloud-provider flags) from the
    /// child environment so usage cannot silently fall back to metered API or cloud billing.
    /// It never reads or reuses the OAuth token itself, which keeps it ToS-compliant.
    /// </summary>
    public sealed class ClaudeCliDriver
    {
        readonly ConcurrentQueue<ClaudeEvent> _events = new ConcurrentQueue<ClaudeEvent>();
        Process _process;
        volatile bool _running;
        volatile bool _stopRequested;
        volatile bool _partial;
        volatile int _generation;
        volatile string _sessionId;

        public bool IsRunning => _running;
        public string SessionId => _sessionId;
        public ConcurrentQueue<ClaudeEvent> Events => _events;

        /// <summary>Start one turn. Pass the captured session id to resume context (null for a fresh chat).</summary>
        public bool TryStartTurn(string prompt, string resumeSessionId, ClaudeChatConfig cfg, out string error)
        {
            error = null;
            if (_running) { error = "A turn is already running."; return false; }
            if (cfg == null || string.IsNullOrEmpty(cfg.ClaudePath)) { error = "Claude executable path is not set."; return false; }
            if (!File.Exists(cfg.ClaudePath)) { error = "Claude executable not found: " + cfg.ClaudePath; return false; }

            if (_process != null) { try { _process.Dispose(); } catch { /* ignore */ } _process = null; }

            BuildCommand(cfg, resumeSessionId, out string fileName, out string args);

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
                WorkingDirectory = string.IsNullOrEmpty(cfg.WorkingDirectory)
                    ? Environment.CurrentDirectory
                    : cfg.WorkingDirectory,
            };

            if (cfg.ClearApiKey)
            {
                // Force the persisted subscription login. Each of these env vars takes
                // precedence over the logged-in subscription and would route usage to METERED
                // API or cloud billing instead. CLAUDE_CODE_OAUTH_TOKEN is intentionally left
                // intact — it is itself subscription-funded.
                foreach (var v in new[]
                {
                    "ANTHROPIC_API_KEY", "ANTHROPIC_AUTH_TOKEN",
                    "CLAUDE_CODE_USE_BEDROCK", "CLAUDE_CODE_USE_VERTEX", "CLAUDE_CODE_USE_FOUNDRY",
                })
                {
                    psi.EnvironmentVariables.Remove(v);
                }
            }

            try
            {
                _process = new Process { StartInfo = psi };
                _process.Start();
            }
            catch (Exception ex)
            {
                error = "Failed to launch claude: " + ex.Message;
                _process = null;
                return false;
            }

            _running = true;
            _stopRequested = false;
            _partial = cfg.IncludePartial;
            int gen = ++_generation;

            var proc = _process; // capture so a fast next-turn can't repoint these threads

            // Start the output readers FIRST so stdout/stderr are being drained before we feed
            // stdin. Otherwise a child that fills its stdout pipe buffer while we're still
            // writing stdin deadlocks: both sides block and the turn hangs forever.
            var stderrThread = new Thread(() => ReadStderr(proc)) { IsBackground = true, Name = "claude-stderr" };
            var stdoutThread = new Thread(() => ReadStdout(proc, gen, stderrThread)) { IsBackground = true, Name = "claude-stdout" };
            stderrThread.Start();
            stdoutThread.Start();

            // Feed the prompt over stdin on its own thread (UTF-8 via BaseStream so we don't
            // depend on the StandardInputEncoding setter; off the UI thread so a large prompt
            // never stalls the editor), then close stdin to signal end-of-input.
            new Thread(() =>
            {
                try
                {
                    var bytes = new UTF8Encoding(false).GetBytes(prompt ?? "");
                    var stdin = proc.StandardInput.BaseStream;
                    stdin.Write(bytes, 0, bytes.Length);
                    stdin.Flush();
                    proc.StandardInput.Close();
                }
                catch (Exception ex)
                {
                    Enqueue(new ClaudeEvent { Kind = ClaudeEventKind.Error, Text = "Failed to send prompt: " + ex.Message });
                }
            }) { IsBackground = true, Name = "claude-stdin" }.Start();

            return true;
        }

        public void Stop()
        {
            _stopRequested = true;
            var proc = _process;
            try
            {
                if (proc != null && !proc.HasExited)
                {
                    // claude spawns child processes (node, etc.); kill the whole tree via
                    // taskkill (the Process.Kill(bool) tree overload isn't available under
                    // Unity's ".NET Framework" API compatibility level, so we can't rely on it).
                    int pid = -1;
                    try { pid = proc.Id; } catch { /* already exited */ }
                    if (pid > 0) TryTaskKillTree(pid);
                    try { if (!proc.HasExited) proc.Kill(); } catch { /* ignore */ }
                }
            }
            catch { /* already gone */ }
            _running = false;
        }

        static void TryTaskKillTree(int pid)
        {
            try
            {
                var psi = new ProcessStartInfo("taskkill", "/PID " + pid + " /T /F")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using (var p = Process.Start(psi)) p.WaitForExit(2000);
            }
            catch { /* taskkill unavailable; the plain Kill() below still runs */ }
        }

        // ---- command construction ----

        void BuildCommand(ClaudeChatConfig cfg, string resumeSessionId, out string fileName, out string args)
        {
            var sb = new StringBuilder();
            sb.Append("-p --output-format stream-json --verbose");
            if (cfg.IncludePartial)
                sb.Append(" --include-partial-messages");
            if (!string.IsNullOrEmpty(cfg.PermissionMode))
                sb.Append(" --permission-mode ").Append(cfg.PermissionMode);
            if (!string.IsNullOrEmpty(cfg.Model))
                sb.Append(" --model ").Append(cfg.Model);
            if (!string.IsNullOrEmpty(cfg.Effort))
                sb.Append(" --effort ").Append(cfg.Effort);
            if (!string.IsNullOrEmpty(resumeSessionId))
                sb.Append(" --resume ").Append(resumeSessionId);
            if (!string.IsNullOrWhiteSpace(cfg.ExtraArgs))
                sb.Append(' ').Append(cfg.ExtraArgs.Trim());
            bool hasSysPrompt = !string.IsNullOrEmpty(cfg.AppendSystemPrompt);
            if (hasSysPrompt)
                sb.Append(" --append-system-prompt \"").Append(cfg.AppendSystemPrompt.Replace("\"", "")).Append("\"");

            string exe = ResolveWindowsExecutable(cfg.ClaudePath);
            string ext = Path.GetExtension(exe).ToLowerInvariant();
            if (ext == ".exe")
            {
                fileName = exe;
                args = sb.ToString();
            }
            else
            {
                // .cmd/.bat shims (and the extensionless npm bash shim) are NOT Win32
                // executables — .NET can't CreateProcess them directly. Route through cmd.exe.
                // No user free-text is on this command line (the prompt goes via stdin), so
                // quoting the path is sufficient.
                fileName = "cmd.exe";
                // A spaced/quoted flag value (--append-system-prompt "...") needs cmd's
                // /s /c "<whole command>" form so the inner quotes survive intact.
                args = hasSysPrompt
                    ? "/s /c \"\"" + exe + "\" " + sb + "\""
                    : "/c \"" + exe + "\" " + sb;
            }
        }

        /// <summary>
        /// npm's global install drops THREE entries: <c>claude</c> (a Unix bash shim with NO
        /// extension — "%1 is not a valid Win32 application" if you exec it), <c>claude.cmd</c>,
        /// and <c>claude.ps1</c>. <c>where claude</c> can return the extensionless one first.
        /// Map any non-.exe / extensionless path to a runnable Windows shim sibling.
        /// </summary>
        static string ResolveWindowsExecutable(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".exe" || ext == ".cmd" || ext == ".bat") return path;
            foreach (var cand in new[] { path + ".cmd", path + ".exe", path + ".bat" })
                if (File.Exists(cand)) return cand;
            return path; // last resort
        }

        // ---- background readers ----

        void ReadStdout(Process proc, int gen, Thread stderrThread)
        {
            try
            {
                var reader = proc.StandardOutput;
                string line;
                while ((line = reader.ReadLine()) != null)
                    ParseLine(line);
            }
            catch (Exception ex)
            {
                Enqueue(new ClaudeEvent { Kind = ClaudeEventKind.Error, Text = "stdout read error: " + ex.Message });
            }
            finally
            {
                int code = -1;
                try { proc.WaitForExit(); code = proc.ExitCode; } catch { /* killed */ }

                // Let stderr finish so any final error lines land BEFORE the "exited" line.
                try { stderrThread?.Join(750); } catch { /* ignore */ }

                bool stopped = _stopRequested;
                Enqueue(new ClaudeEvent
                {
                    Kind = ClaudeEventKind.Exited,
                    ExitCode = code,
                    Success = code == 0 || stopped,
                    Stopped = stopped,
                });

                // Dispose this turn's process; clear the field only if it's still the current one.
                try { proc.Dispose(); } catch { /* ignore */ }
                if (ReferenceEquals(_process, proc)) _process = null;

                // Only the turn that owns the current generation may clear the running flag,
                // so a stale reader can't unguard a turn that has already started after it.
                if (gen == _generation) _running = false;
            }
        }

        void ReadStderr(Process proc)
        {
            try
            {
                var reader = proc.StandardError;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    Enqueue(new ClaudeEvent { Kind = ClaudeEventKind.Error, Text = line });
                }
            }
            catch { /* stream closed on kill */ }
        }

        // ---- NDJSON event parsing ----

        void ParseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            object root;
            try { root = ClaudeJson.Deserialize(line); }
            catch { root = null; }

            if (!(root is Dictionary<string, object> dict))
            {
                Enqueue(new ClaudeEvent { Kind = ClaudeEventKind.Info, Text = line });
                return;
            }

            var sid = ClaudeJson.GetString(dict, "session_id");
            if (!string.IsNullOrEmpty(sid)) _sessionId = sid;

            string type = ClaudeJson.GetString(dict, "type");
            string subtype = ClaudeJson.GetString(dict, "subtype");

            switch (type)
            {
                case "system":
                    if (subtype == "init")
                    {
                        Enqueue(new ClaudeEvent
                        {
                            Kind = ClaudeEventKind.Init,
                            SessionId = sid,
                            Model = ClaudeJson.GetString(dict, "model"),
                            AuthSource = ClaudeJson.GetString(dict, "apiKeySource"),
                        });
                    }
                    else if (subtype == "api_retry")
                    {
                        Enqueue(new ClaudeEvent
                        {
                            Kind = ClaudeEventKind.Info,
                            Text = "retrying: " + (ClaudeJson.GetString(dict, "error") ?? "transient API error"),
                        });
                    }
                    break;

                case "assistant":
                    // In streaming mode the text arrives as deltas (stream_event); take only the
                    // tool_use blocks from the final message so we don't duplicate the text.
                    EmitMessageBlocks(ClaudeJson.Get(dict, "message"), assistant: true, skipText: _partial);
                    break;

                case "user":
                    EmitMessageBlocks(ClaudeJson.Get(dict, "message"), assistant: false, skipText: false);
                    break;

                case "stream_event":
                    if (_partial) HandleStreamEvent(ClaudeJson.Get(dict, "event"));
                    break;

                case "result":
                    bool isError = ClaudeJson.Get(dict, "is_error") is bool b && b;
                    Enqueue(new ClaudeEvent
                    {
                        Kind = ClaudeEventKind.Result,
                        Success = !isError,
                        DurationMs = AsDouble(ClaudeJson.Get(dict, "duration_ms")),
                        CostUsd = AsDouble(ClaudeJson.Get(dict, "total_cost_usd")),
                        Text = isError ? ClaudeJson.GetString(dict, "result") : null,
                    });
                    break;
            }
        }

        void EmitMessageBlocks(object message, bool assistant, bool skipText)
        {
            if (!(message is Dictionary<string, object> mdict)) return;

            var contentObj = ClaudeJson.Get(mdict, "content");
            if (contentObj is string plain)
            {
                if (assistant && !skipText && !string.IsNullOrEmpty(plain))
                    Enqueue(new ClaudeEvent { Kind = ClaudeEventKind.Assistant, Text = plain });
                return;
            }

            if (!(contentObj is List<object> content)) return;

            foreach (var block in content)
            {
                if (!(block is Dictionary<string, object> bd)) continue;
                switch (ClaudeJson.GetString(bd, "type"))
                {
                    case "text":
                        if (skipText) break;
                        var txt = ClaudeJson.GetString(bd, "text");
                        if (!string.IsNullOrEmpty(txt))
                            Enqueue(new ClaudeEvent { Kind = ClaudeEventKind.Assistant, Text = txt });
                        break;
                    case "tool_use":
                        Enqueue(new ClaudeEvent
                        {
                            Kind = ClaudeEventKind.Tool,
                            ToolName = ClaudeJson.GetString(bd, "name"),
                            Text = SummarizeToolInput(ClaudeJson.Get(bd, "input")),
                        });
                        break;
                    case "tool_result":
                        Enqueue(new ClaudeEvent
                        {
                            Kind = ClaudeEventKind.ToolResult,
                            Text = Truncate(OneLine(ExtractText(ClaudeJson.Get(bd, "content"))), 200),
                        });
                        break;
                }
            }
        }

        // ---- partial-message (token streaming) parsing ----

        void HandleStreamEvent(object evt)
        {
            if (!(evt is Dictionary<string, object> e)) return;
            switch (ClaudeJson.GetString(e, "type"))
            {
                case "content_block_start":
                    // Only start a fresh streaming line for TEXT blocks. tool_use blocks are
                    // surfaced (with their full input) from the final assistant message instead.
                    if (ClaudeJson.GetString(ClaudeJson.Get(e, "content_block"), "type") == "text")
                        Enqueue(new ClaudeEvent { Kind = ClaudeEventKind.PartialStart });
                    break;
                case "content_block_delta":
                    var d = ClaudeJson.Get(e, "delta");
                    if (ClaudeJson.GetString(d, "type") == "text_delta")
                    {
                        var t = ClaudeJson.GetString(d, "text");
                        if (!string.IsNullOrEmpty(t))
                            Enqueue(new ClaudeEvent { Kind = ClaudeEventKind.Partial, Text = t });
                    }
                    break;
                case "message_stop":
                    Enqueue(new ClaudeEvent { Kind = ClaudeEventKind.PartialEnd });
                    break;
            }
        }

        // ---- helpers ----

        void Enqueue(ClaudeEvent ev) => _events.Enqueue(ev);

        static string SummarizeToolInput(object input)
        {
            if (!(input is Dictionary<string, object> d)) return "";
            string[] keys = { "file_path", "path", "command", "pattern", "query", "url", "description", "prompt" };
            foreach (var k in keys)
                if (d.TryGetValue(k, out var v) && v is string s && s.Length > 0)
                    return Truncate(OneLine(s), 120);
            foreach (var kv in d)
                if (kv.Value is string s2 && s2.Length > 0)
                    return Truncate(OneLine(s2), 120);
            return "";
        }

        static string ExtractText(object content)
        {
            if (content is string s) return s;
            if (content is List<object> list)
            {
                var sb = new StringBuilder();
                foreach (var item in list)
                    if (item is Dictionary<string, object> d && d.TryGetValue("text", out var t) && t is string ts)
                        sb.Append(ts).Append(' ');
                return sb.ToString();
            }
            return content?.ToString() ?? "";
        }

        static double AsDouble(object o)
        {
            if (o is double d) return d;
            if (o != null && double.TryParse(o.ToString(), out var r)) return r;
            return 0;
        }

        static string OneLine(string s) => s == null ? "" : s.Replace("\r", " ").Replace("\n", " ");

        static string Truncate(string s, int n) =>
            string.IsNullOrEmpty(s) || s.Length <= n ? s : s.Substring(0, n) + "…";
    }
}
