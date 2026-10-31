// Stackshot - Markdown as a standalone HTML page (for Exportar HTML and Copiar como HTML): everything escaped, only safe
// links, the styles inline.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Text;

namespace Stackshot
{
    public static class MdHtml
    {
        const string Css =
            "body{margin:0;background:#fff;color:#1d1d1f;font:16px/1.6 'Segoe UI Variable Text','Segoe UI',system-ui,sans-serif}" +
            "article{max-width:760px;margin:0 auto;padding:32px 24px 48px}" +
            "h1,h2,h3,h4,h5,h6{font-family:'Segoe UI Variable Display','Segoe UI',system-ui,sans-serif;font-weight:600;line-height:1.3;margin:1.6em 0 .55em}" +
            "h1{font-size:1.9em}h2{font-size:1.5em}h3{font-size:1.25em}h4{font-size:1.1em}h5{font-size:1em}h6{font-size:.95em;color:#6e6e73}" +
            "h1,h2{padding-bottom:.3em;border-bottom:1px solid #e5e5ea}article>:first-child{margin-top:0}" +
            "p,ul,ol,blockquote,pre,table{margin:0 0 1em}" +
            "a{color:#0071e3;text-decoration:none}a:hover{text-decoration:underline}" +
            "code{font:.88em 'Cascadia Mono',Consolas,monospace;background:rgba(0,0,0,.06);padding:.1em .3em;border-radius:4px}" +
            "pre{background:rgba(0,0,0,.05);border:1px solid #e5e5ea;border-radius:8px;padding:12px 14px;overflow:auto}" +
            "pre code{background:none;padding:0;font-size:.85em}" +
            "blockquote{margin-left:0;padding:0 0 0 14px;border-left:3px solid rgba(0,113,227,.7);color:#424245}" +
            "table{border-collapse:collapse}th,td{border:1px solid #e5e5ea;padding:7px 11px}th{background:rgba(0,0,0,.04);font-weight:600}" +
            "hr{border:0;border-top:1px solid #e5e5ea;margin:1.5em 0}li.task{list-style:none;margin-left:-1.3em}li.task input{margin-right:.45em}" +
            "li.done{color:#86868b}img{max-width:100%;height:auto}";

        public static string Page(string markdown, string title)
        {
            return Page(markdown, title, null);
        }

        // image: the src for an image's link (null keeps it as text); see Body.
        public static string Page(string markdown, string title, Func<string, string> image)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(I18n.IsSpanish ? "<!DOCTYPE html>\n<html lang=\"es\">\n" : "<!DOCTYPE html>\n<html lang=\"en\">\n").Append("<head>\n<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
            sb.Append("<title>").Append(Esc(title ?? I18n.T("Documento", "Document"))).Append("</title>\n<style>").Append(Css).Append("</style>\n</head>\n<body>\n<article>\n");
            sb.Append(Body(markdown, image));
            sb.Append("</article>\n</body>\n</html>\n");
            return sb.ToString();
        }

        // The blocks alone (what goes into the clipboard as a fragment).
        public static string Body(string markdown)
        {
            return Body(markdown, null);
        }

        // image: the src for an image's link, or null to leave it as text; without one, Source decides.
        public static string Body(string markdown, Func<string, string> image)
        {
            StringBuilder sb = new StringBuilder();
            Dictionary<string, int> slugs = new Dictionary<string, int>();
            Blocks(sb, MarkdownDoc.Parse(markdown), slugs, image ?? Source);
            return sb.ToString();
        }

        // An image's src as written when it is safe to keep: a relative path, a web address or a data image. A Windows
        // path becomes a file URI; scripts and other schemes are not exported.
        public static string Source(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            string u = url.Trim();
            if (u.Length >= 3 && char.IsLetter(u[0]) && u[1] == ':' && (u[2] == '\\' || u[2] == '/'))
            {
                try { return new Uri(u.Replace('/', '\\')).AbsoluteUri; } catch (UriFormatException) { return null; }
            }
            if (u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || u.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                u.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)) return u;
            int colon = u.IndexOf(':'), stop = u.IndexOfAny(new char[] { '/', '?', '#' });
            return colon < 0 || (stop >= 0 && stop < colon) ? u : null;
        }

