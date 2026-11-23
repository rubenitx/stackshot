// Stackshot - Korean translations for the application, installation and updates.
// MIT License - https://github.com/rubenitx/stackshot
using System.Collections.Generic;

namespace Stackshot
{
    public static partial class I18n
    {
        static void RegisterKoreanCore(Dictionary<string, string> map)
        {
            // FileAssoc.cs
            map["Screenshots, video and GIF, with a video player and a Markdown and XML viewer"] = "\uCEA1\uCC98\uC640 \uB3D9\uC601\uC0C1\u00B7GIF \uB179\uD654, \uB3D9\uC601\uC0C1 \uD50C\uB808\uC774\uC5B4\uC640 Markdown\u00B7XML \uBDF0\uC5B4";

            // LinksWindow.cs
            map["Quick links"] = "\uBC14\uB85C\uAC00\uAE30";
            map["Websites, folders, files or programs you want handy: they appear in the mascot's menu and open with a click."] = "\uC790\uC8FC \uC4F0\uB294 \uC6F9\uC0AC\uC774\uD2B8, \uD3F4\uB354, \uD30C\uC77C, \uD504\uB85C\uADF8\uB7A8\uC744 \uCD94\uAC00\uD558\uBA74 \uB9C8\uC2A4\uCF54\uD2B8 \uBA54\uB274\uC5D0\uC11C \uBC14\uB85C \uC5F4 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            map["Web address or path"] = "\uC6F9 \uC8FC\uC18C \uB610\uB294 \uACBD\uB85C";
            map["https://\u2026 or C:\\Folder\\file"] = "https://\u2026 \uB610\uB294 C:\\\uD3F4\uB354\\\uD30C\uC77C";
            map["Name"] = "\uC774\uB984";
            map["Name (optional)"] = "\uC774\uB984(\uC120\uD0DD \uC0AC\uD56D)";
            map["Choose folder\u2026"] = "\uD3F4\uB354 \uC120\uD0DD\u2026";
            map["Choose file or program\u2026"] = "\uD30C\uC77C \uB610\uB294 \uD504\uB85C\uADF8\uB7A8 \uC120\uD0DD\u2026";
            map["Add"] = "\uCD94\uAC00";
            map["Your links ({0} of {1})"] = "\uB0B4 \uBC14\uB85C\uAC00\uAE30 ({0}/{1})";
            map["Open {0}"] = "{0} \uC5F4\uAE30";
            map["Remove {0}"] = "{0} \uC81C\uAC70";
            map["Remove"] = "\uC81C\uAC70";
            map["Done! \u201C{0}\u201D is now in my menu."] = "\uC644\uB8CC! \uBA54\uB274\uC5D0 \u2018{0}\u2019 \uBC14\uB85C\uAC00\uAE30\uB97C \uCD94\uAC00\uD588\uC5B4\uC694.";
            map["Choose the folder you want to have handy"] = "\uBC14\uB85C\uAC00\uAE30\uB85C \uCD94\uAC00\uD560 \uD3F4\uB354 \uC120\uD0DD";
            map["Choose the file or program"] = "\uD30C\uC77C \uB610\uB294 \uD504\uB85C\uADF8\uB7A8 \uC120\uD0DD";
            map["All files|*.*"] = "\uBAA8\uB4E0 \uD30C\uC77C|*.*";

            // NameTemplate.cs
            map["Capture"] = "\uCEA1\uCC98";
            map["Sales Report.xlsx - Excel"] = "\uB9E4\uCD9C \uBCF4\uACE0\uC11C.xlsx - Excel";

            // Net.cs
            map["Only HTTPS connections are allowed: "] = "HTTPS \uC5F0\uACB0\uB9CC \uD5C8\uC6A9\uB429\uB2C8\uB2E4: ";

            // NewsWindow.cs
            map["What's new"] = "\uC0C8\uB85C\uC6B4 \uAE30\uB2A5";
            map["What's new in Stackshot {0}"] = "Stackshot {0}\uC758 \uC0C8\uB85C\uC6B4 \uAE30\uB2A5";
            map["The new version: here is what's new."] = "\uC0C8 \uBC84\uC804\uC5D0\uC11C \uB2EC\uB77C\uC9C4 \uC810\uC744 \uD655\uC778\uD558\uC138\uC694.";
            map["Everything new since the version you had."] = "\uC774\uC804\uC5D0 \uC0AC\uC6A9\uD55C \uBC84\uC804 \uC774\uD6C4\uC758 \uBCC0\uACBD \uC0AC\uD56D\uC785\uB2C8\uB2E4.";
            map["What's new in your current version."] = "\uD604\uC7AC \uBC84\uC804\uC758 \uBCC0\uACBD \uC0AC\uD56D\uC785\uB2C8\uB2E4.";
            map["In {0}"] = "\uBC84\uC804 {0}";
            map["This version has no release notes."] = "\uC774 \uBC84\uC804\uC5D0\uB294 \uB9B4\uB9AC\uC2A4 \uB178\uD2B8\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Not now"] = "\uB098\uC911\uC5D0";
            map["Updating\u2026"] = "\uC5C5\uB370\uC774\uD2B8 \uC911\u2026";
            map["View version"] = "\uBC84\uC804 \uBCF4\uAE30";
            map["Install now"] = "\uC9C0\uAE08 \uC124\uCE58";
            map["Download and install"] = "\uB2E4\uC6B4\uB85C\uB4DC \uBC0F \uC124\uCE58";

            // Policy.cs
            map["Managed by your organization"] = "\uC870\uC9C1\uC5D0\uC11C \uAD00\uB9AC";
            map["Some settings are managed by your organization."] = "\uC77C\uBD80 \uC124\uC815\uC740 \uC870\uC9C1\uC5D0\uC11C \uAD00\uB9AC\uD569\uB2C8\uB2E4.";

            // Program.cs
            map["Could not open {0}.\n\n{1}"] = "{0}\uC744(\uB97C) \uC5F4 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.\n\n{1}";
            map["This machine has Stackshot {0} installed via MSI package.\n\nTo update to {1}, install Stackshot.msi for that version (or ask IT)."] = "\uC774 PC\uC5D0\uB294 MSI\uB85C \uC124\uCE58\uD55C Stackshot {0}\uC774 \uC788\uC2B5\uB2C8\uB2E4.\n\n{1} \uBC84\uC804\uC758 Stackshot.msi\uB97C \uC124\uCE58\uD558\uAC70\uB098 IT \uB2F4\uB2F9\uC790\uC5D0\uAC8C \uBB38\uC758\uD558\uC138\uC694.";
            map["Update Stackshot from version {0} to {1}?"] = "Stackshot\uC744 {0}\uC5D0\uC11C {1}(\uC73C)\uB85C \uC5C5\uB370\uC774\uD2B8\uD560\uAE4C\uC694?";
            map["Does not seem to be a valid image, or is corrupted."] = "\uC62C\uBC14\uB978 \uC774\uBBF8\uC9C0\uAC00 \uC544\uB2C8\uAC70\uB098 \uD30C\uC77C\uC774 \uC190\uC0C1\uB418\uC5C8\uC2B5\uB2C8\uB2E4.";
            map["The file cannot be opened in the editor."] = "\uD3B8\uC9D1\uAE30\uC5D0\uC11C \uD30C\uC77C\uC744 \uC5F4 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Could not install Stackshot.\n\n"] = "Stackshot\uC744 \uC124\uCE58\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.\n\n";
            map["\n\nIf Stackshot is still open (saving a recording, unsaved changes), close it and try again."] = "\n\n\uB179\uD654\uB97C \uC800\uC7A5 \uC911\uC774\uAC70\uB098 \uC800\uC7A5\uD558\uC9C0 \uC54A\uC740 \uBCC0\uACBD \uC0AC\uD56D\uC774 \uC788\uB2E4\uBA74 Stackshot\uC744 \uC885\uB8CC\uD55C \uB4A4 \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694.";

            // QuickWindow.cs
            map["Take note"] = "\uBA54\uBAA8 \uC791\uC131";
            map["Reminder"] = "\uBBF8\uB9AC \uC54C\uB9BC";
            map["What should I note?"] = "\uBB34\uC5C7\uC744 \uC801\uC744\uAE4C\uC694?";
            map["What should I remind you of?"] = "\uBB34\uC5C7\uC744 \uC54C\uB824\uB4DC\uB9B4\uAE4C\uC694?";
            map["Whatever you want to note"] = "\uBA54\uBAA8\uD560 \uB0B4\uC6A9";
            map["For example: call Laura"] = "\uC608: \uC9C0\uBBFC\uC5D0\uAC8C \uC804\uD654\uD558\uAE30";
            map["Start a line with \u201C- [ ] \u201D for a task list."] = "\uD560 \uC77C \uBAA9\uB85D\uC740 \uAC01 \uC904\uC744 \u2018- [ ] \u2019\uB85C \uC2DC\uC791\uD558\uC138\uC694.";
            map["When?"] = "\uC5B8\uC81C \uC54C\uB824\uB4DC\uB9B4\uAE4C\uC694?";
            map["Tomorrow"] = "\uB0B4\uC77C";
            map["At\u2026"] = "\uC2DC\uAC04 \uC9C0\uC815\u2026";
            map["I'll remind you {0}."] = "{0}\uC5D0 \uC54C\uB824\uB4DC\uB9B4\uAC8C\uC694.";
            map["Time"] = "\uC2DC\uAC04";
            map["(for example 16:30)"] = "(\uC608: 16:30)";
            map["Pending"] = "\uC608\uC815\uB41C \uC54C\uB9BC";
            map["Remove this reminder"] = "\uC774 \uC54C\uB9BC \uC0AD\uC81C";
            map["Recent notes"] = "\uCD5C\uADFC \uBA54\uBAA8";
            map["Mark as pending"] = "\uBBF8\uC644\uB8CC\uB85C \uD45C\uC2DC";
            map["Mark as done"] = "\uC644\uB8CC\uB85C \uD45C\uC2DC";
            map["That note changed in Notas.md: open it to see it."] = "Notas.md\uC5D0\uC11C \uBA54\uBAA8\uAC00 \uBCC0\uACBD\uB418\uC5C8\uC2B5\uB2C8\uB2E4. \uD30C\uC77C\uC744 \uC5F4\uC5B4 \uD655\uC778\uD558\uC138\uC694.";
            map["Delete this note"] = "\uC774 \uBA54\uBAA8 \uC0AD\uC81C";
            map["Delete"] = "\uC0AD\uC81C";
            map["Unpin from desktop"] = "\uBC14\uD0D5 \uD654\uBA74\uC5D0\uC11C \uACE0\uC815 \uD574\uC81C";
            map["Pin to desktop"] = "\uBC14\uD0D5 \uD654\uBA74\uC5D0 \uACE0\uC815";
            map["View all (Notas.md, in your captures folder)"] = "\uBAA8\uB450 \uBCF4\uAE30 (\uCEA1\uCC98 \uD3F4\uB354\uC758 Notas.md)";
            map["View my notes (Notas.md, in your captures folder)"] = "\uB0B4 \uBA54\uBAA8 \uBCF4\uAE30 (\uCEA1\uCC98 \uD3F4\uB354\uC758 Notas.md)";
            map["Remind me"] = "\uC54C\uB9BC \uC124\uC815";
            map["Save and pin"] = "\uC800\uC7A5 \uD6C4 \uACE0\uC815";
            map["Type what you want to note."] = "\uBA54\uBAA8\uD560 \uB0B4\uC6A9\uC744 \uC785\uB825\uD558\uC138\uC694.";
            map["Type what I should remind you of."] = "\uC54C\uB9BC \uB0B4\uC6A9\uC744 \uC785\uB825\uD558\uC138\uC694.";
            map["Type the time like 16:30."] = "\uC2DC\uAC04\uC744 16:30 \uD615\uC2DD\uC73C\uB85C \uC785\uB825\uD558\uC138\uC694.";
            map["Added to your notes."] = "\uBA54\uBAA8\uC5D0 \uCD94\uAC00\uD588\uC2B5\uB2C8\uB2E4.";

