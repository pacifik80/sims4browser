using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sims4Creator.Editor
{
    /// <summary>
    /// Builds a UI Toolkit <see cref="VisualElement"/> tree from markdown: headings, paragraphs,
    /// bullet/ordered lists, task checkboxes, blockquotes, fenced code blocks (monospace + Copy),
    /// tables (flex grid), horizontal rules, and inline bold/italic/code/strikethrough/links.
    /// Styling comes from the chat USS (class names below); this only builds structure + text.
    /// </summary>
    public sealed class ClaudeMarkdownRenderer
    {
        enum BType { Para, H1, H2, H3, Bullet, Ordered, Quote, Code, Table, Rule }

        sealed class Block
        {
            public BType Type;
            public string Text;
            public int Indent;
            public string Marker; // ordered number, or code language
            public List<string[]> Rows;
        }

        // Private-Use-Area sentinels, built from hex so the SOURCE stays pure ASCII (no invisible
        // chars). They cannot occur in model text, so protected spans never collide with ordinary
        // prose like "L2 cache" / "CO2"; the closing sentinel bounds the captured index.
        static readonly string SCode = ((char)0xE000).ToString();
        static readonly string SLink = ((char)0xE001).ToString();
        static readonly string SEnd = ((char)0xE002).ToString();

        Font _mono;
        readonly string _codeHex;
        readonly string _linkHex;

        /// <summary>Working dir used to resolve relative asset paths for inline previews.</summary>
        public string WorkingDir;

        public ClaudeMarkdownRenderer(bool pro)
        {
            _codeHex = pro ? "#4ec9b0" : "#0a7d6b";
            _linkHex = pro ? "#7aa2f7" : "#1a5fb4";
        }

        Font Mono()
        {
            if (_mono == null)
                try { _mono = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Cascadia Mono", "JetBrains Mono", "Courier New", "Menlo" }, 12); }
                catch { _mono = null; }
            return _mono;
        }

        /// <summary>Build a container holding one child element per markdown block.</summary>
        public VisualElement Build(string markdown)
        {
            var body = new VisualElement();
            body.AddToClassList("md-body");
            if (!string.IsNullOrEmpty(markdown))
                foreach (var b in ParseBlocks(markdown))
                    body.Add(BuildBlock(b));
            return body;
        }

        // ----------------------------------------------------------- block → element

        VisualElement BuildBlock(Block b)
        {
            switch (b.Type)
            {
                case BType.H1: return RichLabel(b.Text, "md-h1");
                case BType.H2: return RichLabel(b.Text, "md-h2");
                case BType.H3: return RichLabel(b.Text, "md-h3");
                case BType.Para: return BuildParagraph(b.Text);
                case BType.Bullet: return ListItem(b, "•");
                case BType.Ordered: return ListItem(b, b.Marker + ".");
                case BType.Quote: return Quote(b.Text);
                case BType.Code: return CodeBlock(b.Text, b.Marker);
                case BType.Table: return Table(b.Rows);
                case BType.Rule: return Rule();
                default: return RichLabel(b.Text, "md-p");
            }
        }

        Label RichLabel(string text, string cls)
        {
            var l = new Label(Inline(text)) { enableRichText = true };
            l.AddToClassList(cls);
            l.selection.isSelectable = true;
            return l;
        }

        VisualElement ListItem(Block b, string marker)
        {
            string text = b.Text;
            if (text.StartsWith("[x]") || text.StartsWith("[X]")) { marker = "☑"; text = text.Substring(3).TrimStart(); }
            else if (text.StartsWith("[ ]")) { marker = "☐"; text = text.Substring(3).TrimStart(); }

            var row = new VisualElement();
            row.AddToClassList("md-li");
            if (b.Indent > 0) row.style.marginLeft = Mathf.Min(b.Indent, 8) * 6;
            var m = new Label(marker);
            m.AddToClassList("md-li__marker");
            var t = new Label(Inline(text)) { enableRichText = true };
            t.AddToClassList("md-li__text");
            t.selection.isSelectable = true;
            row.Add(m);
            row.Add(t);
            return row;
        }

        VisualElement Quote(string text)
        {
            var q = new VisualElement();
            q.AddToClassList("md-quote");
            var l = new Label(Inline(text)) { enableRichText = true };
            l.selection.isSelectable = true;
            q.Add(l);
            return q;
        }

        VisualElement CodeBlock(string code, string lang)
        {
            var box = new VisualElement();
            box.AddToClassList("md-code");

            var head = new VisualElement();
            head.AddToClassList("md-code__head");
            var langLabel = new Label(string.IsNullOrEmpty(lang) ? "code" : lang);
            langLabel.AddToClassList("md-code__lang");
            head.Add(langLabel);
            head.Add(Spacer());
            var copy = new Button(() => EditorGUIUtility.systemCopyBuffer = code) { text = "Copy" };
            copy.AddToClassList("md-code__copy");
            head.Add(copy);
            box.Add(head);

            var bodyScroll = new ScrollView(ScrollViewMode.Horizontal);
            bodyScroll.AddToClassList("md-code__scroll");
            var body = new Label(code) { enableRichText = false };
            body.AddToClassList("md-code__body");
            body.selection.isSelectable = true;
            var mono = Mono();
            if (mono != null) body.style.unityFontDefinition = FontDefinition.FromFont(mono);
            bodyScroll.Add(body);
            box.Add(bodyScroll);
            return box;
        }

        VisualElement Table(List<string[]> rows)
        {
            var table = new VisualElement();
            table.AddToClassList("md-table");
            if (rows == null) return table;
            int cols = 0;
            foreach (var r in rows) cols = Mathf.Max(cols, r.Length);

            for (int ri = 0; ri < rows.Count; ri++)
            {
                var row = new VisualElement();
                row.AddToClassList("md-row");
                if (ri == 0) row.AddToClassList("md-row--head");
                else if (ri % 2 == 1) row.AddToClassList("md-row--alt");
                var cells = rows[ri];
                for (int ci = 0; ci < cols; ci++)
                {
                    var cell = new Label(Inline(ci < cells.Length ? cells[ci] : "")) { enableRichText = true };
                    cell.AddToClassList("md-cell");
                    cell.selection.isSelectable = true;
                    row.Add(cell);
                }
                table.Add(row);
            }
            return table;
        }

        VisualElement Rule()
        {
            var hr = new VisualElement();
            hr.AddToClassList("md-hr");
            return hr;
        }

        static VisualElement Spacer()
        {
            var s = new VisualElement();
            s.style.flexGrow = 1;
            return s;
        }

        // ----------------------------------------------------------- asset previews

        static readonly Regex TagRe = new Regex(@"\[\[(image|model|asset|ping)\s*:\s*([^\]]+?)\]\]", RegexOptions.IgnoreCase);
        static readonly Regex MdImgRe = new Regex(@"!\[[^\]]*\]\(\s*([^)\s]+)\s*\)");
        static readonly Regex PathRe = new Regex(@"[A-Za-z0-9_./\\:\-]+\.(png|jpg|jpeg|tga|psd|prefab|fbx|obj|asset|mat|controller|anim)", RegexOptions.IgnoreCase);
        static readonly string[] ImageExts = { ".png", ".jpg", ".jpeg", ".tga", ".psd" };

        struct AssetRef { public string Kind; public string Path; }

        VisualElement BuildParagraph(string text)
        {
            var refs = ExtractAssetRefs(ref text);
            if (refs.Count == 0) return RichLabel(text, "md-p");

            var container = new VisualElement();
            if (!string.IsNullOrWhiteSpace(text)) container.Add(RichLabel(text, "md-p"));
            foreach (var r in refs)
            {
                var card = BuildAssetCard(r.Path, r.Kind);
                if (card != null) container.Add(card);
            }
            return container;
        }

        static List<AssetRef> ExtractAssetRefs(ref string text)
        {
            var found = new List<AssetRef>();
            var seen = new HashSet<string>();
            void Add(string kind, string p)
            {
                p = p.Trim().Trim('`', '"', '\'', ',', '.', ')', '(');
                if (p.Length == 0) return;
                if (seen.Add(kind + "|" + p)) found.Add(new AssetRef { Kind = kind, Path = p });
            }
            text = TagRe.Replace(text, m => { Add(m.Groups[1].Value.ToLowerInvariant(), m.Groups[2].Value); return ""; });
            text = MdImgRe.Replace(text, m => { Add("image", m.Groups[1].Value); return ""; });
            foreach (Match m in PathRe.Matches(text))
            {
                string ext = "." + m.Groups[1].Value.ToLowerInvariant();
                Add(System.Array.IndexOf(ImageExts, ext) >= 0 ? "image" : "model", m.Value);
            }
            text = text.Trim();
            return found;
        }

        public VisualElement BuildAssetCard(string rawPath, string kind)
        {
            var obj = ResolveAsset(rawPath, out string assetPath, out string absPath);
            bool resolved = obj != null || (!string.IsNullOrEmpty(absPath) && File.Exists(absPath));
            if (!resolved) return null; // unresolved ref stays as plain text

            var card = new VisualElement(); card.AddToClassList("asset-card");

            if (kind != "ping")
            {
                var thumb = new Image { scaleMode = ScaleMode.ScaleToFit };
                thumb.AddToClassList("asset-thumb");
                Texture2D direct = null;
                if (kind == "image")
                {
                    if (obj is Texture2D t2) direct = t2;
                    else if (!string.IsNullOrEmpty(absPath) && File.Exists(absPath)) direct = LoadDiskImage(absPath);
                }
                if (direct != null) thumb.image = direct;
                else if (obj != null) { thumb.image = AssetPreview.GetMiniThumbnail(obj); PollPreview(thumb, obj); }
                card.Add(thumb);
            }

            var meta = new VisualElement(); meta.AddToClassList("asset-meta");
            var name = new Label(Path.GetFileName(rawPath)); name.AddToClassList("asset-name");
            var sub = new Label(kind + " · " + (assetPath ?? rawPath)); sub.AddToClassList("asset-sub");
            meta.Add(name); meta.Add(sub); card.Add(meta);
            card.Add(Spacer());

            string aPath = absPath; var capObj = obj;
            var ping = new Button(() =>
            {
                if (capObj != null) { EditorGUIUtility.PingObject(capObj); Selection.activeObject = capObj; }
                else if (!string.IsNullOrEmpty(aPath) && File.Exists(aPath)) EditorUtility.RevealInFinder(aPath);
            }) { text = "Ping" };
            ping.AddToClassList("asset-ping");
            card.Add(ping);
            return card;
        }

        UnityEngine.Object ResolveAsset(string raw, out string assetPath, out string absPath)
        {
            assetPath = null; absPath = null;
            if (string.IsNullOrEmpty(raw)) return null;
            string p = raw.Replace('\\', '/').Trim();
            string dataPath = Application.dataPath.Replace('\\', '/');                              // <proj>/Assets
            string projRoot = dataPath.EndsWith("/Assets") ? dataPath.Substring(0, dataPath.Length - 7) : dataPath;

            if (p.StartsWith("Assets/")) { assetPath = p; absPath = projRoot + "/" + p; }
            else if (Path.IsPathRooted(p)) { absPath = p; }
            else
            {
                string baseDir = string.IsNullOrEmpty(WorkingDir) ? projRoot : WorkingDir.Replace('\\', '/').TrimEnd('/');
                absPath = baseDir + "/" + p;
            }
            if (assetPath == null && absPath != null && absPath.Replace('\\', '/').StartsWith(dataPath + "/"))
                assetPath = "Assets/" + absPath.Replace('\\', '/').Substring(dataPath.Length + 1);

            return string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
        }

        static void PollPreview(Image img, UnityEngine.Object obj)
        {
            int id = obj.GetInstanceID();
            int tries = 0;
            IVisualElementScheduledItem item = null;
            item = img.schedule.Execute(() =>
            {
                tries++;
                var t = AssetPreview.GetAssetPreview(obj);
                if (t != null) { img.image = t; item.Pause(); }
                else if (tries > 30 || !AssetPreview.IsLoadingAssetPreview(id)) item.Pause();
            }).Every(100);
        }

        static Texture2D LoadDiskImage(string absPath)
        {
            try
            {
                var tex = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                if (tex.LoadImage(File.ReadAllBytes(absPath))) return tex;
                Object.DestroyImmediate(tex);
            }
            catch { /* ignore */ }
            return null;
        }

        // ----------------------------------------------------------- inline

        string Inline(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var codes = new List<string>();
            var links = new List<string>();

            // Protect inline code + link labels from the emphasis passes with the PUA sentinels.
            s = Regex.Replace(s, "`([^`]+)`", m => { codes.Add(m.Groups[1].Value); return SCode + (codes.Count - 1) + SEnd; });
            s = Regex.Replace(s, @"\[([^\]]+)\]\(([^)]+)\)", m => { links.Add(m.Groups[1].Value); return SLink + (links.Count - 1) + SEnd; });

            s = Regex.Replace(s, @"\*\*([^*]+?)\*\*", "<b>$1</b>");
            s = Regex.Replace(s, "__([^_]+?)__", "<b>$1</b>");
            s = Regex.Replace(s, @"~~([^~]+?)~~", "$1");
            s = Regex.Replace(s, @"(?<![*\w])\*([^*\n]+?)\*(?![*\w])", "<i>$1</i>");

            s = Regex.Replace(s, Regex.Escape(SCode) + @"(\d+)" + Regex.Escape(SEnd), m =>
            {
                int k = int.Parse(m.Groups[1].Value);
                return k >= 0 && k < codes.Count ? "<color=" + _codeHex + ">" + Escape(codes[k]) + "</color>" : "";
            });
            s = Regex.Replace(s, Regex.Escape(SLink) + @"(\d+)" + Regex.Escape(SEnd), m =>
            {
                int k = int.Parse(m.Groups[1].Value);
                return k >= 0 && k < links.Count ? "<color=" + _linkHex + ">" + Escape(links[k]) + "</color>" : "";
            });
            return s;
        }

        // UI Toolkit rich text parses '<'; neutralize stray angle brackets in restored spans.
        static string Escape(string s) => s.Replace("<", "‹").Replace(">", "›");

        // ----------------------------------------------------------- parser (UI-agnostic)

        static readonly Regex RuleRe = new Regex(@"^(-{3,}|\*{3,}|_{3,})$");
        static readonly Regex HeadRe = new Regex(@"^(#{1,6})\s+(.*)$");
        static readonly Regex BulletRe = new Regex(@"^(\s*)[-*+]\s+(.*)$");
        static readonly Regex OrderedRe = new Regex(@"^(\s*)(\d+)\.\s+(.*)$");

        List<Block> ParseBlocks(string md)
        {
            var blocks = new List<Block>();
            var lines = md.Replace("\r", "").Split('\n');
            int i = 0;

            while (i < lines.Length)
            {
                string raw = lines[i];
                string t = raw.TrimStart();

                if (t.StartsWith("```"))
                {
                    string lang = t.Length > 3 ? t.Substring(3).Trim() : "";
                    var sb = new StringBuilder();
                    i++;
                    while (i < lines.Length && !lines[i].TrimStart().StartsWith("```")) { sb.Append(lines[i]).Append('\n'); i++; }
                    if (i < lines.Length) i++;
                    blocks.Add(new Block { Type = BType.Code, Text = sb.ToString().TrimEnd('\n'), Marker = lang });
                    continue;
                }
                if (t.Length == 0) { i++; continue; }
                if (RuleRe.IsMatch(t)) { blocks.Add(new Block { Type = BType.Rule }); i++; continue; }

                if (t.Contains("|") && i + 1 < lines.Length && IsTableSep(lines[i + 1]))
                {
                    var rows = new List<string[]> { SplitRow(t) };
                    i += 2;
                    while (i < lines.Length && lines[i].Contains("|") && lines[i].Trim().Length > 0)
                    { rows.Add(SplitRow(lines[i])); i++; }
                    blocks.Add(new Block { Type = BType.Table, Rows = rows });
                    continue;
                }

                var hm = HeadRe.Match(t);
                if (hm.Success)
                {
                    int lvl = hm.Groups[1].Value.Length;
                    blocks.Add(new Block { Type = lvl == 1 ? BType.H1 : lvl == 2 ? BType.H2 : BType.H3, Text = hm.Groups[2].Value });
                    i++; continue;
                }

                if (t.StartsWith(">"))
                {
                    var sb = new StringBuilder();
                    while (i < lines.Length && lines[i].TrimStart().StartsWith(">"))
                    { sb.Append(lines[i].TrimStart().TrimStart('>').TrimStart()).Append(' '); i++; }
                    blocks.Add(new Block { Type = BType.Quote, Text = sb.ToString().Trim() });
                    continue;
                }

                var bm = BulletRe.Match(raw);
                if (bm.Success) { blocks.Add(new Block { Type = BType.Bullet, Indent = bm.Groups[1].Value.Length, Text = bm.Groups[2].Value }); i++; continue; }

                var om = OrderedRe.Match(raw);
                if (om.Success) { blocks.Add(new Block { Type = BType.Ordered, Indent = om.Groups[1].Value.Length, Marker = om.Groups[2].Value, Text = om.Groups[3].Value }); i++; continue; }

                var pbuf = new StringBuilder();
                while (i < lines.Length)
                {
                    string pl = lines[i];
                    string pt = pl.TrimStart();
                    if (pt.Length == 0) break;
                    if (pt.StartsWith("```") || HeadRe.IsMatch(pt) || pt.StartsWith(">") ||
                        BulletRe.IsMatch(pl) || OrderedRe.IsMatch(pl) || RuleRe.IsMatch(pt)) break;
                    if (pt.Contains("|") && i + 1 < lines.Length && IsTableSep(lines[i + 1])) break;
                    if (pbuf.Length > 0) pbuf.Append('\n');
                    pbuf.Append(pl.Trim());
                    i++;
                }
                if (pbuf.Length > 0) blocks.Add(new Block { Type = BType.Para, Text = pbuf.ToString() });
                else i++; // guarantee forward progress
            }
            return blocks;
        }

        static bool IsTableSep(string line)
        {
            string s = line.Trim();
            return s.Contains("|") && s.Contains("-") &&
                   s.Replace("|", "").Replace(":", "").Replace("-", "").Trim().Length == 0;
        }

        static string[] SplitRow(string line)
        {
            line = line.Trim();
            if (line.StartsWith("|")) line = line.Substring(1);
            if (line.EndsWith("|")) line = line.Substring(0, line.Length - 1);
            var cells = line.Split('|');
            for (int k = 0; k < cells.Length; k++) cells[k] = cells[k].Trim();
            return cells;
        }
    }
}