        static void Blocks(StringBuilder sb, List<MdBlock> blocks, Dictionary<string, int> slugs, Func<string, string> image)
        {
            foreach (MdBlock b in blocks)
            {
                switch (b.Kind)
                {
                    case MdKind.Heading:
                    {
                        int lv = Math.Max(1, Math.Min(6, b.Level));
                        string slug = MarkdownTools.Slug(b.Text);
                        int n;
                        if (slugs.TryGetValue(slug, out n)) { slugs[slug] = n + 1; slug += "-" + n; } else slugs[slug] = 1;
                        sb.Append("<h").Append(lv).Append(" id=\"").Append(Esc(slug)).Append("\">");
                        Inline(sb, b.Text, image);
                        sb.Append("</h").Append(lv).Append(">\n");
                        break;
                    }
                    case MdKind.Paragraph:
                        sb.Append("<p>");
                        Inline(sb, b.Text, image);
                        sb.Append("</p>\n");
                        break;
                    case MdKind.Code:
                        sb.Append("<pre><code").Append(string.IsNullOrEmpty(b.Lang) ? "" : " class=\"language-" + Esc(b.Lang) + "\"").Append('>');
                        sb.Append(Esc(b.Text)).Append("</code></pre>\n");
                        break;
                    case MdKind.Quote:
                        sb.Append("<blockquote>\n");
                        Blocks(sb, b.Children, slugs, image);
                        sb.Append("</blockquote>\n");
                        break;
                    case MdKind.List:
                    {
                        string tag = b.Ordered ? "ol" : "ul";
                        sb.Append('<').Append(tag);
                        if (b.Ordered && b.Level > 1) sb.Append(" start=\"").Append(b.Level).Append('"');
                        sb.Append(">\n");
                        foreach (MdItem it in b.Items)
                        {
                            if (it.Checked.HasValue)
                                sb.Append("<li class=\"task").Append(it.Checked.Value ? " done" : "").Append("\"><input type=\"checkbox\" disabled").Append(it.Checked.Value ? " checked" : "").Append('>');
                            else sb.Append("<li>");
                            // A single paragraph stays inline, as in a tight list.
                            if (it.Blocks.Count == 1 && it.Blocks[0].Kind == MdKind.Paragraph) Inline(sb, it.Blocks[0].Text, image);
                            else { sb.Append('\n'); Blocks(sb, it.Blocks, slugs, image); }
                            sb.Append("</li>\n");
                        }
                        sb.Append("</").Append(tag).Append(">\n");
                        break;
                    }
                    case MdKind.Table:
                    {
                        sb.Append("<table>\n<thead><tr>");
                        for (int c = 0; c < b.Head.Count; c++) { sb.Append("<th").Append(Align(b, c)).Append('>'); Inline(sb, b.Head[c], image); sb.Append("</th>"); }
                        sb.Append("</tr></thead>\n<tbody>\n");
                        foreach (List<string> r in b.Rows)
                        {
                            sb.Append("<tr>");
                            for (int c = 0; c < r.Count; c++) { sb.Append("<td").Append(Align(b, c)).Append('>'); Inline(sb, r[c], image); sb.Append("</td>"); }
                            sb.Append("</tr>\n");
                        }
                        sb.Append("</tbody>\n</table>\n");
                        break;
                    }
                    default:
                        sb.Append("<hr>\n");
                        break;
                }
            }
        }

        static string Align(MdBlock b, int c)
        {
            int a = c < b.Align.Count ? b.Align[c] : 0;
            return a == 1 ? " style=\"text-align:center\"" : a == 2 ? " style=\"text-align:right\"" : "";
        }

        static void Inline(StringBuilder sb, string src, Func<string, string> image)
        {
            foreach (MdSpan s in MarkdownDoc.Inline(src))
            {
                if (s.Break) { sb.Append("<br>"); continue; }
                string img = s.Image ? image(s.Href) : null;
                if (img != null)
                {
                    sb.Append("<img src=\"").Append(Esc(img)).Append("\" alt=\"").Append(Esc(s.Text.StartsWith("\u25A3 ", StringComparison.Ordinal) ? s.Text.Substring(2) : s.Text)).Append("\">");
                    continue;
                }
                bool link = s.Href != null && MarkdownView.IsSafe(s.Href);
                if (link) sb.Append("<a href=\"").Append(Esc(s.Href)).Append("\">");
                if (s.Bold) sb.Append("<strong>");
                if (s.Italic) sb.Append("<em>");
                if (s.Strike) sb.Append("<del>");
                if (s.Code) sb.Append("<code>");
                sb.Append(Esc(s.Text));
                if (s.Code) sb.Append("</code>");
                if (s.Strike) sb.Append("</del>");
                if (s.Italic) sb.Append("</em>");
                if (s.Bold) sb.Append("</strong>");
                if (link) sb.Append("</a>");
            }
        }

        public static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    case '\'': sb.Append("&#39;"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        // The clipboard's HTML format: a header with byte offsets (UTF-8) around the fragment.
        public static string Clipboard(string fragment)
        {
            const string head = "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
            string pre = "<html><head><meta charset=\"utf-8\"><style>" + Css + "</style></head><body><!--StartFragment-->";
            string post = "<!--EndFragment--></body></html>";
            Encoding u = new UTF8Encoding(false);
            int headLen = string.Format(head, 0, 0, 0, 0).Length;
            int startHtml = headLen, startFrag = startHtml + u.GetByteCount(pre), endFrag = startFrag + u.GetByteCount(fragment), endHtml = endFrag + u.GetByteCount(post);
            return string.Format(head, startHtml, endHtml, startFrag, endFrag) + pre + fragment + post;
        }
    }
}
