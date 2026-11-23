// Stackshot - Korean translations for the Markdown and XML viewers.
// MIT License - https://github.com/rubenitx/stackshot
using System.Collections.Generic;

namespace Stackshot
{
    public static partial class I18n
    {
        static void RegisterKoreanDocuments(Dictionary<string, string> map)
        {
            // Markdown/DocFile.cs
            map[" with BOM"] = " (BOM \uD3EC\uD568)";
            map["Cannot find the file: {0}"] = "\uD30C\uC77C\uC744 \uCC3E\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4: {0}";
            map["Only .md, .xml or .txt files are supported: {0}"] = ".md, .xml \uB610\uB294 .txt \uD30C\uC77C\uB9CC \uC9C0\uC6D0\uD569\uB2C8\uB2E4: {0}";
            map["The file is too big (maximum {0} MB): {1}"] = "\uD30C\uC77C\uC774 \uB108\uBB34 \uD07D\uB2C8\uB2E4(\uCD5C\uB300 {0} MB): {1}";
            map["Could not read the file: {0}"] = "\uD30C\uC77C\uC744 \uC77D\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4: {0}";
            map["Some characters don't fit in {0}."] = "{0}\uC5D0\uC11C \uD45C\uD604\uD560 \uC218 \uC5C6\uB294 \uBB38\uC790\uAC00 \uC788\uC2B5\uB2C8\uB2E4.";
            map["The file has bytes that are not valid {0}."] = "\uD30C\uC77C\uC5D0 {0} \uD615\uC2DD\uC5D0 \uB9DE\uC9C0 \uC54A\uB294 \uBC14\uC774\uD2B8\uAC00 \uC788\uC2B5\uB2C8\uB2E4.";
            map["Choose where to save it."] = "\uC800\uC7A5\uD560 \uC704\uCE58\uB97C \uC120\uD0DD\uD558\uC138\uC694.";
            map["This file is read-only. Use \u201CSave as\u2026\u201D."] = "\uC77D\uAE30 \uC804\uC6A9 \uD30C\uC77C\uC785\uB2C8\uB2E4. \u2018\uB2E4\uB978 \uC774\uB984\uC73C\uB85C \uC800\uC7A5\u2026\u2019\uC744 \uC120\uD0DD\uD558\uC138\uC694.";
            map["No permission to write to {0}. Use \u201CSave as\u2026\u201D."] = "{0}\uC5D0 \uC4F8 \uAD8C\uD55C\uC774 \uC5C6\uC2B5\uB2C8\uB2E4. \u2018\uB2E4\uB978 \uC774\uB984\uC73C\uB85C \uC800\uC7A5\u2026\u2019\uC744 \uC120\uD0DD\uD558\uC138\uC694.";
            map["Could not save {0}: {1}"] = "{0}\uC744(\uB97C) \uC800\uC7A5\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4: {1}";

            // Markdown/EditCommands.cs
            map["text"] = "\uD14D\uC2A4\uD2B8";
            map["Column "] = "\uC5F4 ";

            // Markdown/FindBar.cs
            map["Invalid expression"] = "\uC798\uBABB\uB41C \uD45C\uD604\uC2DD";
            map["Find"] = "\uCC3E\uAE30";
            map["Match case"] = "\uB300\uC18C\uBB38\uC790 \uAD6C\uBD84";
            map["Whole word"] = "\uB2E8\uC5B4 \uB2E8\uC704\uB85C";
            map["Regular expression"] = "\uC815\uADDC\uC2DD";
            map["Previous (Shift+F3)"] = "\uC774\uC804 (Shift+F3)";
            map["Next (F3)"] = "\uB2E4\uC74C (F3)";
            map["Close (Esc)"] = "\uB2EB\uAE30 (Esc)";
            map["Replace with"] = "\uBC14\uAFC0 \uB0B4\uC6A9";
            map["Replace"] = "\uBC14\uAFB8\uAE30";
            map["Replace all"] = "\uBAA8\uB450 \uBC14\uAFB8\uAE30";
            map["No results (search cut short)"] = "\uAC80\uC0C9 \uACB0\uACFC \uC5C6\uC74C (\uAC80\uC0C9\uC774 \uC911\uB2E8\uB428)";
            map["No results"] = "\uAC80\uC0C9 \uACB0\uACFC \uC5C6\uC74C";
            map[" of "] = " / ";
            map[" result"] = "\uAC74";
            map[" results"] = "\uAC74";

            // Markdown/MarkdownDoc.cs
            map["image"] = "\uC774\uBBF8\uC9C0";

            // Markdown/MarkdownEditor.cs
            map["Undo"] = "\uC2E4\uD589 \uCDE8\uC18C";
            map["Redo"] = "\uB2E4\uC2DC \uC2E4\uD589";
            map["Cut"] = "\uC798\uB77C\uB0B4\uAE30";
            map["Cut."] = "\uC798\uB77C\uB0C8\uC2B5\uB2C8\uB2E4.";
            map["Copy"] = "\uBCF5\uC0AC";
            map["Copied."] = "\uBCF5\uC0AC\uD588\uC2B5\uB2C8\uB2E4.";
            map["Paste"] = "\uBD99\uC5EC\uB123\uAE30";
            map["Select all"] = "\uBAA8\uB450 \uC120\uD0DD";

            // Markdown/MarkdownFlow.Images.cs
            map["Cannot show this image"] = "\uC774\uBBF8\uC9C0\uB97C \uD45C\uC2DC\uD560 \uC218 \uC5C6\uC74C";

            // Markdown/MarkdownTools.cs
            map["It's empty: there is nothing to show."] = "\uD45C\uC2DC\uD560 \uB0B4\uC6A9\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["It's too big (maximum 2 MB of text)."] = "\uD14D\uC2A4\uD2B8\uAC00 \uB108\uBB34 \uD07D\uB2C8\uB2E4(\uCD5C\uB300 2 MB).";
            map["This doesn't look like text: it has binary content."] = "\uD14D\uC2A4\uD2B8\uAC00 \uC544\uB2CC \uBC14\uC774\uB108\uB9AC \uB370\uC774\uD130\uAC00 \uD3EC\uD568\uB3FC \uC788\uC2B5\uB2C8\uB2E4.";
            map["Cannot open a remote file ({0}): {1}"] = "\uC6D0\uACA9 \uD30C\uC77C\uC744 \uC5F4 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4({0}): {1}";
            map["Cannot recognize this path: {0}"] = "\uACBD\uB85C\uB97C \uC778\uC2DD\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4: {0}";
            map["Cannot find WSL to open: {0}"] = "WSL\uC744 \uCC3E\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4: {0}";
            map["Cannot find the WSL home folder to open: {0}"] = "WSL \uD648 \uD3F4\uB354\uB97C \uCC3E\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4: {0}";
            map["There is no file."] = "\uD30C\uC77C\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["## Contents"] = "## \uBAA9\uCC28";

            // Markdown/MarkdownView.cs
            map["The document is too long for a single image."] = "\uBB38\uC11C\uAC00 \uB108\uBB34 \uAE38\uC5B4 \uD55C \uC7A5\uC758 \uC774\uBBF8\uC9C0\uB85C \uB9CC\uB4E4 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";

            // Markdown/MarkdownWindow.Access.cs
            map["Mode"] = "\uBAA8\uB4DC";
            map["XML text"] = "XML \uD14D\uC2A4\uD2B8";
            map["Markdown text"] = "\uB9C8\uD06C\uB2E4\uC6B4 \uD14D\uC2A4\uD2B8";
            map["Preview"] = "\uBBF8\uB9AC \uBCF4\uAE30";
            map["Search or filter"] = "\uAC80\uC0C9 \uB610\uB294 \uD544\uD130";
            map["Width of the text and the preview"] = "\uD14D\uC2A4\uD2B8\uC640 \uBBF8\uB9AC \uBCF4\uAE30 \uB108\uBE44";

            // Markdown/MarkdownWindow.cs
            map["No earlier XML in the folder."] = "\uD3F4\uB354\uC5D0 \uC774\uC804 XML\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["No later XML in the folder."] = "\uD3F4\uB354\uC5D0 \uB2E4\uC74C XML\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Markdown and XML"] = "\uB9C8\uD06C\uB2E4\uC6B4 \uBC0F XML";
            map["Save changes to \u201C{0}\u201D?"] = "\u2018{0}\u2019\uC758 \uBCC0\uACBD \uC0AC\uD56D\uC744 \uC800\uC7A5\uD560\uAE4C\uC694?";
            map["Untitled"] = "\uC81C\uBAA9 \uC5C6\uC74C";
            map["If you don't save them, they will be lost."] = "\uC800\uC7A5\uD558\uC9C0 \uC54A\uC73C\uBA74 \uBCC0\uACBD \uC0AC\uD56D\uC774 \uC0AC\uB77C\uC9D1\uB2C8\uB2E4.";
            map["Save"] = "\uC800\uC7A5";
            map["Don't save"] = "\uC800\uC7A5 \uC548 \uD568";
            map["Cancel"] = "\uCDE8\uC18C";
            map["View"] = "\uBCF4\uAE30";
            map["Edit"] = "\uD3B8\uC9D1";
            map["New"] = "\uC0C8\uB85C \uB9CC\uB4E4\uAE30";
            map["New (Ctrl+N)"] = "\uC0C8\uB85C \uB9CC\uB4E4\uAE30 (Ctrl+N)";
            map["More options"] = "\uCD94\uAC00 \uC635\uC158";
            map["Save (Ctrl+S)"] = "\uC800\uC7A5 (Ctrl+S)";
            map["Create the capture and close (Ctrl+Enter)"] = "\uCEA1\uCC98 \uC0DD\uC131 \uD6C4 \uB2EB\uAE30 (Ctrl+Enter)";
            map["Format"] = "\uC11C\uC2DD \uC815\uB9AC";
            map["Format (Ctrl+Shift+F)"] = "\uC11C\uC2DD \uC815\uB9AC (Ctrl+Shift+F)";
            map["Format options"] = "\uC11C\uC2DD \uC635\uC158";
            map["Compact"] = "\uC555\uCD95";
            map["Expand all"] = "\uBAA8\uB450 \uD3BC\uCE58\uAE30";
            map["Collapse all"] = "\uBAA8\uB450 \uC811\uAE30";
            map["Copy XML"] = "XML \uBCF5\uC0AC";
            map["Tidy"] = "\uBB38\uC11C \uC815\uB9AC";
            map["Insert contents"] = "\uBAA9\uCC28 \uC0BD\uC785";
            map["Copy Markdown"] = "\uB9C8\uD06C\uB2E4\uC6B4 \uBCF5\uC0AC";
            map["Copy as image"] = "\uC774\uBBF8\uC9C0\uB85C \uBCF5\uC0AC";
            map["Bold (Ctrl+B)"] = "\uAD75\uAC8C (Ctrl+B)";
            map["Italic (Ctrl+I)"] = "\uAE30\uC6B8\uC784\uAF34 (Ctrl+I)";
            map["Strikethrough (Ctrl+Shift+X)"] = "\uCDE8\uC18C\uC120 (Ctrl+Shift+X)";
            map["Heading (Ctrl+1 to Ctrl+6)"] = "\uC81C\uBAA9 (Ctrl+1~Ctrl+6)";
            map["List (Ctrl+Shift+8)"] = "\uAE00\uBA38\uB9AC \uAE30\uD638 \uBAA9\uB85D (Ctrl+Shift+8)";
            map["Numbered list (Ctrl+Shift+7)"] = "\uBC88\uD638 \uB9E4\uAE30\uAE30 \uBAA9\uB85D (Ctrl+Shift+7)";
            map["Task list"] = "\uD560 \uC77C \uBAA9\uB85D";
            map["Quote"] = "\uC778\uC6A9\uBB38";
            map["Link (Ctrl+K)"] = "\uB9C1\uD06C (Ctrl+K)";
            map["Code (Ctrl+Shift+C)"] = "\uCF54\uB4DC (Ctrl+Shift+C)";
            map["code"] = "\uCF54\uB4DC";
            map["Code block"] = "\uCF54\uB4DC \uBE14\uB85D";
            map["Table (inside one: rows and columns)"] = "\uD45C (\uD45C \uC548\uC5D0\uC11C\uB294 \uD589\uACFC \uC5F4 \uD3B8\uC9D1)";
            map["Divider"] = "\uAD6C\uBD84\uC120";
            map["Undo (Ctrl+Z)"] = "\uC2E4\uD589 \uCDE8\uC18C (Ctrl+Z)";
            map["Redo (Ctrl+Y)"] = "\uB2E4\uC2DC \uC2E4\uD589 (Ctrl+Y)";
            map["Insert row below"] = "\uC544\uB798\uC5D0 \uD589 \uC0BD\uC785";
            map["Insert column to the right"] = "\uC624\uB978\uCABD\uC5D0 \uC5F4 \uC0BD\uC785";
            map["Delete row"] = "\uD589 \uC0AD\uC81C";
            map["Delete column"] = "\uC5F4 \uC0AD\uC81C";
            map["Align table"] = "\uD45C \uC815\uB82C";
            map["The header row cannot be deleted."] = "\uBA38\uB9AC\uAE00 \uD589\uC740 \uC0AD\uC81C\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Open\u2026"] = "\uC5F4\uAE30\u2026";
            map["Save as\u2026"] = "\uB2E4\uB978 \uC774\uB984\uC73C\uB85C \uC800\uC7A5\u2026";
            map["Reload from disk"] = "\uB514\uC2A4\uD06C\uC5D0\uC11C \uB2E4\uC2DC \uBD88\uB7EC\uC624\uAE30";
            map["Show in folder"] = "\uD3F4\uB354\uC5D0\uC11C \uBCF4\uAE30";
            map["Copy path"] = "\uACBD\uB85C \uBCF5\uC0AC";
            map["Path copied."] = "\uACBD\uB85C\uB97C \uBCF5\uC0AC\uD588\uC2B5\uB2C8\uB2E4.";
            map["Copy as HTML"] = "HTML\uB85C \uBCF5\uC0AC";
            map["Export HTML\u2026"] = "HTML \uB0B4\uBCF4\uB0B4\uAE30\u2026";
            map["Previous XML in the folder   ["] = "\uD3F4\uB354\uC758 \uC774\uC804 XML   [";
            map["Next XML in the folder   ]"] = "\uD3F4\uB354\uC758 \uB2E4\uC74C XML   ]";
            map["Recent XML"] = "\uCD5C\uADFC XML";
            map["MMM d HH:mm"] = "M\uC6D4 d\uC77C HH:mm";
            map["HTML copied."] = "HTML\uC744 \uBCF5\uC0AC\uD588\uC2B5\uB2C8\uB2E4.";
            map["Could not use the clipboard."] = "\uD074\uB9BD\uBCF4\uB4DC\uB97C \uC0AC\uC6A9\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Could not copy the HTML."] = "HTML\uC744 \uBCF5\uC0AC\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Export HTML"] = "HTML \uB0B4\uBCF4\uB0B4\uAE30";
            map["Web page|*.html;*.htm|All files|*.*"] = "\uC6F9 \uD398\uC774\uC9C0|*.html;*.htm|\uBAA8\uB4E0 \uD30C\uC77C|*.*";
            map["Exported: {0}."] = "\uB0B4\uBCF4\uB0C8\uC2B5\uB2C8\uB2E4: {0}.";
            map["Indent"] = "\uB4E4\uC5EC\uC4F0\uAE30";
            map["2 spaces"] = "\uACF5\uBC31 2\uCE78";
            map["4 spaces"] = "\uACF5\uBC31 4\uCE78";
            map["Tab"] = "\uD0ED";
            map["Attributes on new lines"] = "\uD2B9\uC131\uC744 \uC0C8 \uC904\uC5D0 \uD45C\uC2DC";
            map["Paste or drag in a .md, .xml or text"] = ".md, .xml \uD30C\uC77C\uC774\uB098 \uD14D\uC2A4\uD2B8\uB97C \uBD99\uC5EC\uB123\uAC70\uB098 \uB04C\uC5B4 \uB193\uC73C\uC138\uC694";
            map["Ctrl+V pastes from the clipboard. Accepts .md and .txt up to 2 MB, and .xml up to 8 MB."] = "Ctrl+V\uB85C \uD074\uB9BD\uBCF4\uB4DC\uC5D0\uC11C \uBD99\uC5EC\uB123\uC73C\uC138\uC694. .md\uC640 .txt\uB294 \uCD5C\uB300 2 MB, .xml\uC740 \uCD5C\uB300 8 MB\uAE4C\uC9C0 \uC9C0\uC6D0\uD569\uB2C8\uB2E4.";
            map["Open file\u2026"] = "\uD30C\uC77C \uC5F4\uAE30\u2026";
            map["Open (Ctrl+O)"] = "\uC5F4\uAE30 (Ctrl+O)";
            map["Blank document"] = "\uBE48 \uBB38\uC11C";
            map["Could not read the XML: {0}"] = "XML\uC744 \uC77D\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4: {0}";
            map["Reading the XML\u2026"] = "XML \uC77D\uB294 \uC911\u2026";
            map["The XML is not well-formed"] = "\uC62C\uBC14\uB978 \uD615\uC2DD\uC758 XML\uC774 \uC544\uB2D9\uB2C8\uB2E4";
            map["Fix it in the editor: this view updates by itself."] = "\uD3B8\uC9D1\uAE30\uC5D0\uC11C \uC218\uC815\uD558\uBA74 \uC774 \uBCF4\uAE30\uC5D0 \uC790\uB3D9\uC73C\uB85C \uBC18\uC601\uB429\uB2C8\uB2E4.";
            map["Switch to Edit to fix it."] = "\uD3B8\uC9D1 \uBAA8\uB4DC\uB85C \uC804\uD658\uD574 \uC218\uC815\uD558\uC138\uC694.";
            map["Go to error"] = "\uC624\uB958 \uC704\uCE58\uB85C \uC774\uB3D9";
            map["View, edit and turn Markdown or XML into a capture"] = "\uB9C8\uD06C\uB2E4\uC6B4 \uB610\uB294 XML \uBB38\uC11C\uB97C \uBCF4\uACE0 \uD3B8\uC9D1\uD558\uAC70\uB098 \uCEA1\uCC98\uB85C \uB9CC\uB4DC\uC138\uC694";
            map["unsaved"] = "\uC800\uC7A5 \uC548 \uB428";
            map["saved"] = "\uC800\uC7A5\uB428";
            map["no changes"] = "\uBCC0\uACBD \uC5C6\uC74C";
            map["  \u00B7  reading\u2026"] = "  \u00B7  \uC77D\uB294 \uC911\u2026";
            map["XML with errors"] = "\uC624\uB958\uAC00 \uC788\uB294 XML";
            map[" match"] = "\uAC74 \uC77C\uCE58";
            map[" matches"] = "\uAC74 \uC77C\uCE58";
            map["  \u00B7  well-formed"] = "  \u00B7  \uC62C\uBC14\uB978 \uD615\uC2DD";
            map[" node"] = "\uAC1C \uB178\uB4DC";
            map[" nodes"] = "\uAC1C \uB178\uB4DC";
            map[" (the first ones)"] = " (\uC55E\uBD80\uBD84\uB9CC)";
            map[" word"] = "\uB2E8\uC5B4";
            map[" words"] = "\uB2E8\uC5B4";
            map["Untitled \u2014 "] = "\uC81C\uBAA9 \uC5C6\uC74C \u2014 ";
            map["Some bytes are not valid {0}: they show as \u201C\uFFFD\u201D."] = "{0} \uD615\uC2DD\uC5D0 \uB9DE\uC9C0 \uC54A\uB294 \uBC14\uC774\uD2B8\uB294 \u2018\uFFFD\u2019\uB85C \uD45C\uC2DC\uD569\uB2C8\uB2E4.";
            map["Loading {0}\u2026"] = "{0} \uBD88\uB7EC\uC624\uB294 \uC911\u2026";
            map["Could not read the file."] = "\uD30C\uC77C\uC744 \uC77D\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Open a Markdown or XML document"] = "\uB9C8\uD06C\uB2E4\uC6B4 \uB610\uB294 XML \uBB38\uC11C \uC5F4\uAE30";
            map["Markdown, XML and text|*.md;*.markdown;*.mdown;*.mkd;*.xml;*.xsd;*.config;*.svg;*.xaml;*.txt|All files|*.*"] = "\uB9C8\uD06C\uB2E4\uC6B4, XML \uBC0F \uD14D\uC2A4\uD2B8|*.md;*.markdown;*.mdown;*.mkd;*.xml;*.xsd;*.config;*.svg;*.xaml;*.txt|\uBAA8\uB4E0 \uD30C\uC77C|*.*";
            map["Could not open file picker."] = "\uD30C\uC77C \uC120\uD0DD \uCC3D\uC744 \uC5F4 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["There is no text or .md/.xml file on the clipboard."] = "\uD074\uB9BD\uBCF4\uB4DC\uC5D0 \uD14D\uC2A4\uD2B8\uB098 .md/.xml \uD30C\uC77C\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Could not read clipboard."] = "\uD074\uB9BD\uBCF4\uB4DC\uB97C \uC77D\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["\u201C{0}\u201D has changed outside Stackshot"] = "Stackshot \uC678\uBD80\uC5D0\uC11C \u2018{0}\u2019\uC774(\uAC00) \uBCC0\uACBD\uB418\uC5C8\uC2B5\uB2C8\uB2E4";
            map["If you save, your version replaces the one on disk."] = "\uC800\uC7A5\uD558\uBA74 \uB514\uC2A4\uD06C\uC758 \uD30C\uC77C\uC744 \uD604\uC7AC \uB0B4\uC6A9\uC73C\uB85C \uB36E\uC5B4\uC501\uB2C8\uB2E4.";
            map["Overwrite"] = "\uB36E\uC5B4\uC4F0\uAE30";
            map["When you save, those bytes are replaced by \u201C\uFFFD\u201D"] = "\uC800\uC7A5\uD558\uBA74 \uD574\uB2F9 \uBC14\uC774\uD2B8\uAC00 \u2018\uFFFD\u2019\uB85C \uBC14\uB00C\uACE0";
            map[" and the original is lost. To keep it, use \u201CSave as\u2026\u201D."] = " \uC6D0\uBCF8 \uB370\uC774\uD130\uAC00 \uC0AC\uB77C\uC9D1\uB2C8\uB2E4. \uBCF4\uC874\uD558\uB824\uBA74 \u2018\uB2E4\uB978 \uC774\uB984\uC73C\uB85C \uC800\uC7A5\u2026\u2019\uC744 \uC120\uD0DD\uD558\uC138\uC694.";
            map["Save anyway"] = "\uADF8\uB798\uB3C4 \uC800\uC7A5";
            map["I can save it in UTF-8, which supports any character."] = "\uBAA8\uB4E0 \uBB38\uC790\uB97C \uC9C0\uC6D0\uD558\uB294 UTF-8\uB85C \uC800\uC7A5\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            map["Save in UTF-8"] = "UTF-8\uB85C \uC800\uC7A5";
            map["Could not save."] = "\uC800\uC7A5\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Saved at {0}."] = "{0}\uC5D0 \uC800\uC7A5\uD588\uC2B5\uB2C8\uB2E4.";
            map["Save as"] = "\uB2E4\uB978 \uC774\uB984\uC73C\uB85C \uC800\uC7A5";
            map["XML|*.xml|All files|*.*"] = "XML|*.xml|\uBAA8\uB4E0 \uD30C\uC77C|*.*";
            map["Markdown|*.md|Text|*.txt|All files|*.*"] = "\uB9C8\uD06C\uB2E4\uC6B4|*.md|\uD14D\uC2A4\uD2B8|*.txt|\uBAA8\uB4E0 \uD30C\uC77C|*.*";
            map["Could not open save dialog."] = "\uC800\uC7A5 \uB300\uD654 \uC0C1\uC790\uB97C \uC5F4 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Could not open the folder."] = "\uD3F4\uB354\uB97C \uC5F4 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Reload \u201C{0}\u201D from disk?"] = "\uB514\uC2A4\uD06C\uC5D0\uC11C \u2018{0}\u2019\uC744(\uB97C) \uB2E4\uC2DC \uBD88\uB7EC\uC62C\uAE4C\uC694?";
            map["Unsaved changes will be lost."] = "\uC800\uC7A5\uD558\uC9C0 \uC54A\uC740 \uBCC0\uACBD \uC0AC\uD56D\uC774 \uC0AC\uB77C\uC9D1\uB2C8\uB2E4.";
            map["Reload"] = "\uB2E4\uC2DC \uBD88\uB7EC\uC624\uAE30";
            map["Could not read file."] = "\uD30C\uC77C\uC744 \uC77D\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Reloaded from disk."] = "\uB514\uC2A4\uD06C\uC5D0\uC11C \uB2E4\uC2DC \uBD88\uB7EC\uC654\uC2B5\uB2C8\uB2E4.";
            map["Reloaded: file changed outside Stackshot."] = "\uC678\uBD80\uC5D0\uC11C \uD30C\uC77C\uC774 \uBCC0\uACBD\uB418\uC5B4 \uB2E4\uC2DC \uBD88\uB7EC\uC654\uC2B5\uB2C8\uB2E4.";
            map["The file changed on disk and you have unsaved changes here."] = "\uB514\uC2A4\uD06C\uC758 \uD30C\uC77C\uC774 \uBCC0\uACBD\uB418\uC5C8\uACE0, \uD604\uC7AC \uBB38\uC11C\uC5D0\uB3C4 \uC800\uC7A5\uD558\uC9C0 \uC54A\uC740 \uBCC0\uACBD \uC0AC\uD56D\uC774 \uC788\uC2B5\uB2C8\uB2E4.";
            map["The file no longer exists on disk. Save it so you don't lose the text."] = "\uB514\uC2A4\uD06C\uC5D0\uC11C \uD30C\uC77C\uC774 \uC0AC\uB77C\uC84C\uC2B5\uB2C8\uB2E4. \uD14D\uC2A4\uD2B8\uB97C \uBCF4\uAD00\uD558\uB824\uBA74 \uC800\uC7A5\uD558\uC138\uC694.";
            map["Keep my changes"] = "\uB0B4 \uBCC0\uACBD \uC0AC\uD56D \uC720\uC9C0";
            map["The expression takes too long: nothing was replaced."] = "\uC815\uADDC\uC2DD \uCC98\uB9AC \uC2DC\uAC04\uC774 \uCD08\uACFC\uB418\uC5B4 \uBC14\uAFB8\uAE30\uB97C \uC911\uB2E8\uD588\uC2B5\uB2C8\uB2E4.";
            map["Nothing to replace."] = "\uBC14\uAFC0 \uD56D\uBAA9\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["1 replacement."] = "1\uACF3\uC744 \uBC14\uAFE8\uC2B5\uB2C8\uB2E4.";
            map["{0} replacements."] = "{0}\uACF3\uC744 \uBC14\uAFE8\uC2B5\uB2C8\uB2E4.";
            map["Press Esc again to close."] = "\uB2EB\uC73C\uB824\uBA74 Esc\uB97C \uD55C \uBC88 \uB354 \uB204\uB974\uC138\uC694.";
            map["No text or .md/.xml file found in what you dropped."] = "\uB193\uC740 \uD56D\uBAA9\uC5D0\uC11C \uD14D\uC2A4\uD2B8\uB098 .md/.xml \uD30C\uC77C\uC744 \uCC3E\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Could not read what you dropped."] = "\uB193\uC740 \uD56D\uBAA9\uC744 \uC77D\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Opening the editor\u2026"] = "\uD3B8\uC9D1\uAE30\uB97C \uC5EC\uB294 \uC911\u2026";
            map["Already tidy."] = "\uC774\uBBF8 \uC815\uB9AC\uB41C \uBB38\uC11C\uC785\uB2C8\uB2E4.";
            map["Already"] = "\uC774\uBBF8 \uC801\uC6A9\uB428";
            map["Document tidied."] = "\uBB38\uC11C\uB97C \uC815\uB9AC\uD588\uC2B5\uB2C8\uB2E4.";
            map["Tidied"] = "\uC815\uB9AC\uB428";
            map["Not enough headings for a table of contents."] = "\uBAA9\uCC28\uB97C \uB9CC\uB4E4 \uC81C\uBAA9\uC774 \uBD80\uC871\uD569\uB2C8\uB2E4.";
            map["Table of contents inserted."] = "\uBAA9\uCC28\uB97C \uC0BD\uC785\uD588\uC2B5\uB2C8\uB2E4.";
            map["Inserted"] = "\uC0BD\uC785\uB428";
            map["Already formatting\u2026"] = "\uC774\uBBF8 \uC11C\uC2DD\uC744 \uC815\uB9AC\uD558\uACE0 \uC788\uC2B5\uB2C8\uB2E4\u2026";
            map["Formatting\u2026"] = "\uC11C\uC2DD \uC815\uB9AC \uC911\u2026";
            map["Compacting\u2026"] = "\uC555\uCD95 \uC911\u2026";
            map["The text changed while formatting: press {0} again."] = "\uC11C\uC2DD\uC744 \uC815\uB9AC\uD558\uB294 \uB3D9\uC548 \uD14D\uC2A4\uD2B8\uAC00 \uBCC0\uACBD\uB418\uC5C8\uC2B5\uB2C8\uB2E4. {0}\uC744(\uB97C) \uB2E4\uC2DC \uB204\uB974\uC138\uC694.";
            map["Already formatted."] = "\uC774\uBBF8 \uC11C\uC2DD\uC774 \uC815\uB9AC\uB418\uC5B4 \uC788\uC2B5\uB2C8\uB2E4.";
            map["Already compact."] = "\uC774\uBBF8 \uC555\uCD95\uB418\uC5B4 \uC788\uC2B5\uB2C8\uB2E4.";
            map["XML formatted."] = "XML \uC11C\uC2DD\uC744 \uC815\uB9AC\uD588\uC2B5\uB2C8\uB2E4.";
            map["XML compacted."] = "XML\uC744 \uC555\uCD95\uD588\uC2B5\uB2C8\uB2E4.";
            map["One moment: still reading the XML."] = "\uC7A0\uC2DC\uB9CC \uAE30\uB2E4\uB824 \uC8FC\uC138\uC694. XML\uC744 \uC77D\uACE0 \uC788\uC2B5\uB2C8\uB2E4.";
            map["Fix the XML before creating the image."] = "\uC774\uBBF8\uC9C0\uB97C \uB9CC\uB4E4\uAE30 \uC804\uC5D0 XML \uC624\uB958\uB97C \uC218\uC815\uD558\uC138\uC694.";
            map["Switch to View to create the XML image."] = "XML \uC774\uBBF8\uC9C0\uB97C \uB9CC\uB4E4\uB824\uBA74 \uBCF4\uAE30 \uBAA8\uB4DC\uB85C \uC804\uD658\uD558\uC138\uC694.";
            map["Too long for an image"] = "\uC774\uBBF8\uC9C0\uB85C \uB9CC\uB4E4\uAE30\uC5D0 \uB108\uBB34 \uAE41\uB2C8\uB2E4";
            map["As an image it would be over 16,000 pixels high and hard to view properly. In PDF it stays whole, split across pages."] = "\uC774\uBBF8\uC9C0 \uB192\uC774\uAC00 16,000\uD53D\uC140\uC744 \uB118\uC5B4 \uC77D\uAE30 \uC5B4\uB835\uC2B5\uB2C8\uB2E4. PDF\uB85C \uC800\uC7A5\uD558\uBA74 \uC804\uCCB4 \uB0B4\uC6A9\uC744 \uC5EC\uB7EC \uD398\uC774\uC9C0\uC5D0 \uB098\uB204\uC5B4 \uB2F4\uC744 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            map["Save as PDF"] = "PDF\uB85C \uC800\uC7A5";
            map["XML copied."] = "XML\uC744 \uBCF5\uC0AC\uD588\uC2B5\uB2C8\uB2E4.";
            map["Markdown copied."] = "\uB9C8\uD06C\uB2E4\uC6B4\uC744 \uBCF5\uC0AC\uD588\uC2B5\uB2C8\uB2E4.";
            map["Copied"] = "\uBCF5\uC0AC\uB428";
            map["Image copied."] = "\uC774\uBBF8\uC9C0\uB97C \uBCF5\uC0AC\uD588\uC2B5\uB2C8\uB2E4.";
            map["Could not create image."] = "\uC774\uBBF8\uC9C0\uB97C \uB9CC\uB4E4 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Could not add capture."] = "\uCEA1\uCC98\uB97C \uCD94\uAC00\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Could not create capture."] = "\uCEA1\uCC98\uB97C \uB9CC\uB4E4 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";

            // Markdown/MarkdownWindow.Outline.cs
            map["Contents"] = "\uBAA9\uCC28";
            map["Contents: show or hide the document's headings"] = "\uBB38\uC11C \uC81C\uBAA9 \uBAA9\uB85D \uD45C\uC2DC \uB610\uB294 \uC228\uAE30\uAE30";
            map["Document contents"] = "\uBB38\uC11C \uBAA9\uCC28";
            map["No headings yet. Lines that start with # will show up here."] = "\uC544\uC9C1 \uC81C\uBAA9\uC774 \uC5C6\uC2B5\uB2C8\uB2E4. #\uC73C\uB85C \uC2DC\uC791\uD558\uB294 \uC904\uC774 \uC5EC\uAE30\uC5D0 \uD45C\uC2DC\uB429\uB2C8\uB2E4.";

            // Markdown/MarkdownWindow.Paste.cs
            map["Could not access clipboard."] = "\uD074\uB9BD\uBCF4\uB4DC\uC5D0 \uC811\uADFC\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["No text on clipboard."] = "\uD074\uB9BD\uBCF4\uB4DC\uC5D0 \uD14D\uC2A4\uD2B8\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Could not paste table."] = "\uD45C\uB97C \uBD99\uC5EC\uB123\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Table pasted ({0} \u00D7 {1})."] = "\uD45C\uB97C \uBD99\uC5EC\uB123\uC5C8\uC2B5\uB2C8\uB2E4({0} \u00D7 {1}).";
            map["Pasting table\u2026"] = "\uD45C \uBD99\uC5EC\uB123\uB294 \uC911\u2026";
            map["Images are saved in the \u201C{0}\u201D folder, next to the document."] = "\uC774\uBBF8\uC9C0\uB97C \uBB38\uC11C \uC606 \u2018{0}\u2019 \uD3F4\uB354\uC5D0 \uC800\uC7A5\uD569\uB2C8\uB2E4.";
            map["Save the document before pasting image"] = "\uC774\uBBF8\uC9C0\uB97C \uBD99\uC5EC\uB123\uAE30 \uC804\uC5D0 \uBB38\uC11C\uB97C \uC800\uC7A5\uD558\uC138\uC694";
            map["Saving image\u2026"] = "\uC774\uBBF8\uC9C0 \uC800\uC7A5 \uC911\u2026";
            map["Image pasted: {0}."] = "\uC774\uBBF8\uC9C0\uB97C \uBD99\uC5EC\uB123\uC5C8\uC2B5\uB2C8\uB2E4: {0}.";
            map["Save the document before adding image"] = "\uC774\uBBF8\uC9C0\uB97C \uCD94\uAC00\uD558\uAE30 \uC804\uC5D0 \uBB38\uC11C\uB97C \uC800\uC7A5\uD558\uC138\uC694";
            map["Save the document before adding images"] = "\uC5EC\uB7EC \uC774\uBBF8\uC9C0\uB97C \uCD94\uAC00\uD558\uAE30 \uC804\uC5D0 \uBB38\uC11C\uB97C \uC800\uC7A5\uD558\uC138\uC694";
            map["Could not add image."] = "\uC774\uBBF8\uC9C0\uB97C \uCD94\uAC00\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Image added: {0}."] = "\uC774\uBBF8\uC9C0\uB97C \uCD94\uAC00\uD588\uC2B5\uB2C8\uB2E4: {0}.";
            map["{0} images added."] = "\uC774\uBBF8\uC9C0 {0}\uAC1C\uB97C \uCD94\uAC00\uD588\uC2B5\uB2C8\uB2E4.";

            // Markdown/MarkdownWindow.Pdf.cs
            map["Exporting PDF\u2026"] = "PDF \uB0B4\uBCF4\uB0B4\uB294 \uC911\u2026";
            map["Export PDF\u2026"] = "PDF \uB0B4\uBCF4\uB0B4\uAE30\u2026";
            map["Already exporting a PDF\u2026"] = "\uC774\uBBF8 PDF\uB97C \uB0B4\uBCF4\uB0B4\uACE0 \uC788\uC2B5\uB2C8\uB2E4\u2026";
            map["Export PDF"] = "PDF \uB0B4\uBCF4\uB0B4\uAE30";
            map["PDF document|*.pdf|All files|*.*"] = "PDF \uBB38\uC11C|*.pdf|\uBAA8\uB4E0 \uD30C\uC77C|*.*";
            map["Exporting PDF\u2026 page {0}"] = "PDF \uB0B4\uBCF4\uB0B4\uB294 \uC911\u2026 {0}\uCABD";
            map["Could not save PDF: "] = "PDF\uB97C \uC800\uC7A5\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4: ";
            map["Could not export PDF."] = "PDF\uB97C \uB0B4\uBCF4\uB0BC \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Cancelled."] = "\uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4.";

            // Markdown/MarkdownWindow.Soap.cs
            map["Empty Body"] = "\uBCF8\uBB38\uC774 \uBE44\uC5B4 \uC788\uC74C";
            map["No message"] = "\uBA54\uC2DC\uC9C0 \uC5C6\uC74C";
            map["Fault code: {0}"] = "\uC624\uB958 \uCF54\uB4DC: {0}";
            map["Fault response"] = "\uC624\uB958 \uC751\uB2F5";
            map["No SOAP Fault"] = "SOAP \uC624\uB958 \uC5C6\uC74C";
            map["Previous XML in folder ([)"] = "\uD3F4\uB354\uC758 \uC774\uC804 XML ([)";
            map["Next XML in folder (])"] = "\uD3F4\uB354\uC758 \uB2E4\uC74C XML (])";

            // Markdown/MarkdownWindow.XmlAttach.cs
            map["Opening attachment\u2026"] = "\uCCA8\uBD80 \uD30C\uC77C \uC5EC\uB294 \uC911\u2026";
            map["Use \u201CSave attachment as\u2026\u201D."] = "\u2018\uB2E4\uB978 \uC774\uB984\uC73C\uB85C \uCCA8\uBD80 \uD30C\uC77C \uC800\uC7A5\u2026\u2019\uC744 \uC120\uD0DD\uD558\uC138\uC694.";
            map["Could not read attachment."] = "\uCCA8\uBD80 \uD30C\uC77C\uC744 \uC77D\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["use \u201CSave attachment as\u2026\u201D"] = "\u2018\uB2E4\uB978 \uC774\uB984\uC73C\uB85C \uCCA8\uBD80 \uD30C\uC77C \uC800\uC7A5\u2026\u2019\uC744 \uC120\uD0DD\uD558\uC138\uC694";
            map["For security reasons programs, scripts and shortcuts are not opened: {0}."] = "\uBCF4\uC548\uC744 \uC704\uD574 \uD504\uB85C\uADF8\uB7A8, \uC2A4\uD06C\uB9BD\uD2B8, \uBC14\uB85C\uAC00\uAE30\uB294 \uC5F4\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4: {0}.";
            map["It is a ZIP file: {0}."] = "ZIP \uD30C\uC77C\uC785\uB2C8\uB2E4: {0}.";
            map["Contains macros: {0} if you trust it."] = "\uB9E4\uD06C\uB85C\uAC00 \uD3EC\uD568\uB3FC \uC788\uC2B5\uB2C8\uB2E4. \uC2E0\uB8B0\uD560 \uC218 \uC788\uB294 \uACBD\uC6B0\uC5D0\uB9CC {0}.";
            map["Unknown file type: {0}."] = "\uC54C \uC218 \uC5C6\uB294 \uD30C\uC77C \uD615\uC2DD\uC785\uB2C8\uB2E4: {0}.";
            map["Could not write attachment."] = "\uCCA8\uBD80 \uD30C\uC77C\uC744 \uC4F8 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Could not open image in editor."] = "\uD3B8\uC9D1\uAE30\uC5D0\uC11C \uC774\uBBF8\uC9C0\uB97C \uC5F4 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Open attachment \u201C{0}\u201D?"] = "\uCCA8\uBD80 \uD30C\uC77C \u2018{0}\u2019\uC744(\uB97C) \uC5F4\uAE4C\uC694?";
            map["It's {0} that came inside the XML. It will open with the program Windows uses for {1} files."] = "XML\uC5D0 \uD3EC\uD568\uB41C {0}\uC785\uB2C8\uB2E4. Windows\uC5D0\uC11C {1} \uD30C\uC77C\uC5D0 \uC5F0\uACB0\uD55C \uD504\uB85C\uADF8\uB7A8\uC73C\uB85C \uC5FD\uB2C8\uB2E4.";
            map["Could not open \u201C{0}\u201D: perhaps there is no program for {1} files."] = "\u2018{0}\u2019\uC744(\uB97C) \uC5F4 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. {1} \uD30C\uC77C\uC744 \uC5F4 \uD504\uB85C\uADF8\uB7A8\uC774 \uC788\uB294\uC9C0 \uD655\uC778\uD558\uC138\uC694.";
            map["Could not read GIF image."] = "GIF \uC774\uBBF8\uC9C0\uB97C \uC77D\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Reading attachment\u2026"] = "\uCCA8\uBD80 \uD30C\uC77C \uC77D\uB294 \uC911\u2026";
            map["Saving attachment\u2026"] = "\uCCA8\uBD80 \uD30C\uC77C \uC800\uC7A5 \uC911\u2026";
            map["Attachment saved: {0}."] = "\uCCA8\uBD80 \uD30C\uC77C\uC744 \uC800\uC7A5\uD588\uC2B5\uB2C8\uB2E4: {0}.";
            map["Could not save attachment."] = "\uCCA8\uBD80 \uD30C\uC77C\uC744 \uC800\uC7A5\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Save attachment"] = "\uCCA8\uBD80 \uD30C\uC77C \uC800\uC7A5";
            map["{0} file|*{1}|"] = "{0} \uD30C\uC77C|*{1}|";

            // Markdown/MarkdownWindow.XmlCompare.cs
            map["No differences"] = "\uCC28\uC774 \uC5C6\uC74C";
            map["Comparison"] = "\uBE44\uAD50";
            map["before "] = "\uC774\uC804 ";
            map["   after "] = "   \uC774\uD6C4 ";
            map["Before: {0}\nAfter: {1}"] = "\uC774\uC804: {0}\n\uC774\uD6C4: {1}";
            map["Differences only"] = "\uCC28\uC774\uB9CC \uD45C\uC2DC";
            map["Show only what changes (and path leading to it)"] = "\uBCC0\uACBD\uB41C \uD56D\uBAA9\uACFC \uADF8 \uACBD\uB85C\uB9CC \uD45C\uC2DC";
            map["Previous difference (Shift+F7)"] = "\uC774\uC804 \uCC28\uC774 (Shift+F7)";
            map["Next difference (F7)"] = "\uB2E4\uC74C \uCC28\uC774 (F7)";
            map["Exit comparison"] = "\uBE44\uAD50 \uC885\uB8CC";
            map["Exit comparison (Esc)"] = "\uBE44\uAD50 \uC885\uB8CC (Esc)";
            map["{0} of {1}"] = "{0}/{1}";
            map["No differences."] = "\uCC28\uC774\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Back to first difference."] = "\uCCAB \uBC88\uC9F8 \uCC28\uC774\uB85C \uB3CC\uC544\uC654\uC2B5\uB2C8\uB2E4.";
            map["Back to last difference."] = "\uB9C8\uC9C0\uB9C9 \uCC28\uC774\uB85C \uB3CC\uC544\uC654\uC2B5\uB2C8\uB2E4.";
            map["Compare with previous XML in folder"] = "\uD3F4\uB354\uC758 \uC774\uC804 XML\uACFC \uBE44\uAD50";
            map["Compare with another file\u2026"] = "\uB2E4\uB978 \uD30C\uC77C\uACFC \uBE44\uAD50\u2026";
            map["Exit comparison   Esc"] = "\uBE44\uAD50 \uC885\uB8CC   Esc";
            map["Copy as JSON"] = "JSON\uC73C\uB85C \uBCF5\uC0AC";
            map["Selected node"] = "\uC120\uD0DD\uD55C \uB178\uB4DC";
            map["Whole document"] = "\uBB38\uC11C \uC804\uCCB4";
            map["Compare with another XML"] = "\uB2E4\uB978 XML\uACFC \uBE44\uAD50";
            map["XML|*.xml;*.xsd;*.config;*.svg;*.xaml|All files|*.*"] = "XML|*.xml;*.xsd;*.config;*.svg;*.xaml|\uBAA8\uB4E0 \uD30C\uC77C|*.*";
            map["Fix XML before comparing."] = "\uBE44\uAD50\uD558\uAE30 \uC804\uC5D0 XML \uC624\uB958\uB97C \uC218\uC815\uD558\uC138\uC694.";
            map["One moment: still reading XML."] = "\uC7A0\uC2DC\uB9CC \uAE30\uB2E4\uB824 \uC8FC\uC138\uC694. XML\uC744 \uC77D\uACE0 \uC788\uC2B5\uB2C8\uB2E4.";
            map["Comparing\u2026"] = "\uBE44\uAD50 \uC911\u2026";
            map["No previous XML in folder."] = "\uD3F4\uB354\uC5D0 \uC774\uC804 XML\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["\u201C{0}\u201D is not well-formed XML. {1}"] = "\u2018{0}\u2019\uC740(\uB294) \uC62C\uBC14\uB978 \uD615\uC2DD\uC758 XML\uC774 \uC544\uB2D9\uB2C8\uB2E4. {1}";
            map["Could not compare XML."] = "XML\uC744 \uBE44\uAD50\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["The XML is very large: only its first {0} nodes are compared."] = "XML\uC774 \uB108\uBB34 \uCEE4\uC11C \uCC98\uC74C {0}\uAC1C\uC758 \uB178\uB4DC\uB9CC \uBE44\uAD50\uD569\uB2C8\uB2E4.";

            // Markdown/MdHtml.cs
            map["Document"] = "\uBB38\uC11C";

            // Markdown/MdImages.cs
            map["Cannot find the image"] = "\uC774\uBBF8\uC9C0\uB97C \uCC3E\uC744 \uC218 \uC5C6\uC74C";
            map["Cannot read the image"] = "\uC774\uBBF8\uC9C0\uB97C \uC77D\uC744 \uC218 \uC5C6\uC74C";
            map["The image is too big to show"] = "\uC774\uBBF8\uC9C0\uAC00 \uB108\uBB34 \uCEE4\uC11C \uD45C\uC2DC\uD560 \uC218 \uC5C6\uC74C";
            map["Cannot show SVG images"] = "SVG \uC774\uBBF8\uC9C0\uB97C \uD45C\uC2DC\uD560 \uC218 \uC5C6\uC74C";
            map["Could not save image: "] = "\uC774\uBBF8\uC9C0\uB97C \uC800\uC7A5\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4: ";
            map["Could not copy {0}: {1}"] = "{0}\uC744(\uB97C) \uBCF5\uC0AC\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4: {1}";

            // Xml/XmlAttachment.cs
            map["unknown type"] = "\uC54C \uC218 \uC5C6\uB294 \uD615\uC2DD";
            map["There is no attachment."] = "\uCCA8\uBD80 \uD30C\uC77C\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["The attachment is too large (maximum {0} MB)."] = "\uCCA8\uBD80 \uD30C\uC77C\uC774 \uB108\uBB34 \uD07D\uB2C8\uB2E4(\uCD5C\uB300 {0} MB).";
            map["Field is not valid base64."] = "\uD544\uB4DC\uAC00 \uC62C\uBC14\uB978 base64 \uD615\uC2DD\uC774 \uC544\uB2D9\uB2C8\uB2E4.";
            map["PNG image"] = "PNG \uC774\uBBF8\uC9C0";
            map["JPEG image"] = "JPEG \uC774\uBBF8\uC9C0";
            map["GIF image"] = "GIF \uC774\uBBF8\uC9C0";
            map["BMP image"] = "BMP \uC774\uBBF8\uC9C0";
            map["TIFF image"] = "TIFF \uC774\uBBF8\uC9C0";
            map["an executable"] = "\uC2E4\uD589 \uD30C\uC77C";
            map["a shortcut"] = "\uBC14\uB85C\uAC00\uAE30";
            map["ZIP or Office"] = "ZIP \uB610\uB294 Office \uD30C\uC77C";
            map["an Office document with macros"] = "\uB9E4\uD06C\uB85C\uAC00 \uD3EC\uD568\uB41C Office \uBB38\uC11C";
            map["Word document"] = "Word \uBB38\uC11C";
            map["Excel spreadsheet"] = "Excel \uC2A4\uD504\uB808\uB4DC\uC2DC\uD2B8";
            map["PowerPoint presentation"] = "PowerPoint \uD504\uB808\uC820\uD14C\uC774\uC158";
            map["a ZIP file"] = "ZIP \uD30C\uC77C";
            map["Could not write attachment to temporary folder."] = "\uC784\uC2DC \uD3F4\uB354\uC5D0 \uCCA8\uBD80 \uD30C\uC77C\uC744 \uC4F8 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";

            // Xml/XmlDiffView.cs
            map[" unchanged node"] = "\uAC1C \uB178\uB4DC \uBCC0\uACBD \uC5C6\uC74C";
            map[" unchanged nodes"] = "\uAC1C \uB178\uB4DC \uBCC0\uACBD \uC5C6\uC74C";
            map[" \u00B7 inner XML"] = " \u00B7 \uB0B4\uBD80 XML";
            map["   inner XML"] = "   \uB0B4\uBD80 XML";
            map["The two XML files are the same"] = "\uB450 XML \uD30C\uC77C\uC774 \uAC19\uC2B5\uB2C8\uB2E4";
            map["Whitespace, attribute order and namespace prefixes don't count."] = "\uACF5\uBC31, \uD2B9\uC131 \uC21C\uC11C, \uB124\uC784\uC2A4\uD398\uC774\uC2A4 \uC811\uB450\uC0AC\uB294 \uBE44\uAD50\uC5D0\uC11C \uC81C\uC678\uD569\uB2C8\uB2E4.";
            map["Next difference   F7"] = "\uB2E4\uC74C \uCC28\uC774   F7";
            map["Previous difference   Shift+F7"] = "\uC774\uC804 \uCC28\uC774   Shift+F7";
            map["Expand"] = "\uD3BC\uCE58\uAE30";
            map["Collapse"] = "\uC811\uAE30";
            map["{0} lines copied."] = "{0}\uC904\uC744 \uBCF5\uC0AC\uD588\uC2B5\uB2C8\uB2E4.";
            map["No differences to display."] = "\uD45C\uC2DC\uD560 \uCC28\uC774\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["\u2026 and {0} more lines"] = "\u2026 \uC678 {0}\uC904";
            map["The comparison is too long for a single image."] = "\uBE44\uAD50 \uACB0\uACFC\uAC00 \uB108\uBB34 \uAE38\uC5B4 \uD55C \uC7A5\uC758 \uC774\uBBF8\uC9C0\uB85C \uB9CC\uB4E4 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";

            // Xml/XmlDoc.cs
            map["It's too big (maximum 8 MB of XML)."] = "XML\uC774 \uB108\uBB34 \uD07D\uB2C8\uB2E4(\uCD5C\uB300 8 MB).";
            map["Line {0}, column {1}: "] = "{0}\uD589 {1}\uC5F4: ";
            map["Could not read the XML: "] = "XML\uC744 \uC77D\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4: ";
            map["No XML elements found."] = "XML \uC694\uC18C\uB97C \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
            map["It has a DOCTYPE (DTD) declaration, which Stackshot doesn't process for security. Remove that line to view it."] = "\uBCF4\uC548\uC0C1 Stackshot\uC740 DOCTYPE(DTD) \uC120\uC5B8\uC744 \uCC98\uB9AC\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4. \uBB38\uC11C\uB97C \uBCF4\uB824\uBA74 \uD574\uB2F9 \uC904\uC744 \uC0AD\uC81C\uD558\uC138\uC694.";
            map["Closing tag </{0}> does not match <{1}>, opened on line {2}, column {3}."] = "\uB2EB\uB294 \uD0DC\uADF8 </{0}>\uAC00 {2}\uD589 {3}\uC5F4\uC5D0\uC11C \uC5F0 <{1}>\uC640 \uC77C\uCE58\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
            map["The file ends without closing: {0}."] = "\uB2E4\uC74C \uD0DC\uADF8\uB97C \uB2EB\uAE30 \uC804\uC5D0 \uD30C\uC77C\uC774 \uB05D\uB0AC\uC2B5\uB2C8\uB2E4: {0}.";
            map["The file ends sooner than expected."] = "\uC608\uC0C1\uBCF4\uB2E4 \uC77C\uCC0D \uD30C\uC77C\uC774 \uB05D\uB0AC\uC2B5\uB2C8\uB2E4.";
            map["Root element is missing."] = "\uB8E8\uD2B8 \uC694\uC18C\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["There are multiple root elements."] = "\uB8E8\uD2B8 \uC694\uC18C\uAC00 \uC5EC\uB7EC \uAC1C \uC788\uC2B5\uB2C8\uB2E4.";
            map["There is text outside the root element, or the document starts wrong."] = "\uB8E8\uD2B8 \uC694\uC18C \uBC16\uC5D0 \uD14D\uC2A4\uD2B8\uAC00 \uC788\uAC70\uB098 \uBB38\uC11C \uC2DC\uC791 \uBD80\uBD84\uC774 \uC62C\uBC14\uB974\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
            map["The attribute {0} is repeated."] = "{0} \uD2B9\uC131\uC774 \uC911\uBCF5\uB418\uC5C8\uC2B5\uB2C8\uB2E4.";
            map["Invalid entity reference (to write & use &amp;)."] = "\uC62C\uBC14\uB974\uC9C0 \uC54A\uC740 \uC5D4\uD130\uD2F0 \uCC38\uC870\uC785\uB2C8\uB2E4(&\uB294 &amp;\uB85C \uC785\uB825\uD558\uC138\uC694).";
            map["There is a character XML doesn't allow. "] = "XML\uC5D0\uC11C \uD5C8\uC6A9\uD558\uC9C0 \uC54A\uB294 \uBB38\uC790\uAC00 \uC788\uC2B5\uB2C8\uB2E4. ";
            map["Undeclared namespace prefix. "] = "\uC120\uC5B8\uB418\uC9C0 \uC54A\uC740 \uB124\uC784\uC2A4\uD398\uC774\uC2A4 \uC811\uB450\uC0AC\uC785\uB2C8\uB2E4. ";
            map["It's so deeply nested or so big that it can't be formatted with indentation."] = "\uC911\uCCA9\uC774 \uB108\uBB34 \uAE4A\uAC70\uB098 \uBB38\uC11C\uAC00 \uB108\uBB34 \uCEE4\uC11C \uB4E4\uC5EC\uC4F0\uAE30\uB97C \uC801\uC6A9\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";

            // Xml/XmlJson.cs
            map["Nothing selected."] = "\uC120\uD0DD\uD55C \uD56D\uBAA9\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Only elements and text can be copied as JSON."] = "\uC694\uC18C\uC640 \uD14D\uC2A4\uD2B8\uB9CC JSON\uC73C\uB85C \uBCF5\uC0AC\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            map["Too large to copy as JSON."] = "JSON\uC73C\uB85C \uBCF5\uC0AC\uD558\uAE30\uC5D0\uB294 \uB108\uBB34 \uD07D\uB2C8\uB2E4.";

            // Xml/XmlTreeAccess.cs
            map["Added: "] = "\uCD94\uAC00\uB428: ";
            map["Removed: "] = "\uC81C\uAC70\uB428: ";
            map["Changed: "] = "\uBCC0\uACBD\uB428: ";
            map["Differences between the two XML files"] = "\uB450 XML \uD30C\uC77C\uC758 \uCC28\uC774";
            map["XML tree"] = "XML \uD2B8\uB9AC";

            // Xml/XmlTreeView.cs
            map["Previous (Shift+Enter)"] = "\uC774\uC804 (Shift+Enter)";
            map["Next (Enter)"] = "\uB2E4\uC74C (Enter)";
            map["Show only matching"] = "\uC77C\uCE58\uD558\uB294 \uD56D\uBAA9\uB9CC \uD45C\uC2DC";
            map["Copy path (XPath)"] = "\uACBD\uB85C \uBCF5\uC0AC (XPath)";
            map["Partially copied: selection is too large."] = "\uC120\uD0DD\uD55C \uBC94\uC704\uAC00 \uB108\uBB34 \uCEE4\uC11C \uC77C\uBD80\uB9CC \uBCF5\uC0AC\uD588\uC2B5\uB2C8\uB2E4.";
            map["Nothing to display."] = "\uD45C\uC2DC\uD560 \uB0B4\uC6A9\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["\u2026 and {0} more lines (collapse what you don't need to see them)"] = "\u2026 \uC678 {0}\uC904 (\uBD88\uD544\uC694\uD55C \uD56D\uBAA9\uC744 \uC811\uC73C\uBA74 \uB354 \uBCFC \uC218 \uC788\uC2B5\uB2C8\uB2E4)";
            map["The XML is too long for a single image."] = "XML\uC774 \uB108\uBB34 \uAE38\uC5B4 \uD55C \uC7A5\uC758 \uC774\uBBF8\uC9C0\uB85C \uB9CC\uB4E4 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";

            // Xml/XmlTreeView.ValueMenu.cs
            map["Copy value"] = "\uAC12 \uBCF5\uC0AC";
            map["Value copied."] = "\uAC12\uC744 \uBCF5\uC0AC\uD588\uC2B5\uB2C8\uB2E4.";
            map["Copy value of \u201C{0}\u201D"] = "\u2018{0}\u2019\uC758 \uAC12 \uBCF5\uC0AC";
            map["Open attachment ({0})"] = "\uCCA8\uBD80 \uD30C\uC77C \uC5F4\uAE30 ({0})";
            map["Open attachment"] = "\uCCA8\uBD80 \uD30C\uC77C \uC5F4\uAE30";
            map["It is {0}: can only be saved"] = "{0} \uD615\uC2DD: \uC800\uC7A5\uB9CC \uAC00\uB2A5";
            map["Its name is for a program or script: can only be saved"] = "\uD504\uB85C\uADF8\uB7A8 \uB610\uB294 \uC2A4\uD06C\uB9BD\uD2B8 \uD30C\uC77C \uC774\uB984: \uC800\uC7A5\uB9CC \uAC00\uB2A5";
            map["Unknown type: can only be saved"] = "\uC54C \uC218 \uC5C6\uB294 \uD615\uC2DD: \uC800\uC7A5\uB9CC \uAC00\uB2A5";
            map["Save attachment as\u2026"] = "\uB2E4\uB978 \uC774\uB984\uC73C\uB85C \uCCA8\uBD80 \uD30C\uC77C \uC800\uC7A5\u2026";
            map["JSON copied."] = "JSON\uC744 \uBCF5\uC0AC\uD588\uC2B5\uB2C8\uB2E4.";
            map["Could not convert to JSON."] = "JSON\uC73C\uB85C \uBCC0\uD658\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
        }
    }
}