            // ReportHistory.cs
            map["was reopened"] = "\uB2E4\uC2DC \uC5F4\uB9BC";
            map["is done"] = "\uC644\uB8CC\uB428";
            map["is fixed"] = "\uC218\uC815\uB428";
            map["was discarded"] = "\uBC18\uB824\uB428";
            map["is a duplicate"] = "\uC911\uBCF5\uC73C\uB85C \uD45C\uC2DC\uB428";
            map["was closed"] = "\uC885\uB8CC\uB428";
            map["has a new reply"] = "\uC0C8 \uB2F5\uBCC0 1\uAC1C";
            map["has {0} new replies"] = "\uC0C8 \uB2F5\uBCC0 {0}\uAC1C";
            map[" and "] = " \uBC0F ";
            map["\u201C{0}\u201D {1}"] = "\u2018{0}\u2019 {1}";
            map["Cannot check how they are doing right now. Check your connection and click Refresh."] = "\uC9C0\uAE08\uC740 \uC0C1\uD0DC\uB97C \uD655\uC778\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. \uC5F0\uACB0\uC744 \uD655\uC778\uD55C \uB4A4 \uC0C8\uB85C \uACE0\uCE68\uC744 \uB204\uB974\uC138\uC694.";

            // ReportWindow.cs
            map["Report an issue"] = "\uBB38\uC81C \uC2E0\uACE0";
            map["Discard report?"] = "\uC791\uC131\uD558\uB358 \uC2E0\uACE0\uB97C \uC0AD\uC81C\uD560\uAE4C\uC694?";
            map["What you wrote has not been sent."] = "\uC791\uC131\uD55C \uB0B4\uC6A9\uC740 \uC544\uC9C1 \uC804\uC1A1\uB418\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4.";
            map["Discard"] = "\uBC84\uB9AC\uAE30";
            map["Keep typing"] = "\uACC4\uC18D \uC791\uC131";
            map["My reports"] = "\uB0B4 \uC2E0\uACE0";
            map["Tell us what happened"] = "\uC5B4\uB5A4 \uC77C\uC774 \uC788\uC5C8\uB098\uC694?";
            map["It goes straight to the people who make Stackshot, with no accounts or emails. "] = "\uACC4\uC815\uC774\uB098 \uC774\uBA54\uC77C \uC5C6\uC774 Stackshot \uAC1C\uBC1C\uC790\uC5D0\uAC8C \uC9C1\uC811 \uC804\uB2EC\uB429\uB2C8\uB2E4. ";
            map["Continuing opens GitHub with everything you wrote prefilled. "] = "\uACC4\uC18D\uD558\uBA74 \uC791\uC131\uD55C \uB0B4\uC6A9\uC774 \uC785\uB825\uB41C GitHub \uD398\uC774\uC9C0\uAC00 \uC5F4\uB9BD\uB2C8\uB2E4. ";
            map["It is published on GitHub, which is public: do not include personal data, passwords or company info."] = "GitHub\uC5D0 \uACF5\uAC1C\uB429\uB2C8\uB2E4. \uAC1C\uC778\uC815\uBCF4, \uBE44\uBC00\uBC88\uD638, \uD68C\uC0AC \uB0B4\uBD80 \uC815\uBCF4\uB294 \uC785\uB825\uD558\uC9C0 \uB9C8\uC138\uC694.";
            map["Something's broken"] = "\uBB38\uC81C\uAC00 \uC788\uC5B4\uC694";
            map["I have an idea"] = "\uC544\uC774\uB514\uC5B4\uAC00 \uC788\uC5B4\uC694";
            map["Title"] = "\uC81C\uBAA9";
            map["What happened?"] = "\uC5B4\uB5A4 \uC77C\uC774 \uC788\uC5C8\uB098\uC694?";
            map["What would you like?"] = "\uC5B4\uB5A4 \uAE30\uB2A5\uC774 \uD544\uC694\uD55C\uAC00\uC694?";
            map["Description"] = "\uC124\uBA85";
            map["How to reproduce? (optional)"] = "\uC7AC\uD604 \uBC29\uBC95(\uC120\uD0DD \uC0AC\uD56D)";
            map["Steps to reproduce"] = "\uC7AC\uD604 \uB2E8\uACC4";
            map["Include Stackshot log"] = "Stackshot \uB85C\uADF8 \uD3EC\uD568";
            map["Hide what is sent"] = "\uC804\uC1A1\uD560 \uB0B4\uC6A9 \uC228\uAE30\uAE30";
            map["View what is sent"] = "\uC804\uC1A1\uD560 \uB0B4\uC6A9 \uBCF4\uAE30";
            map["--- Log (only with switch turned on) ---"] = "--- \uB85C\uADF8 (\uD3EC\uD568 \uC635\uC158\uC744 \uCF20 \uACBD\uC6B0\uB9CC) ---";
            map["What is sent"] = "\uC804\uC1A1\uD560 \uB0B4\uC6A9";
            map["Always added: "] = "\uD56D\uC0C1 \uD3EC\uD568\uB418\uB294 \uC815\uBCF4: ";
            map[". Your username, computer name, emails, IDs, account and card numbers, and phone numbers are removed from what you write and the log."] = ". \uC791\uC131\uD55C \uB0B4\uC6A9\uACFC \uB85C\uADF8\uC5D0\uC11C \uC0AC\uC6A9\uC790 \uC774\uB984, \uCEF4\uD4E8\uD130 \uC774\uB984, \uC774\uBA54\uC77C, \uC2DD\uBCC4 \uBC88\uD638, \uACC4\uC88C\u00B7\uCE74\uB4DC \uBC88\uD638, \uC804\uD654\uBC88\uD638\uB97C \uC81C\uAC70\uD569\uB2C8\uB2E4.";
            map["Continue on GitHub"] = "GitHub\uC5D0\uC11C \uACC4\uC18D";
            map["Sending\u2026"] = "\uC804\uC1A1 \uC911\u2026";
            map["Retry"] = "\uB2E4\uC2DC \uC2DC\uB3C4";
            map["Send"] = "\uBCF4\uB0B4\uAE30";
            map["Open on GitHub"] = "GitHub\uC5D0\uC11C \uC5F4\uAE30";
            map["Opens the issue in browser with everything you wrote"] = "\uC791\uC131\uD55C \uB0B4\uC6A9\uC774 \uC785\uB825\uB41C \uC774\uC288\uB97C \uBE0C\uB77C\uC6B0\uC800\uC5D0\uC11C \uC5FD\uB2C8\uB2E4";
            map["Sent. Thank you"] = "\uC804\uC1A1\uD588\uC2B5\uB2C8\uB2E4. \uAC10\uC0AC\uD569\uB2C8\uB2E4.";
            map["Your report is number {0}. Under \u201CMy reports\u201D you will see its progress and replies, right here with no account."] = "\uC2E0\uACE0 \uBC88\uD638\uB294 {0}\uC785\uB2C8\uB2E4. \u2018\uB0B4 \uC2E0\uACE0\u2019\uC5D0\uC11C \uACC4\uC815 \uC5C6\uC774 \uC9C4\uD589 \uC0C1\uD669\uACFC \uB2F5\uBCC0\uC744 \uBCFC \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            map["New report"] = "\uC0C8 \uC2E0\uACE0";
            map["What you sent from this computer, how it's going and the replies you get. No account needed."] = "\uC774 PC\uC5D0\uC11C \uBCF4\uB0B8 \uC2E0\uACE0\uC758 \uC9C4\uD589 \uC0C1\uD669\uACFC \uB2F5\uBCC0\uC744 \uD655\uC778\uD558\uC138\uC694. \uACC4\uC815\uC774 \uD544\uC694\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
            map["You haven't sent anything from this computer yet. When you do, it will appear here."] = "\uC774 PC\uC5D0\uC11C \uBCF4\uB0B8 \uC2E0\uACE0\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4. \uC2E0\uACE0\uB97C \uBCF4\uB0B4\uBA74 \uC5EC\uAE30\uC5D0 \uD45C\uC2DC\uB429\uB2C8\uB2E4.";
            map["Checking how they're doing\u2026"] = "\uC0C1\uD0DC \uD655\uC778 \uC911\u2026";
            map["Refresh"] = "\uC0C8\uB85C \uACE0\uCE68";
            map["Sent"] = "\uC804\uC1A1\uB428";
            map["Not available"] = "\uD655\uC778\uD560 \uC218 \uC5C6\uC74C";
            map["Open"] = "\uC5F4\uAE30";
            map["Done"] = "\uC644\uB8CC";
            map["Fixed"] = "\uC218\uC815\uB428";
            map["Duplicate"] = "\uC911\uBCF5";
            map["Discarded"] = "\uBC18\uB824\uB428";
            map["Closed"] = "\uC885\uB8CC\uB428";
            map["New reply"] = "\uC0C8 \uB2F5\uBCC0";
            map["#{0}  \u00B7  {1}  \u00B7  sent on {2}"] = "#{0}  \u00B7  {1}  \u00B7  {2} \uC804\uC1A1";
            map["Idea"] = "\uC544\uC774\uB514\uC5B4";
            map["Bug"] = "\uBC84\uADF8";
            map["Reply from Stackshot"] = "Stackshot\uC758 \uB2F5\uBCC0";
            map["Comment"] = "\uB313\uAE00";
            map["\u201CWhat happened?\u201D"] = "\u2018\uC5B4\uB5A4 \uC77C\uC774 \uC788\uC5C8\uB098\uC694?\u2019";
            map["\u201CWhat would you like?\u201D"] = "\u2018\uC5B4\uB5A4 \uAE30\uB2A5\uC774 \uD544\uC694\uD55C\uAC00\uC694?\u2019";
            map["Type a title and {0}."] = "\uC81C\uBAA9\uACFC {0} \uD56D\uBAA9\uC744 \uC785\uB825\uD558\uC138\uC694.";
            map["Title needs {0} more."] = "\uC81C\uBAA9\uC5D0 {0} \uB354 \uC785\uB825\uD558\uC138\uC694.";
            map["{0} needs {1} more."] = "{0} \uD56D\uBAA9\uC5D0 {1} \uB354 \uC785\uB825\uD558\uC138\uC694.";
            map["1 letter"] = "1\uC790";
            map["{0} letters"] = "{0}\uC790";
            map["Wait a minute before sending another report."] = "\uB2E4\uC74C \uC2E0\uACE0\uB294 1\uBD84 \uB4A4\uC5D0 \uBCF4\uB0BC \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            map["The server did not confirm the report."] = "\uC11C\uBC84\uC5D0\uC11C \uC2E0\uACE0 \uC811\uC218\uB97C \uD655\uC778\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
            map["Too many reports in a row. Try again in a minute."] = "\uC5F0\uB2EC\uC544 \uBCF4\uB0B8 \uC2E0\uACE0\uAC00 \uB108\uBB34 \uB9CE\uC2B5\uB2C8\uB2E4. 1\uBD84 \uB4A4\uC5D0 \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694.";
            map["Could not send ({0})."] = "\uC804\uC1A1\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4({0}).";
            map["No connection to reports server."] = "\uC2E0\uACE0 \uC11C\uBC84\uC5D0 \uC5F0\uACB0\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Could not send: "] = "\uC804\uC1A1\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4: ";
            map["You can retry or open on GitHub."] = "\uB2E4\uC2DC \uC2DC\uB3C4\uD558\uAC70\uB098 GitHub\uC5D0\uC11C \uC5F4 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            map["How to reproduce?"] = "\uC7AC\uD604 \uBC29\uBC95";
            map["Log"] = "\uB85C\uADF8";
            map["1 display: "] = "\uBAA8\uB2C8\uD130 1\uB300: ";
            map["{0} displays: "] = "\uBAA8\uB2C8\uD130 {0}\uB300: ";
            map["(no log)"] = "(\uB85C\uADF8 \uC5C6\uC74C)";
            map["minimum {0} characters"] = "\uCD5C\uC18C {0}\uC790";

            // Setup.cs
            map["Stackshot: screenshots, video and GIF"] = "Stackshot \uCEA1\uCC98 \uBC0F \uB3D9\uC601\uC0C1\u00B7GIF \uB179\uD654";
            map["\n\nRecovered recordings ({0}) remain in {1}."] = "\n\n\uBCF5\uAD6C\uB41C \uB179\uD654 {0}\uAC1C\uB294 {1}\uC5D0 \uBCF4\uAD00\uB429\uB2C8\uB2E4.";
            map["Uninstall Stackshot?\n\nCaptures saved in {0} will not be deleted.{1}"] = "Stackshot\uC744 \uC81C\uAC70\uD560\uAE4C\uC694?\n\n{0}\uC5D0 \uC800\uC7A5\uB41C \uCEA1\uCC98\uB294 \uBCF4\uC874\uB429\uB2C8\uB2E4.{1}";
            map["Stackshot is still open: there are unsaved changes or a recording is being saved.\n\nClose it and uninstall again."] = "\uC800\uC7A5\uD558\uC9C0 \uC54A\uC740 \uBCC0\uACBD \uC0AC\uD56D\uC774 \uC788\uAC70\uB098 \uB179\uD654\uB97C \uC800\uC7A5 \uC911\uC785\uB2C8\uB2E4.\n\nStackshot\uC744 \uC885\uB8CC\uD55C \uB4A4 \uB2E4\uC2DC \uC81C\uAC70\uD558\uC138\uC694.";
            map["Stackshot has been uninstalled. Thank you for trying it!"] = "Stackshot\uC744 \uC81C\uAC70\uD588\uC2B5\uB2C8\uB2E4. \uC0AC\uC6A9\uD574 \uC8FC\uC154\uC11C \uAC10\uC0AC\uD569\uB2C8\uB2E4!";
            map["\n\nThe Print Screen key opens Windows Snipping Tool again."] = "\n\nPrint Screen \uD0A4\uB85C Windows \uCEA1\uCC98 \uB3C4\uAD6C\uB97C \uB2E4\uC2DC \uC5F4 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            map["\n\nRecovered recordings remain in {0}."] = "\n\n\uBCF5\uAD6C\uB41C \uB179\uD654\uB294 {0}\uC5D0 \uBCF4\uAD00\uB429\uB2C8\uB2E4.";

            // ShotStack.Assist.cs
            map["Reminder saved"] = "\uC54C\uB9BC \uC800\uC7A5\uB428";
            map["I'll remind you {0}: \u201C{1}\u201D."] = "{0}\uC5D0 \u2018{1}\u2019 \uC54C\uB9BC\uC744 \uB4DC\uB9B4\uAC8C\uC694.";
            map["at {0}"] = "{0}";
            map["tomorrow {0}"] = "\uB0B4\uC77C {0}";
            map["on {0} {1}"] = "{0} {1}";
            map["Was for {0}: "] = "{0} \uC608\uC815\uC774\uC5C8\uB358 \uC54C\uB9BC: ";
            map["10 more min"] = "10\uBD84 \uB4A4 \uB2E4\uC2DC \uC54C\uB9BC";
            map["# Notes\n\n"] = "# \uBA54\uBAA8\n\n";
            map["Could not add note"] = "\uBA54\uBAA8\uB97C \uCD94\uAC00\uD560 \uC218 \uC5C6\uC74C";
            map["Could not update note"] = "\uBA54\uBAA8\uB97C \uC218\uC815\uD560 \uC218 \uC5C6\uC74C";
            map["Noted! \u201C{0}\u201D is now in my quick links."] = "\u2018{0}\u2019 \uBC14\uB85C\uAC00\uAE30\uB97C \uCD94\uAC00\uD588\uC5B4\uC694!";
            map["Noted {0} folders in my quick links!"] = "\uD3F4\uB354 {0}\uAC1C\uB97C \uBC14\uB85C\uAC00\uAE30\uC5D0 \uCD94\uAC00\uD588\uC5B4\uC694!";
            map["And I'll open the file for you."] = "\uD30C\uC77C\uB3C4 \uC5F4\uC5B4 \uB4DC\uB9B4\uAC8C\uC694.";
            map["And I'll open the {0} files for you."] = "\uD30C\uC77C {0}\uAC1C\uB3C4 \uC5F4\uC5B4 \uB4DC\uB9B4\uAC8C\uC694.";
            map["I don't know what to do with \u201C{0}\u201D. Drop images, videos, XML or Markdown."] = "\u2018{0}\u2019\uC740(\uB294) \uC5F4 \uC218 \uC5C6\uC5B4\uC694. \uC774\uBBF8\uC9C0, \uB3D9\uC601\uC0C1, XML \uB610\uB294 Markdown \uD30C\uC77C\uC744 \uB193\uC544 \uC8FC\uC138\uC694.";
            map["Opening {0} files!"] = "\uD30C\uC77C {0}\uAC1C\uB97C \uC5F4\uAC8C\uC694!";
            map["To the editor!"] = "\uD3B8\uC9D1\uAE30\uC5D0\uC11C \uC5F4\uAC8C\uC694!";
            map["Let's watch it!"] = "\uAC19\uC774 \uBCFC\uAE4C\uC694?";
            map["Opening it for you!"] = "\uC5F4\uC5B4 \uB4DC\uB9B4\uAC8C\uC694!";
            map["Watch out: an XML with a fault has arrived. "] = "\uC624\uB958\uAC00 \uC788\uB294 XML\uC774 \uB4E4\uC5B4\uC654\uC5B4\uC694. ";
            map["XML with fault"] = "\uC624\uB958\uAC00 \uC788\uB294 XML";
            map["Open XML"] = "XML \uC5F4\uAE30";

            // ShotStack.Cli.cs
            map["There is no XML in {0}."] = "{0}\uC5D0 XML \uD30C\uC77C\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";

            // ShotStack.cs
            map["Choose the XML folder"] = "XML \uD3F4\uB354 \uC120\uD0DD";
            map["XML monitoring is on, but has no folder. Pick one in General > Advanced."] = "XML \uBAA8\uB2C8\uD130\uB9C1\uC774 \uCF1C\uC838 \uC788\uC9C0\uB9CC \uC9C0\uC815\uD55C \uD3F4\uB354\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4. \uC77C\uBC18 > \uACE0\uAE09\uC5D0\uC11C \uC120\uD0DD\uD558\uC138\uC694.";
            map["Opening {0}. {1} {2} (the viewer shows one at a time)."] = "{0}\uC744(\uB97C) \uC5FD\uB2C8\uB2E4. \uB098\uBA38\uC9C0 \uD30C\uC77C {1}{2} (\uBDF0\uC5B4\uB294 \uD55C \uBC88\uC5D0 \uD558\uB098\uC529 \uD45C\uC2DC\uD569\uB2C8\uB2E4).";
            map["more file"] = "\uAC1C";
            map["more files"] = "\uAC1C";
            map["No XML folder is monitored yet: choose it in Settings > General > Advanced."] = "\uBAA8\uB2C8\uD130\uB9C1\uD560 XML \uD3F4\uB354\uB97C \uC124\uC815 > \uC77C\uBC18 > \uACE0\uAE09\uC5D0\uC11C \uC120\uD0DD\uD558\uC138\uC694.";
            map["is used by another program (OneDrive, ShareX, Lightshot, Greenshot\u2026?)."] = "\uB2E4\uB978 \uD504\uB85C\uADF8\uB7A8\uC5D0\uC11C \uC0AC\uC6A9 \uC911\uC785\uB2C8\uB2E4(\uC608: OneDrive, ShareX, Lightshot, Greenshot).";
            map["is set twice in Stackshot shortcuts: only the first counts."] = "Stackshot \uB2E8\uCD95\uD0A4\uC5D0 \uB450 \uBC88 \uC9C0\uC815\uB418\uC5C8\uC2B5\uB2C8\uB2E4. \uCCAB \uBC88\uC9F8 \uB2E8\uCD95\uD0A4\uB9CC \uC791\uB3D9\uD569\uB2C8\uB2E4.";
            map["is not a valid key combination."] = "\uC62C\uBC14\uB978 \uD0A4 \uC870\uD569\uC774 \uC544\uB2D9\uB2C8\uB2E4.";
            map["Some shortcuts are occupied"] = "\uC77C\uBD80 \uB2E8\uCD95\uD0A4\uAC00 \uC0AC\uC6A9 \uC911\uC785\uB2C8\uB2E4";
            map["Some shortcuts do not work"] = "\uC77C\uBD80 \uB2E8\uCD95\uD0A4\uAC00 \uC791\uB3D9\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4";
            map["Choose other shortcuts"] = "\uB2E4\uB978 \uB2E8\uCD95\uD0A4 \uC120\uD0DD";
            map["Could not open viewer"] = "\uBDF0\uC5B4\uB97C \uC5F4 \uC218 \uC5C6\uC74C";
            map["Could not capture"] = "\uCEA1\uCC98\uD560 \uC218 \uC5C6\uC74C";
            map["Not enough memory for such a large image. Try a smaller area."] = "\uC774\uBBF8\uC9C0\uAC00 \uB108\uBB34 \uCEE4\uC11C \uBA54\uBAA8\uB9AC\uAC00 \uBD80\uC871\uD569\uB2C8\uB2E4. \uB354 \uC791\uC740 \uC601\uC5ED\uC744 \uC120\uD0DD\uD558\uC138\uC694.";
            map["An unexpected error occurred. Please try again; if it recurs, check the Stackshot log for details."] = "\uC608\uAE30\uCE58 \uC54A\uC740 \uC624\uB958\uAC00 \uBC1C\uC0DD\uD588\uC2B5\uB2C8\uB2E4. \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694. \uBB38\uC81C\uAC00 \uBC18\uBCF5\uB418\uBA74 Stackshot \uB85C\uADF8\uB97C \uD655\uC778\uD558\uC138\uC694.";
            map["Windows wants to sign out and a recording is in progress.\n\nStackshot will stop and save it to your folder before exiting."] = "Windows\uC5D0\uC11C \uB85C\uADF8\uC544\uC6C3\uD558\uB824 \uD558\uC9C0\uB9CC \uB179\uD654\uAC00 \uC9C4\uD589 \uC911\uC785\uB2C8\uB2E4.\n\n\uC885\uB8CC\uD558\uAE30 \uC804\uC5D0 \uB179\uD654\uB97C \uC911\uC9C0\uD558\uACE0 \uC9C0\uC815\uD55C \uD3F4\uB354\uC5D0 \uC800\uC7A5\uD569\uB2C8\uB2E4.";
            map["A recording is in progress.\n\nStackshot will stop and save it to your folder before exiting."] = "\uB179\uD654\uAC00 \uC9C4\uD589 \uC911\uC785\uB2C8\uB2E4.\n\n\uC885\uB8CC\uD558\uAE30 \uC804\uC5D0 \uB179\uD654\uB97C \uC911\uC9C0\uD558\uACE0 \uC9C0\uC815\uD55C \uD3F4\uB354\uC5D0 \uC800\uC7A5\uD569\uB2C8\uB2E4.";
            map["Stop and exit"] = "\uC911\uC9C0 \uD6C4 \uC885\uB8CC";
            map["Keep recording"] = "\uB179\uD654 \uACC4\uC18D";
            map["A recording is in progress in Stackshot"] = "Stackshot\uC5D0\uC11C \uB179\uD654 \uC911\uC785\uB2C8\uB2E4";
            map["Stackshot is saving a recording\u2026"] = "Stackshot\uC774 \uB179\uD654\uB97C \uC800\uC7A5\uD558\uACE0 \uC788\uC2B5\uB2C8\uB2E4\u2026";
            map["There are unsaved changes in Stackshot"] = "Stackshot\uC5D0 \uC800\uC7A5\uD558\uC9C0 \uC54A\uC740 \uBCC0\uACBD \uC0AC\uD56D\uC774 \uC788\uC2B5\uB2C8\uB2E4";
            map["Could not read text"] = "\uD14D\uC2A4\uD2B8\uB97C \uC77D\uC744 \uC218 \uC5C6\uC74C";
            map["No text found"] = "\uD14D\uC2A4\uD2B8\uB97C \uCC3E\uC744 \uC218 \uC5C6\uC74C";
            map["No text found in the image."] = "\uC774\uBBF8\uC9C0\uC5D0\uC11C \uD14D\uC2A4\uD2B8\uB97C \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
            map["Text copied"] = "\uD14D\uC2A4\uD2B8 \uBCF5\uC0AC\uB428";
            map["Could not copy"] = "\uBCF5\uC0AC\uD560 \uC218 \uC5C6\uC74C";
            map["Another program is using the clipboard. Please try again."] = "\uB2E4\uB978 \uD504\uB85C\uADF8\uB7A8\uC5D0\uC11C \uD074\uB9BD\uBCF4\uB4DC\uB97C \uC0AC\uC6A9 \uC911\uC785\uB2C8\uB2E4. \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694.";
            map["Screen"] = "\uD654\uBA74";
            map["Window"] = "\uCC3D";
            map["Could not save capture"] = "\uCEA1\uCC98\uB97C \uC800\uC7A5\uD560 \uC218 \uC5C6\uC74C";
            map[" The thumbnail remains in the stack: you can copy it or save it to your folder."] = " \uC378\uB124\uC77C\uC5D0\uC11C \uB2E4\uC2DC \uBCF5\uC0AC\uD558\uAC70\uB098 \uC9C0\uC815\uD55C \uD3F4\uB354\uC5D0 \uC800\uC7A5\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            map["Could not save to your folder"] = "\uC9C0\uC815\uD55C \uD3F4\uB354\uC5D0 \uC800\uC7A5\uD560 \uC218 \uC5C6\uC74C";
            map[" The thumbnail remains in the stack: you can click Save again."] = " \uC378\uB124\uC77C\uC5D0\uC11C \uC800\uC7A5\uC744 \uB2E4\uC2DC \uB204\uB974\uC138\uC694.";
            map["Windows or the antivirus blocked it."] = "Windows \uB610\uB294 \uBC14\uC774\uB7EC\uC2A4 \uBC31\uC2E0\uC774 \uCC28\uB2E8\uD588\uC2B5\uB2C8\uB2E4.";
            map["Windows has no application to open this type of file."] = "\uC774 \uD615\uC2DD\uC758 \uD30C\uC77C\uC744 \uC5F4 \uC218 \uC788\uB294 \uC571\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Windows cannot open the file (due to permissions or antivirus)."] = "Windows\uC5D0\uC11C \uD30C\uC77C\uC744 \uC5F4 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. \uAD8C\uD55C\uC774\uB098 \uBC14\uC774\uB7EC\uC2A4 \uBC31\uC2E0 \uC124\uC815\uC744 \uD655\uC778\uD558\uC138\uC694.";
            map["Windows cannot write to {0}. Check folder permissions or if Controlled Folder Access is protecting it."] = "{0}\uC5D0 \uC4F8 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. \uD3F4\uB354 \uAD8C\uD55C\uACFC Windows\uC758 \uC81C\uC5B4\uB41C \uD3F4\uB354 \uC561\uC138\uC2A4 \uC124\uC815\uC744 \uD655\uC778\uD558\uC138\uC694.";
            map["No space left on the disk of {0}. Free up space and try again."] = "{0} \uB4DC\uB77C\uC774\uBE0C\uC5D0 \uACF5\uAC04\uC774 \uBD80\uC871\uD569\uB2C8\uB2E4. \uACF5\uAC04\uC744 \uD655\uBCF4\uD55C \uB4A4 \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694.";
            map["Another program is using the file. Close it and try again."] = "\uB2E4\uB978 \uD504\uB85C\uADF8\uB7A8\uC5D0\uC11C \uD30C\uC77C\uC744 \uC0AC\uC6A9 \uC911\uC785\uB2C8\uB2E4. \uD30C\uC77C\uC744 \uB2EB\uACE0 \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694.";
            map["Cannot find folder {0} (a disconnected or network drive?)."] = "{0} \uD3F4\uB354\uB97C \uCC3E\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. \uB4DC\uB77C\uC774\uBE0C\uB098 \uB124\uD2B8\uC6CC\uD06C \uC5F0\uACB0\uC744 \uD655\uC778\uD558\uC138\uC694.";
            map["The file no longer exists."] = "\uD30C\uC77C\uC774 \uB354 \uC774\uC0C1 \uC874\uC7AC\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
            map["The path is too long for Windows. Choose a folder with a shorter path."] = "Windows\uC5D0\uC11C \uCC98\uB9AC\uD558\uAE30\uC5D0 \uACBD\uB85C\uAC00 \uB108\uBB34 \uAE41\uB2C8\uB2E4. \uACBD\uB85C\uAC00 \uB354 \uC9E7\uC740 \uD3F4\uB354\uB97C \uC120\uD0DD\uD558\uC138\uC694.";
            map["Not enough memory. Close something and try again."] = "\uBA54\uBAA8\uB9AC\uAC00 \uBD80\uC871\uD569\uB2C8\uB2E4. \uB2E4\uB978 \uD504\uB85C\uADF8\uB7A8\uC744 \uB2EB\uACE0 \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694.";
            map["Failed to open the file. Try again; if it recurs, the Stackshot log has the details."] = "\uD30C\uC77C\uC744 \uC5F4\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694. \uBB38\uC81C\uAC00 \uBC18\uBCF5\uB418\uBA74 Stackshot \uB85C\uADF8\uB97C \uD655\uC778\uD558\uC138\uC694.";
            map["Failed to read or write the file. Try again; if it recurs, the Stackshot log has the details."] = "\uD30C\uC77C\uC744 \uC77D\uAC70\uB098 \uC4F0\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694. \uBB38\uC81C\uAC00 \uBC18\uBCF5\uB418\uBA74 Stackshot \uB85C\uADF8\uB97C \uD655\uC778\uD558\uC138\uC694.";
            map["the folder"] = "\uD574\uB2F9 \uD3F4\uB354";
            map["Stackshot closed while recording{0}. The recording is safe"] = "\uB179\uD654 \uC911 Stackshot\uC774 \uC885\uB8CC\uB418\uC5C8\uC2B5\uB2C8\uB2E4{0}. \uB179\uD654 \uD30C\uC77C\uC740";
            map["Stackshot closed during {0} recordings. The recordings are safe"] = "\uB179\uD654 {0}\uAC74\uC774 \uC608\uAE30\uCE58 \uC54A\uAC8C \uC885\uB8CC\uB418\uC5C8\uC2B5\uB2C8\uB2E4. \uB179\uD654 \uD30C\uC77C\uC740";
            map["in the \u201Crecuperado\u201D folder."] = "\u2018recuperado\u2019 \uD3F4\uB354\uC5D0 \uBCF4\uAD00\uB418\uC5B4 \uC788\uC2B5\uB2C8\uB2E4.";
            map["Recording recovered"] = "\uB179\uD654 \uBCF5\uAD6C\uB428";
            map["Could not open folder"] = "\uD3F4\uB354\uB97C \uC5F4 \uC218 \uC5C6\uC74C";
            map["Videos|"] = "\uB3D9\uC601\uC0C1|";
            map["All files"] = "\uBAA8\uB4E0 \uD30C\uC77C";
            map["Could not open"] = "\uC5F4 \uC218 \uC5C6\uC74C";
            map["Open video"] = "\uB3D9\uC601\uC0C1 \uC5F4\uAE30";

            // ShotStack.Delayed.cs
            map["An unexpected error occurred. Please try again."] = "\uC608\uAE30\uCE58 \uC54A\uC740 \uC624\uB958\uAC00 \uBC1C\uC0DD\uD588\uC2B5\uB2C8\uB2E4. \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694.";

            // ShotStack.Focus.cs
            map["Block {0} of {1} done"] = "\uC9D1\uC911 \uC2DC\uAC04 {0}/{1} \uC644\uB8CC";
            map["Break finished"] = "\uD734\uC2DD \uC885\uB8CC";
            map["Long break"] = "\uAE34 \uD734\uC2DD";
            map["Break"] = "\uD734\uC2DD";
            map[" paused"] = " \uC77C\uC2DC \uC815\uC9C0\uB428";
            map["{0} left"] = "{0} \uB0A8\uC74C";
            map["Focus"] = "\uC9D1\uC911";
            map["block {0} of {1}"] = "\uC9D1\uC911 \uC2DC\uAC04 {0}/{1}";
            map["Today: {0} {1}, {2} of focus"] = "\uC624\uB298: {0}{1}, \uC9D1\uC911\uD55C \uC2DC\uAC04 {2}";
            map["block"] = "\uD68C";
            map["blocks"] = "\uD68C";
            map["Focus: block 1 of {0}"] = "\uC9D1\uC911 \uC2DC\uAC04 1/{0}";
            map["Let's focus! {0}. I'll let you know when it's done."] = "\uC9D1\uC911\uD560 \uC2DC\uAC04\uC774\uC5D0\uC694! {0} \uB3D9\uC548 \uD568\uAED8\uD574\uC694. \uB05D\uB098\uBA74 \uC54C\uB824\uB4DC\uB9B4\uAC8C\uC694.";
            map["{0} left."] = "{0} \uB0A8\uC558\uC5B4\uC694.";
            map["Resume"] = "\uC7AC\uAC1C";
            map["Continuing focus"] = "\uC9D1\uC911 \uACC4\uC18D";
            map["Continuing break"] = "\uD734\uC2DD \uACC4\uC18D";
            map["Focus finished"] = "\uC9D1\uC911 \uC885\uB8CC";
            map[". Well done!"] = ". \uC798\uD588\uC5B4\uC694!";
            map["Let's leave it here."] = "\uC624\uB298\uC740 \uC5EC\uAE30\uAE4C\uC9C0 \uD574\uC694.";
            map["Long break of {0}"] = "\uAE34 \uD734\uC2DD {0}";
            map["Break of {0}"] = "\uD734\uC2DD {0}";
            map[": stand up, look away, drink water. I'll let you know when it's done."] = " \uB3D9\uC548 \uC77C\uC5B4\uB098\uC11C \uBA3C \uACF3\uC744 \uBC14\uB77C\uBCF4\uACE0 \uBB3C\uC744 \uB9C8\uC154\uC694. \uB05D\uB098\uBA74 \uC54C\uB824\uB4DC\uB9B4\uAC8C\uC694.";
            map["Block {0} again"] = "\uC9D1\uC911 \uC2DC\uAC04 {0} \uB2E4\uC2DC \uC2DC\uC791";
            map["Focus: block {0} of {1}"] = "\uC9D1\uC911 \uC2DC\uAC04 {0}/{1}";
            map["Block {0} of {1}: "] = "\uC9D1\uC911 \uC2DC\uAC04 {0}/{1}: ";
            map["{0} of focus. Let's go!"] = "{0} \uB3D9\uC548 \uC9D1\uC911\uD574\uC694. \uC2DC\uC791!";
            map["{0} more minutes. I'll let you know when it's done."] = "{0}\uBD84 \uB354 \uC9C4\uD589\uD574\uC694. \uB05D\uB098\uBA74 \uC54C\uB824\uB4DC\uB9B4\uAC8C\uC694.";
            map["You were away {0} min and I stopped the clock: {1} left."] = "{0}\uBD84 \uB3D9\uC548 \uC790\uB9AC\uB97C \uBE44\uC6CC \uD0C0\uC774\uBA38\uB97C \uBA48\uCDC4\uC5B4\uC694. {1} \uB0A8\uC558\uC5B4\uC694.";
            map["Focus paused"] = "\uC9D1\uC911 \uC77C\uC2DC \uC815\uC9C0";
            map["Continue"] = "\uACC4\uC18D";

            // ShotStack.Health.cs
            map["Have a Pixel Watch or a Fitbit? If you connect it, I can accompany you with your steps, activity and sleep. Data goes from Google directly to your PC."] = "Pixel Watch\uB098 Fitbit\uC744 \uC5F0\uACB0\uD558\uBA74 \uAC78\uC74C \uC218\uC640 \uD65C\uB3D9, \uC218\uBA74\uC744 \uD568\uAED8 \uD655\uC778\uD560 \uC218 \uC788\uC5B4\uC694. Google\uC5D0\uC11C \uC774 PC\uB85C \uC9C1\uC811 \uB370\uC774\uD130\uB97C \uAC00\uC838\uC640\uC694.";
            map["Connect your smartwatch?"] = "\uC2A4\uB9C8\uD2B8\uC6CC\uCE58\uB97C \uC5F0\uACB0\uD560\uAE4C\uC694?";
            map["Connect"] = "\uC5F0\uACB0";
            map["{0} of {1} glasses of water"] = "\uBB3C {0}/{1}\uC794";
            map[" steps"] = "\uAC78\uC74C";
            map["Today: "] = "\uC624\uB298: ";
            map["How was your day? "] = "\uC624\uB298 \uD558\uB8E8\uB294 \uC5B4\uB560\uB098\uC694? ";
            map["Your day"] = "\uD558\uB8E8 \uB3CC\uC544\uBCF4\uAE30";
            map["What a day! You're already at {0} captures."] = "\uC624\uB298 \uBC8C\uC368 {0}\uBC88\uC774\uB098 \uCEA1\uCC98\uD588\uC5B4\uC694!";
            map["I could not read your watch: "] = "\uC6CC\uCE58 \uB370\uC774\uD130\uB97C \uAC00\uC838\uC624\uC9C0 \uBABB\uD588\uC5B4\uC694: ";
            map["I have not read your watch yet today."] = "\uC624\uB298\uC740 \uC544\uC9C1 \uC6CC\uCE58 \uB370\uC774\uD130\uB97C \uAC00\uC838\uC624\uC9C0 \uC54A\uC558\uC5B4\uC694.";
            map["Google has no data for today yet. Open your watch's app on your phone so it syncs, and I will check again."] = "Google\uC5D0 \uC544\uC9C1 \uC624\uB298 \uB370\uC774\uD130\uAC00 \uC5C6\uC5B4\uC694. \uD734\uB300\uC804\uD654\uC5D0\uC11C \uC6CC\uCE58 \uC571\uC744 \uC5F4\uC5B4 \uB3D9\uAE30\uD654\uD558\uBA74 \uB2E4\uC2DC \uD655\uC778\uD560\uAC8C\uC694.";
            map["Watch could not connect"] = "\uC6CC\uCE58\uB97C \uC5F0\uACB0\uD560 \uC218 \uC5C6\uC74C";
            map["Watch connected"] = "\uC6CC\uCE58 \uC5F0\uACB0\uB428";
            map["Stackshot will read your steps, activity and sleep every half hour. Data goes from Google directly to your PC."] = "30\uBD84\uB9C8\uB2E4 \uAC78\uC74C \uC218, \uD65C\uB3D9, \uC218\uBA74 \uB370\uC774\uD130\uB97C Google\uC5D0\uC11C \uC774 PC\uB85C \uC9C1\uC811 \uAC00\uC838\uC635\uB2C8\uB2E4.";
            map["You slept {0} last night. Today I will suggest the eye and movement breaks a little earlier."] = "\uC5B4\uC82F\uBC24 {0} \uC7A4\uB124\uC694. \uC624\uB298\uC740 \uB208\uC744 \uC26C\uAC8C \uD558\uAC70\uB098 \uC6C0\uC9C1\uC77C \uC2DC\uAC04\uC744 \uC870\uAE08 \uC77C\uCC0D \uC54C\uB824\uB4DC\uB9B4\uAC8C\uC694.";
            map["You slept {0}. A good day to focus!"] = "{0} \uC7A4\uB124\uC694. \uC9D1\uC911\uD558\uAE30 \uC88B\uC740 \uB0A0\uC774\uC5D0\uC694!";
            map["You slept {0} last night. Let's go!"] = "\uC5B4\uC82F\uBC24 {0} \uC7A4\uB124\uC694. \uC624\uB298\uB3C4 \uD798\uB0B4\uC694!";
            map["{0} steps! You reached today's goal."] = "{0}\uAC78\uC74C! \uC624\uB298 \uBAA9\uD45C\uB97C \uB2EC\uC131\uD588\uC5B4\uC694.";
            map["Nice walk! {0} more steps in {1} min."] = "\uC798 \uAC78\uC5C8\uC5B4\uC694! {1}\uBD84 \uB3D9\uC548 {0}\uAC78\uC74C \uB354 \uAC78\uC5C8\uB124\uC694.";
            map["Halfway there! {0} of {1} steps."] = "\uC808\uBC18\uC774\uB098 \uC654\uC5B4\uC694! \uBAA9\uD45C {1}\uAC78\uC74C \uC911 {0}\uAC78\uC74C\uC744 \uAC78\uC5C8\uC5B4\uC694.";
            map["{0} steps so far this morning. How about getting up for a glass of water and a short walk?"] = "\uC624\uB298 \uC544\uCE68 \uBC8C\uC368 {0}\uAC78\uC74C\uC774\uC5D0\uC694. \uBB3C \uD55C \uC794 \uB9C8\uC2DC\uACE0 \uC7A0\uAE50 \uAC78\uC5B4 \uBCFC\uAE4C\uC694?";
            map["A walk after lunch feels great: ten minutes out and you come back with a clear head."] = "\uC810\uC2EC\uC744 \uBA39\uACE0 10\uBD84\uCBE4 \uAC78\uC5B4 \uBCF4\uC138\uC694. \uD55C\uACB0 \uAC1C\uC6B4\uD574\uC9C8 \uAC70\uC608\uC694.";
            map["An active hour today! Your body thanks you."] = "\uC624\uB298\uC740 \uD55C \uC2DC\uAC04\uC774\uB098 \uD65C\uBC1C\uD558\uAC8C \uC6C0\uC9C1\uC600\uC5B4\uC694!";
            map["{0} active minutes today already. Way to go!"] = "\uC624\uB298 \uBC8C\uC368 {0}\uBD84\uC774\uB098 \uD65C\uB3D9\uD588\uC5B4\uC694. \uC798\uD588\uC5B4\uC694!";
            map["{0} steps to your goal: a walk of about {1} minutes and you're there."] = "\uBAA9\uD45C\uAE4C\uC9C0 {0}\uAC78\uC74C \uB0A8\uC558\uC5B4\uC694. {1}\uBD84 \uC815\uB3C4 \uAC78\uC73C\uBA74 \uB3FC\uC694.";
            map["You slept little last night: try to go to bed a bit earlier today. You'll feel it tomorrow!"] = "\uC5B4\uC82F\uBC24\uC5D4 \uC7A0\uC774 \uBD80\uC871\uD588\uB124\uC694. \uC624\uB298\uC740 \uC870\uAE08 \uC77C\uCC0D \uC7A0\uC790\uB9AC\uC5D0 \uB4E4\uC5B4 \uBCF4\uC138\uC694.";

            // ShotStack.Links.cs
            map["Type a web address or the path of a folder or file."] = "\uC6F9 \uC8FC\uC18C \uB610\uB294 \uD3F4\uB354\u00B7\uD30C\uC77C \uACBD\uB85C\uB97C \uC785\uB825\uD558\uC138\uC694.";
            map["I cannot find that folder or file."] = "\uD3F4\uB354 \uB610\uB294 \uD30C\uC77C\uC744 \uCC3E\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Type a web address (like intranet.company.com) or a full path (like C:\\Projects)."] = "\uC6F9 \uC8FC\uC18C(\uC608: intranet.company.com) \uB610\uB294 \uC804\uCCB4 \uACBD\uB85C(\uC608: C:\\Projects)\uB97C \uC785\uB825\uD558\uC138\uC694.";
            map["That address or path cannot be used."] = "\uC0AC\uC6A9\uD560 \uC218 \uC5C6\uB294 \uC8FC\uC18C \uB610\uB294 \uACBD\uB85C\uC785\uB2C8\uB2E4.";
            map["You already have it in your quick links: \u201C{0}\u201D."] = "\uBC14\uB85C\uAC00\uAE30\uC5D0 \uC774\uBBF8 \u2018{0}\u2019\uC774(\uAC00) \uC788\uC2B5\uB2C8\uB2E4.";
            map["You already have {0} links: remove one to add another."] = "\uBC14\uB85C\uAC00\uAE30\uAC00 \uC774\uBBF8 {0}\uAC1C \uC788\uC2B5\uB2C8\uB2E4. \uD558\uB098\uB97C \uC0AD\uC81C\uD55C \uB4A4 \uCD94\uAC00\uD558\uC138\uC694.";
            map["Could not open browser."] = "\uBE0C\uB77C\uC6B0\uC800\uB97C \uC5F4 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Could not open \u201C{0}\u201D: it may no longer exist."] = "\u2018{0}\u2019\uC744(\uB97C) \uC5F4 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. \uC0AD\uC81C\uB418\uC5C8\uAC70\uB098 \uC774\uB3D9\uB418\uC5C8\uC744 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";

            // ShotStack.Merge.cs
            map["Images only"] = "\uC774\uBBF8\uC9C0\uB9CC \uAC00\uB2A5";
            map["Could not combine"] = "\uD569\uCE60 \uC218 \uC5C6\uC74C";
            map["The captures are too large or could not be read. Please try again."] = "\uCEA1\uCC98\uAC00 \uB108\uBB34 \uD06C\uAC70\uB098 \uC77D\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694.";
            map["Combined"] = "\uD569\uCE58\uAE30 \uC644\uB8CC";

            // ShotStack.News.cs
            map["Stackshot {0} is here"] = "Stackshot {0} \uC5C5\uB370\uC774\uD2B8";
            map["Highlights include: \u201C{0}\u201D. "] = "\uC8FC\uC694 \uBCC0\uACBD \uC0AC\uD56D: \u2018{0}\u2019. ";
            map["Would you like to see what's new?"] = "\uC0C8\uB85C\uC6B4 \uAE30\uB2A5\uC744 \uBCFC\uAE4C\uC694?";
            map["View what's new"] = "\uC0C8\uB85C\uC6B4 \uAE30\uB2A5 \uBCF4\uAE30";

            // ShotStack.Reports.cs
            map["Updates on your report"] = "\uC2E0\uACE0\uC5D0 \uC0C8 \uC18C\uC2DD\uC774 \uC788\uC2B5\uB2C8\uB2E4";
            map["Updates on {0} of your reports"] = "\uC2E0\uACE0 {0}\uAC74\uC5D0 \uC0C8 \uC18C\uC2DD\uC774 \uC788\uC2B5\uB2C8\uB2E4";
            map["View my reports"] = "\uB0B4 \uC2E0\uACE0 \uBCF4\uAE30";

            // ShotStack.Saving.cs
            map["Saving GIF\u2026"] = "GIF \uC800\uC7A5 \uC911\u2026";
            map["Saving video\u2026"] = "\uB3D9\uC601\uC0C1 \uC800\uC7A5 \uC911\u2026";
            map["{0}%  \u00B7  it will appear here"] = "{0}%  \u00B7  \uC800\uC7A5\uB418\uBA74 \uC5EC\uAE30\uC5D0 \uD45C\uC2DC\uB429\uB2C8\uB2E4";
            map["It will appear here in a moment"] = "\uC7A0\uC2DC \uD6C4 \uC5EC\uAE30\uC5D0 \uD45C\uC2DC\uB429\uB2C8\uB2E4";

            // ShotStack.Sensitive.cs
            map["Checking data\u2026"] = "\uB370\uC774\uD130 \uAC80\uC0AC \uC911\u2026";
            map["The capture was discarded because it could not be checked for sensitive data."] = "\uBBFC\uAC10\uC815\uBCF4\uB97C \uAC80\uC0AC\uD560 \uC218 \uC5C6\uC5B4 \uCEA1\uCC98\uB97C \uC0AD\uC81C\uD588\uC2B5\uB2C8\uB2E4.";
            map["The capture could not be checked"] = "\uCEA1\uCC98\uB97C \uAC80\uC0AC\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4";
            map["Its sensitive data could not be covered, so it was discarded: it is never shown or saved unchecked."] = "\uBBFC\uAC10 \uC815\uBCF4\uB97C \uAC00\uB9AC\uC9C0 \uBABB\uD574 \uCEA1\uCC98\uB97C \uC0AD\uC81C\uD588\uC2B5\uB2C8\uB2E4. \uAC80\uC0AC\uD558\uC9C0 \uC54A\uC740 \uCEA1\uCC98\uB294 \uD45C\uC2DC\uD558\uAC70\uB098 \uC800\uC7A5\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
            map["Captures discarded: no OCR"] = "OCR\uC774 \uC5C6\uC5B4 \uCEA1\uCC98\uB97C \uC0AD\uC81C\uD569\uB2C8\uB2E4";
            map["Your organization requires \u201C{0}\u201D, but this Windows has no OCR installed, so every capture is discarded rather than shown unchecked. Install an OCR language (Windows Settings > Apps > Optional features) or contact IT."] = "\uC870\uC9C1\uC5D0\uC11C \u2018{0}\u2019 \uC0AC\uC6A9\uC744 \uC694\uAD6C\uD558\uC9C0\uB9CC \uC774 Windows\uC5D0\uB294 OCR\uC774 \uC124\uCE58\uB418\uC5B4 \uC788\uC9C0 \uC54A\uC544, \uAC80\uC0AC\uD558\uC9C0 \uC54A\uC740 \uCEA1\uCC98\uAC00 \uD45C\uC2DC\uB418\uC9C0 \uC54A\uB3C4\uB85D \uBAA8\uB4E0 \uCEA1\uCC98\uB97C \uC0AD\uC81C\uD569\uB2C8\uB2E4. Windows \uC124\uC815 > \uC571 > \uC120\uD0DD\uC801 \uAE30\uB2A5\uC5D0\uC11C OCR \uC5B8\uC5B4\uB97C \uC124\uCE58\uD558\uAC70\uB098 IT \uBD80\uC11C\uC5D0 \uBB38\uC758\uD558\uC138\uC694.";
            map["With \u201C{0}\u201D on, every capture is checked before it is shown, and this Windows has no OCR installed, so captures are discarded rather than shown unchecked. Install an OCR language (Windows Settings > Apps > Optional features) or turn the option off."] = "\u2018{0}\u2019 \uC635\uC158\uC774 \uCF1C\uC838 \uC788\uC73C\uBA74 \uBAA8\uB4E0 \uCEA1\uCC98\uB97C \uD45C\uC2DC\uD558\uAE30 \uC804\uC5D0 \uAC80\uC0AC\uD558\uB294\uB370, \uC774 Windows\uC5D0\uB294 OCR\uC774 \uC124\uCE58\uB418\uC5B4 \uC788\uC9C0 \uC54A\uC544 \uAC80\uC0AC\uD558\uC9C0 \uC54A\uC740 \uCEA1\uCC98\uAC00 \uD45C\uC2DC\uB418\uC9C0 \uC54A\uB3C4\uB85D \uCEA1\uCC98\uB97C \uC0AD\uC81C\uD569\uB2C8\uB2E4. Windows \uC124\uC815 > \uC571 > \uC120\uD0DD\uC801 \uAE30\uB2A5\uC5D0\uC11C OCR \uC5B8\uC5B4\uB97C \uC124\uCE58\uD558\uAC70\uB098 \uC774 \uC635\uC158\uC744 \uB044\uC138\uC694.";
            map["Pasted"] = "\uBD99\uC5EC\uB123\uC74C";
            map["Okay, I won't."] = "\uC54C\uACA0\uC5B4, \uC548 \uD560\uAC8C.";
            map["tomorrow "] = "\uB0B4\uC77C ";
            map["Reinstall Stackshot {0} over the installed copy?"] = "\uC124\uCE58\uB41C Stackshot \uC704\uC5D0 {0} \uBC84\uC804\uC744 \uB2E4\uC2DC \uC124\uCE58\uD560\uAE4C\uC694?";
            map["you slept {0} last night"] = "\uC5B4\uC82F\uBC24 {0} \uC218\uBA74";
            map["you have {0} min of moderate or vigorous activity"] = "\uC911\uAC15\uB3C4 \uC774\uC0C1 \uD65C\uB3D9 {0}\uBD84";
            map[" {0} to go: about {1} min of walking."] = " {0}\uAC78\uC74C \uB0A8\uC558\uC5B4\uC694. \uAC78\uC73C\uBA74 \uC57D {1}\uBD84 \uAC78\uB824\uC694.";

            // ShotStack.Toast.cs
            map["Open folder"] = "\uD3F4\uB354 \uC5F4\uAE30";
            map["New XML"] = "\uC0C8 XML";
            map[". The viewer has unsaved changes: open it from the tray when finished."] = ". \uBDF0\uC5B4\uC5D0 \uC800\uC7A5\uD558\uC9C0 \uC54A\uC740 \uBCC0\uACBD \uC0AC\uD56D\uC774 \uC788\uC2B5\uB2C8\uB2E4. \uB9C8\uCE5C \uB4A4 \uD2B8\uB808\uC774\uC5D0\uC11C \uC5EC\uC138\uC694.";
            map["picking an area"] = "\uC601\uC5ED \uC120\uD0DD \uC911";

            // UpdateNotice.cs
            map["As soon as the recording finishes saving, Stackshot will close and reopen."] = "\uB179\uD654 \uC800\uC7A5\uC774 \uB05D\uB098\uBA74 Stackshot\uC744 \uB2E4\uC2DC \uC2DC\uC791\uD569\uB2C8\uB2E4.";
            map["Stackshot will close and reopen in a few seconds."] = "\uC7A0\uC2DC \uD6C4 Stackshot\uC744 \uB2E4\uC2DC \uC2DC\uC791\uD569\uB2C8\uB2E4.";
            map["SHA-256 hash and digital signature."] = "SHA-256 \uD574\uC2DC \uBC0F \uB514\uC9C0\uD138 \uC11C\uBA85";
            map["Connecting to GitHub\u2026"] = "GitHub\uC5D0 \uC5F0\uACB0 \uC911\u2026";
            map["signature checked afterwards"] = "\uB2E4\uC6B4\uB85C\uB4DC \uD6C4 \uC11C\uBA85 \uAC80\uC99D";
            map["What's new: {0}.\n"] = "\uC0C8\uB85C\uC6B4 \uAE30\uB2A5: {0}.\n";
            map["Already downloaded and verified. Upon installing, Stackshot will close and reopen."] = "\uB2E4\uC6B4\uB85C\uB4DC\uC640 \uAC80\uC99D\uC774 \uC644\uB8CC\uB410\uC2B5\uB2C8\uB2E4. \uC124\uCE58\uD558\uBA74 Stackshot\uC744 \uB2E4\uC2DC \uC2DC\uC791\uD569\uB2C8\uB2E4.";
            map["Do you want to download it? Its signature will be verified and it installs in seconds."] = "\uB2E4\uC6B4\uB85C\uB4DC\uD560\uAE4C\uC694? \uC11C\uBA85\uC744 \uD655\uC778\uD55C \uB4A4 \uC124\uCE58\uD569\uB2C8\uB2E4.";
            map["On this machine updates are managed by IT. Would you like to view the new version?"] = "\uC774 PC\uC758 \uC5C5\uB370\uC774\uD2B8\uB294 IT \uBD80\uC11C\uC5D0\uC11C \uAD00\uB9AC\uD569\uB2C8\uB2E4. \uC0C8 \uBC84\uC804\uC744 \uD655\uC778\uD560\uAE4C\uC694?";
            map["Do you want to download it? This version is downloaded from its GitHub page."] = "\uB2E4\uC6B4\uB85C\uB4DC\uD560\uAE4C\uC694? \uC774 \uBC84\uC804\uC740 GitHub\uC5D0\uC11C \uB2E4\uC6B4\uB85C\uB4DC\uD569\uB2C8\uB2E4.";
            map["See what's new \u203A"] = "\uC0C8\uB85C\uC6B4 \uAE30\uB2A5 \uBCF4\uAE30 \u203A";
            map["Downloading Stackshot {0}"] = "Stackshot {0} \uB2E4\uC6B4\uB85C\uB4DC \uC911";
            map["Verifying download\u2026"] = "\uB2E4\uC6B4\uB85C\uB4DC \uAC80\uC99D \uC911\u2026";
            map["Installing Stackshot {0}"] = "Stackshot {0} \uC124\uCE58 \uC911";
            map["Hide"] = "\uC228\uAE30\uAE30";
            map["Could not update to {0}"] = "{0}(\uC73C)\uB85C \uC5C5\uB370\uC774\uD2B8\uD560 \uC218 \uC5C6\uC74C";
            map["Stackshot {0} ready to install"] = "Stackshot {0} \uC124\uCE58 \uC900\uBE44 \uC644\uB8CC";
            map["Stackshot {0} available"] = "Stackshot {0} \uC0AC\uC6A9 \uAC00\uB2A5";

            // Updater.cs
            map["Installing {0} did not complete. Try again or download it from GitHub."] = "{0} \uC124\uCE58\uAC00 \uC644\uB8CC\uB418\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4. \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uAC70\uB098 GitHub\uC5D0\uC11C \uB2E4\uC6B4\uB85C\uB4DC\uD558\uC138\uC694.";
            map["download the update"] = "\uC5C5\uB370\uC774\uD2B8 \uB2E4\uC6B4\uB85C\uB4DC";
            map["check GitHub"] = "GitHub \uD655\uC778";
            map["Could not {0} ({1})."] = "{0} \uC2E4\uD328({1}).";
            map["Could not {0}. "] = "{0} \uC2E4\uD328. ";
            map["Could not {0}. Will try again later."] = "{0} \uC2E4\uD328. \uB098\uC911\uC5D0 \uB2E4\uC2DC \uC2DC\uB3C4\uD569\uB2C8\uB2E4.";
            map["GitHub rate limit reached; will try again later."] = "GitHub \uC694\uCCAD \uD55C\uB3C4\uC5D0 \uB3C4\uB2EC\uD588\uC2B5\uB2C8\uB2E4. \uB098\uC911\uC5D0 \uB2E4\uC2DC \uC2DC\uB3C4\uD569\uB2C8\uB2E4.";
            map["The network proxy does not let Stackshot {0}."] = "\uB124\uD2B8\uC6CC\uD06C \uD504\uB85D\uC2DC\uAC00 \uB2E4\uC74C \uC791\uC5C5\uC744 \uCC28\uB2E8\uD588\uC2B5\uB2C8\uB2E4: {0}.";
            map["GitHub is not responding right now; will try again later."] = "\uD604\uC7AC GitHub\uAC00 \uC751\uB2F5\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4. \uB098\uC911\uC5D0 \uB2E4\uC2DC \uC2DC\uB3C4\uD569\uB2C8\uB2E4.";
            map["Could not {0} (error {1})."] = "{0} \uC2E4\uD328(\uC624\uB958 {1}).";
            map["Connection lost during download."] = "\uB2E4\uC6B4\uB85C\uB4DC \uC911 \uC5F0\uACB0\uC774 \uB04A\uACBC\uC2B5\uB2C8\uB2E4.";
            map["No connection to GitHub. Will try again when network is restored."] = "GitHub\uC5D0 \uC5F0\uACB0\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. \uB124\uD2B8\uC6CC\uD06C\uAC00 \uBCF5\uAD6C\uB418\uBA74 \uB2E4\uC2DC \uC2DC\uB3C4\uD569\uB2C8\uB2E4.";
            map["Could not open a secure connection to GitHub."] = "GitHub \uBCF4\uC548 \uC5F0\uACB0\uC744 \uC124\uC815\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["The update's signature is not valid; it was discarded."] = "\uC5C5\uB370\uC774\uD2B8 \uC11C\uBA85\uC774 \uC720\uD6A8\uD558\uC9C0 \uC54A\uC544 \uD30C\uC77C\uC744 \uC0AD\uC81C\uD588\uC2B5\uB2C8\uB2E4.";
            map["too large"] = "\uD06C\uAE30 \uCD08\uACFC";
            map["The downloaded version does not match the published one; it was discarded."] = "\uB2E4\uC6B4\uB85C\uB4DC\uD55C \uBC84\uC804\uC774 \uAC8C\uC2DC\uB41C \uBC84\uC804\uACFC \uB2EC\uB77C \uD30C\uC77C\uC744 \uC0AD\uC81C\uD588\uC2B5\uB2C8\uB2E4.";
            map["The download is incomplete; it was discarded."] = "\uB2E4\uC6B4\uB85C\uB4DC\uAC00 \uC644\uB8CC\uB418\uC9C0 \uC54A\uC544 \uD30C\uC77C\uC744 \uC0AD\uC81C\uD588\uC2B5\uB2C8\uB2E4.";
            map["Could not start update. "] = "\uC5C5\uB370\uC774\uD2B8\uB97C \uC2DC\uC791\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. ";
            map["Installation did not complete{0}. Try again or download from GitHub."] = "\uC124\uCE58\uB97C \uC644\uB8CC\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4{0}. \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uAC70\uB098 GitHub\uC5D0\uC11C \uB2E4\uC6B4\uB85C\uB4DC\uD558\uC138\uC694.";
            map["code "] = "\uCF54\uB4DC ";
            map["The download does not match the published SHA-256 checksum; it was discarded."] = "\uB2E4\uC6B4\uB85C\uB4DC\uD55C \uD30C\uC77C\uC758 SHA-256 \uCCB4\uD06C\uC12C\uC774 \uAC8C\uC2DC\uB41C \uAC12\uACFC \uB2EC\uB77C \uD30C\uC77C\uC744 \uC0AD\uC81C\uD588\uC2B5\uB2C8\uB2E4.";
            map["Could not open browser"] = "\uBE0C\uB77C\uC6B0\uC800\uB97C \uC5F4 \uC218 \uC5C6\uC74C";

            // Design/AccessForms.cs
            map["Press"] = "\uB204\uB974\uAE30";

            // Design/Dialogs.cs
            map["OK"] = "\uD655\uC778";
            map["Yes"] = "\uC608";

            // Design/Sheet.cs
            map["Minimize"] = "\uCD5C\uC18C\uD654";

            // Common actions and labels
            map["Close"] = "\uB2EB\uAE30";
            map["Quit"] = "\uC885\uB8CC";
            map["Settings"] = "\uC124\uC815";
            map["Language"] = "\uC5B8\uC5B4";
            map["Auto"] = "\uC790\uB3D9";
            map["Markdown document"] = "Markdown \uBB38\uC11C";
            map["XML document"] = "XML \uBB38\uC11C";
            map["MKV video"] = "MKV \uB3D9\uC601\uC0C1";
            map["Red"] = "\uBE68\uAC15";
            map["Yellow"] = "\uB178\uB791";
            map["Green"] = "\uCD08\uB85D";
            map["Blue"] = "\uD30C\uB791";
            map["White"] = "\uD770\uC0C9";
            map["Scroll"] = "\uC2A4\uD06C\uB864";
            map["Pin to screen"] = "\uD654\uBA74\uC5D0 \uACE0\uC815";
            map["Always on top"] = "\uD56D\uC0C1 \uC704\uC5D0 \uD45C\uC2DC";
            map["Thumbnail"] = "\uC378\uB124\uC77C";
            map["Scrolling capture"] = "\uC2A4\uD06C\uB864 \uCEA1\uCC98";
            map["Opacity"] = "\uBD88\uD22C\uBA85\uB3C4";
            map["{0} older"] = "\uC774\uC804 {0}\uAC1C";
            map["{0} newer"] = "\uCD5C\uADFC {0}\uAC1C";
            map["{0} glass of water"] = "\uBB3C {0}\uC794";
            map["{0} glasses of water"] = "\uBB3C {0}\uC794";
            map["{0} eye rest"] = "\uB208 \uD734\uC2DD {0}\uD68C";
            map["{0} eye rests"] = "\uB208 \uD734\uC2DD {0}\uD68C";
            map["{0} movement break"] = "\uC2A4\uD2B8\uB808\uCE6D {0}\uD68C";
            map["{0} movement breaks"] = "\uC2A4\uD2B8\uB808\uCE6D {0}\uD68C";
            map["{0} minute breathing"] = "\uD638\uD761 \uC6B4\uB3D9 {0}\uBD84";
            map["{0} minutes breathing"] = "\uD638\uD761 \uC6B4\uB3D9 {0}\uBD84";
            map["{0} focus block ({1})"] = "\uC9D1\uC911 {0}\uD68C ({1})";
            map["{0} focus blocks ({1})"] = "\uC9D1\uC911 {0}\uD68C ({1})";
            map["{0} capture"] = "\uCEA1\uCC98 {0}\uD68C";
            map["{0} captures"] = "\uCEA1\uCC98 {0}\uD68C";
            map["PDF exported: {0} ({1} page)."] = "PDF \uB0B4\uBCF4\uB0B4\uAE30 \uC644\uB8CC: {0} ({1}\uCABD).";
            map["PDF exported: {0} ({1} pages)."] = "PDF \uB0B4\uBCF4\uB0B4\uAE30 \uC644\uB8CC: {0} ({1}\uCABD).";
            map["{0} change"] = "\uBCC0\uACBD {0}\uAC1C";
            map["{0} changes"] = "\uBCC0\uACBD {0}\uAC1C";
            map["{0} added"] = "\uCD94\uAC00 {0}\uAC1C";
            map["{0} removed"] = "\uC0AD\uC81C {0}\uAC1C";
            map["{0} difference"] = "\uCC28\uC774 {0}\uAC1C";
            map["{0} differences"] = "\uCC28\uC774 {0}\uAC1C";
            map["You've already walked {0} steps today: goal reached!"] = "\uC624\uB298 {0}\uAC78\uC74C\uC744 \uAC78\uC5B4 \uBAA9\uD45C\uB97C \uB2EC\uC131\uD588\uC5B4\uC694!";
            map["You've walked {0} steps today, {1}% of your {2} goal."] = "\uC624\uB298 {0}\uAC78\uC74C\uC744 \uAC78\uC5C8\uC5B4\uC694. \uBAA9\uD45C {2}\uAC78\uC74C\uC758 {1}%\uC608\uC694.";
            map["You slept {0} last night."] = "\uC5B4\uC82F\uBC24\uC5D0\uB294 {0} \uC7A4\uC5B4\uC694.";
            map["You have {0} min of moderate or vigorous activity."] = "\uC624\uB298 \uC911\uAC15\uB3C4 \uC774\uC0C1\uC73C\uB85C \uD65C\uB3D9\uD55C \uC2DC\uAC04\uC740 {0}\uBD84\uC774\uC5D0\uC694.";
            map["{0} to go: about {1} min of walking."] = "{0}\uAC78\uC74C \uB0A8\uC558\uC5B4\uC694. {1}\uBD84 \uC815\uB3C4 \uB354 \uAC78\uC73C\uBA74 \uB3FC\uC694.";
            map["In the last {0} you have only taken {1} steps. Shall we stretch our legs for a couple of minutes?"] = "\uC9C0\uB09C {0} \uB3D9\uC548 {1}\uAC78\uC74C \uAC78\uC5C8\uC5B4\uC694. \uC7A0\uAE50 \uC77C\uC5B4\uB098 \uB2E4\uB9AC\uB97C \uD480\uC5B4 \uBCFC\uAE4C\uC694?";
            map["hour and a half"] = "1\uC2DC\uAC04 \uBC18";
            map["hour"] = "1\uC2DC\uAC04";
            map["{0} steps so far, {1}% of today's goal."] = "\uC9C0\uAE08\uAE4C\uC9C0 {0}\uAC78\uC74C \uAC78\uC5C8\uC5B4\uC694. \uC624\uB298 \uBAA9\uD45C\uC758 {1}%\uC608\uC694.";
        }
    }
}
