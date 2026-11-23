// Stackshot - Korean translations for the image editor and video player.
// MIT License - https://github.com/rubenitx/stackshot
using System.Collections.Generic;

namespace Stackshot
{
    public static partial class I18n
    {
        static void RegisterKoreanEditor(Dictionary<string, string> map)
        {
            map["File format"] = "\uD30C\uC77C \uD615\uC2DD";
            // BgPanel.Access
            map["Backdrop"] = "\uBC30\uACBD";
            map["No backdrop"] = "\uBC30\uACBD \uC5C6\uC74C";
            map["Alignment: "] = "\uC815\uB82C: ";
            map["Aspect ratio "] = "\uD654\uBA74 \uBE44\uC728 ";
            map["Select"] = "\uC120\uD0DD";
            map["Toggle"] = "\uC804\uD658";

            // BgPanel
            map["Saved styles"] = "\uC800\uC7A5\uD55C \uC2A4\uD0C0\uC77C";
            map["Save this style"] = "\uD604\uC7AC \uC2A4\uD0C0\uC77C \uC800\uC7A5";
            map["Gradients"] = "\uADF8\uB77C\uB370\uC774\uC158";
            map["Show less"] = "\uAC04\uB2E8\uD788 \uBCF4\uAE30";
            map["Show more"] = "\uB354 \uBCF4\uAE30";
            map["Wallpapers"] = "\uBC30\uACBD \uC774\uBBF8\uC9C0";
            map["Add an image\u2026"] = "\uC774\uBBF8\uC9C0 \uCD94\uAC00\u2026";
            map["Blur"] = "\uD750\uB9BC";
            map["Solid color"] = "\uB2E8\uC0C9";
            map["Other color\u2026"] = "\uB2E4\uB978 \uC0C9\uC0C1\u2026";
            map["Auto-balance"] = "\uC790\uB3D9 \uADE0\uD615";
            map["Alignment"] = "\uC815\uB82C";
            map["Aspect ratio"] = "\uD654\uBA74 \uBE44\uC728";
            map["Saved styles\u2026"] = "\uC800\uC7A5\uD55C \uC2A4\uD0C0\uC77C\u2026";
            map["Saved styles ({0})"] = "\uC800\uC7A5\uD55C \uC2A4\uD0C0\uC77C({0})";
            map["Choose an image to use as background"] = "\uBC30\uACBD\uC73C\uB85C \uC0AC\uC6A9\uD560 \uC774\uBBF8\uC9C0 \uC120\uD0DD";
            map["Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*"] = "\uC774\uBBF8\uC9C0|*.png;*.jpg;*.jpeg;*.bmp;*.gif|\uBAA8\uB4E0 \uD30C\uC77C|*.*";
            map["Could not open that image."] = "\uC774\uBBF8\uC9C0\uB97C \uC5F4\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
            map["Remove this background"] = "\uC774 \uBC30\uACBD \uC81C\uAC70";
            map["Black"] = "\uAC80\uC815";
            map["Orange"] = "\uC8FC\uD669";
            map["Purple"] = "\uBCF4\uB77C";
            map["Pink"] = "\uBD84\uD64D";
            map["Dark gray"] = "\uC9C4\uD68C\uC0C9";
            map["Light gray"] = "\uC5F0\uD68C\uC0C9";
            map["Soft red"] = "\uC5F0\uD55C \uBE68\uAC15";
            map["Soft orange"] = "\uC5F0\uD55C \uC8FC\uD669";
            map["Soft yellow"] = "\uC5F0\uD55C \uB178\uB791";
            map["Soft green"] = "\uC5F0\uD55C \uCD08\uB85D";
            map["Soft blue"] = "\uC5F0\uD55C \uD30C\uB791";
            map["Soft purple"] = "\uC5F0\uD55C \uBCF4\uB77C";
            map["Blurred"] = "\uD750\uB9BC";
            map["Blurred & dark"] = "\uC5B4\uB461\uAC8C \uD750\uB9BC";
            map["Blurred & light"] = "\uBC1D\uAC8C \uD750\uB9BC";
            map["Top left"] = "\uC67C\uCABD \uC704";
            map["Top"] = "\uC704";
            map["Top right"] = "\uC624\uB978\uCABD \uC704";
            map["Left"] = "\uC67C\uCABD";
            map["Centered"] = "\uAC00\uC6B4\uB370";
            map["Right"] = "\uC624\uB978\uCABD";
            map["Bottom left"] = "\uC67C\uCABD \uC544\uB798";
            map["Bottom"] = "\uC544\uB798";
            map["Bottom right"] = "\uC624\uB978\uCABD \uC544\uB798";
            map["Padding"] = "\uC5EC\uBC31";
            map["Corners"] = "\uBAA8\uC11C\uB9AC";
            map["Shadow"] = "\uADF8\uB9BC\uC790";
            map["Border inset"] = "\uC548\uCABD \uC5EC\uBC31";

            // BgPanel.Styles
            map[" \u00B7 padding "] = " \u00B7 \uC5EC\uBC31 ";
            map[", border inset"] = ", \uD14C\uB450\uB9AC \uC548\uCABD \uC5EC\uBC31";
            map[", no shadow"] = ", \uADF8\uB9BC\uC790 \uC5C6\uC74C";
            map["This style is already saved"] = "\uC774\uBBF8 \uC800\uC7A5\uD55C \uC2A4\uD0C0\uC77C\uC785\uB2C8\uB2E4";
            map["Style saved: it is in Saved styles"] = "\uC800\uC7A5\uD55C \uC2A4\uD0C0\uC77C \uBAA9\uB85D\uC5D0 \uCD94\uAC00\uD588\uC2B5\uB2C8\uB2E4";
            map["No styles yet: click + to save the current one"] = "\uC800\uC7A5\uD55C \uC2A4\uD0C0\uC77C\uC774 \uC5C6\uC2B5\uB2C8\uB2E4. +\uB97C \uB20C\uB7EC \uD604\uC7AC \uC2A4\uD0C0\uC77C\uC744 \uC800\uC7A5\uD558\uC138\uC694";
            map["Remove a style"] = "\uC2A4\uD0C0\uC77C \uC81C\uAC70";

            // Canvas
            map["Fit"] = "\uB9DE\uCDA4";
            map["Type and press Enter \u00B7 Shift+Enter adds a line \u00B7 Esc cancels"] = "\uC785\uB825 \uD6C4 Enter \u00B7 Shift+Enter: \uC904\uBC14\uAFC8 \u00B7 Esc: \uCDE8\uC18C";
            map["Spotlight selected: drag by border or pull points \u00B7 Del removes \u00B7 Ctrl+D adds another"] = "\uAC15\uC870 \uC601\uC5ED \uC120\uD0DD\uB428 \u00B7 \uD14C\uB450\uB9AC\uB97C \uB4DC\uB798\uADF8\uD558\uAC70\uB098 \uC870\uC808\uC810\uC744 \uC774\uB3D9 \u00B7 Del: \uC0AD\uC81C \u00B7 Ctrl+D: \uBCF5\uC81C";
            map["drag it; pull the blue midpoint to curve"] = "\uB4DC\uB798\uADF8\uD574 \uC774\uB3D9\uD558\uACE0 \uD30C\uB780 \uAC00\uC6B4\uB370 \uC810\uC744 \uB04C\uC5B4 \uACE1\uC120\uC73C\uB85C \uBCC0\uACBD";
            map["drag it; double-click edits text"] = "\uB4DC\uB798\uADF8\uD574 \uC774\uB3D9\uD558\uACE0 \uB450 \uBC88 \uD074\uB9AD\uD574 \uD14D\uC2A4\uD2B8 \uD3B8\uC9D1";
            map["drag it or pull points"] = "\uB4DC\uB798\uADF8\uD574 \uC774\uB3D9\uD558\uAC70\uB098 \uC870\uC808\uC810\uC73C\uB85C \uD06C\uAE30 \uC870\uC808";
            map["drag to move"] = "\uB4DC\uB798\uADF8\uD574 \uC774\uB3D9";
            map["Selected mark: {0} \u00B7 Del deletes \u00B7 Ctrl+D duplicates \u00B7 color and thickness apply"] = "\uC120\uD0DD\uD55C \uD45C\uC2DC: {0} \u00B7 Del: \uC0AD\uC81C \u00B7 Ctrl+D: \uBCF5\uC81C \u00B7 \uC0C9\uC0C1 \uBC0F \uB450\uAED8 \uBCC0\uACBD \uAC00\uB2A5";
            map["Drag to draw an arrow \u00B7 Shift draws straight \u00B7 then drag the blue midpoint to curve"] = "\uB4DC\uB798\uADF8\uD574 \uD654\uC0B4\uD45C \uADF8\uB9AC\uAE30 \u00B7 Shift: \uC9C1\uC120 \u00B7 \uD30C\uB780 \uAC00\uC6B4\uB370 \uC810\uC744 \uB04C\uBA74 \uACE1\uC120";
            map["Drag to draw a line \u00B7 Shift snaps to straight or 45\u00B0 \u00B7 then drag the blue midpoint to curve"] = "\uB4DC\uB798\uADF8\uD574 \uC120 \uADF8\uB9AC\uAE30 \u00B7 Shift: \uC218\uD3C9\u00B7\uC218\uC9C1\u00B745\u00B0 \u00B7 \uD30C\uB780 \uAC00\uC6B4\uB370 \uC810\uC744 \uB04C\uBA74 \uACE1\uC120";
            map["Drag over what matters: the rest is dimmed \u00B7 you can add multiple spotlights"] = "\uAC15\uC870\uD560 \uC601\uC5ED\uC744 \uB4DC\uB798\uADF8\uD558\uBA74 \uB098\uBA38\uC9C0\uAC00 \uC5B4\uB450\uC6CC\uC9D1\uB2C8\uB2E4 \u00B7 \uC5EC\uB7EC \uC601\uC5ED\uC744 \uAC15\uC870\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4";
            map["Drag to frame \u00B7 Shift makes a square \u00B7 click a mark to move it"] = "\uB4DC\uB798\uADF8\uD574 \uC0AC\uAC01\uD615 \uADF8\uB9AC\uAE30 \u00B7 Shift: \uC815\uC0AC\uAC01\uD615 \u00B7 \uD45C\uC2DC\uB97C \uD074\uB9AD\uD574 \uC774\uB3D9";
            map["Drag to circle \u00B7 Shift makes a circle"] = "\uB4DC\uB798\uADF8\uD574 \uD0C0\uC6D0 \uADF8\uB9AC\uAE30 \u00B7 Shift: \uC6D0";
            map["Click where you want to type"] = "\uD14D\uC2A4\uD2B8\uB97C \uC785\uB825\uD560 \uC704\uCE58\uB97C \uD074\uB9AD\uD558\uC138\uC694";
            map["Each click adds the next number: 1, 2, 3\u2026"] = "\uD074\uB9AD\uD560 \uB54C\uB9C8\uB2E4 \uB2E4\uC74C \uBC88\uD638\uB97C \uCD94\uAC00\uD569\uB2C8\uB2E4: 1, 2, 3\u2026";
            map["Drag over what you want to highlight"] = "\uAC15\uC870\uD560 \uBD80\uBD84\uC744 \uB4DC\uB798\uADF8\uD558\uC138\uC694";
            map["Drag over what you want to pixelate \u00B7 for passwords or sensitive data, use Redact (X)"] = "\uBAA8\uC790\uC774\uD06C\uD560 \uBD80\uBD84\uC744 \uB4DC\uB798\uADF8\uD558\uC138\uC694 \u00B7 \uC554\uD638\uB098 \uBBFC\uAC10 \uC815\uBCF4\uC5D0\uB294 \uAC00\uB9AC\uAE30(X)\uB97C \uC0AC\uC6A9\uD558\uC138\uC694";
            map["Drag over what you want to redact: a solid block that permanently erases what is underneath"] = "\uAC00\uB9B4 \uC601\uC5ED\uC744 \uB4DC\uB798\uADF8\uD558\uC138\uC694. \uB0B4\uBCF4\uB0B8 \uC774\uBBF8\uC9C0\uC5D0\uB294 \uB2E8\uC0C9 \uBE14\uB85D\uB9CC \uB0A8\uC2B5\uB2C8\uB2E4";
            map["Drag the area you want to keep \u00B7 Ctrl+Z undoes"] = "\uB0A8\uAE38 \uC601\uC5ED\uC744 \uB4DC\uB798\uADF8\uD558\uC138\uC694 \u00B7 Ctrl+Z: \uC2E4\uD589 \uCDE8\uC18C";
            map["Type here"] = "\uC5EC\uAE30\uC5D0 \uC785\uB825";
            map["Zoom out  (Ctrl+\u2212)"] = "\uCD95\uC18C  (Ctrl+\u2212)";
            map["Actual size  (Ctrl+1)"] = "\uC2E4\uC81C \uD06C\uAE30  (Ctrl+1)";
            map["Zoom in  (Ctrl++)"] = "\uD655\uB300  (Ctrl++)";
            map["Fit to window  (Ctrl+0)"] = "\uCC3D\uC5D0 \uB9DE\uCDA4  (Ctrl+0)";

            // Editor
            map["Undo  (Ctrl+Z)"] = "\uC2E4\uD589 \uCDE8\uC18C  (Ctrl+Z)";
            map["Redo  (Ctrl+Y)"] = "\uB2E4\uC2DC \uC2E4\uD589  (Ctrl+Y)";
            map["Presentation backdrop: gradients, images, blur or solid color, with padding, shadow and rounded corners  (B)"] = "\uD504\uB808\uC820\uD14C\uC774\uC158 \uBC30\uACBD: \uADF8\uB77C\uB370\uC774\uC158, \uC774\uBBF8\uC9C0, \uD750\uB9BC, \uB2E8\uC0C9\uC5D0 \uC5EC\uBC31\u00B7\uADF8\uB9BC\uC790\u00B7\uB465\uADFC \uBAA8\uC11C\uB9AC \uC801\uC6A9  (B)";
            map["Export"] = "\uB0B4\uBCF4\uB0B4\uAE30";
            map["Pin to screen: stays floating on top"] = "\uD654\uBA74\uC5D0 \uACE0\uC815: \uB2E4\uB978 \uCC3D \uC704\uC5D0 \uACC4\uC18D \uD45C\uC2DC";
            map["Save as PDF: tall scrolling captures split across pages without losing crispness  (Ctrl+P)"] = "PDF\uB85C \uC800\uC7A5: \uAE34 \uC2A4\uD06C\uB864 \uCEA1\uCC98\uB97C \uC120\uBA85\uD558\uAC8C \uC720\uC9C0\uD558\uBA70 \uC5EC\uB7EC \uD398\uC774\uC9C0\uB85C \uBD84\uD560  (Ctrl+P)";
            map["Drag"] = "\uB4DC\uB798\uADF8";
            map["Drag to chat or another application"] = "\uCC44\uD305\uC774\uB098 \uB2E4\uB978 \uC571\uC73C\uB85C \uB4DC\uB798\uADF8";
            map["Copy text from image (Windows OCR)  (Ctrl+Shift+C)"] = "\uC774\uBBF8\uC9C0\uC5D0\uC11C \uD14D\uC2A4\uD2B8 \uBCF5\uC0AC(Windows OCR)  (Ctrl+Shift+C)";
            map["Copy to clipboard  (Ctrl+C)"] = "\uD074\uB9BD\uBCF4\uB4DC\uC5D0 \uBCF5\uC0AC  (Ctrl+C)";
            map["Copied \u2713"] = "\uBCF5\uC0AC\uB428 \u2713";
            map["Keep in your captures folder  (Ctrl+S)"] = "\uCEA1\uCC98 \uD3F4\uB354\uC5D0 \uC800\uC7A5  (Ctrl+S)";
            map["Save over the image  (Ctrl+S)"] = "\uC6D0\uBCF8 \uC774\uBBF8\uC9C0\uC5D0 \uB36E\uC5B4\uC4F0\uAE30  (Ctrl+S)";
            map["Saved \u2713"] = "\uC800\uC7A5\uB428 \u2713";
            map["Apply, copy and close  (Enter)"] = "\uC801\uC6A9\uD558\uACE0 \uBCF5\uC0AC\uD55C \uB4A4 \uB2EB\uAE30  (Enter)";
            map["Restore"] = "\uC774\uC804 \uD06C\uAE30\uB85C";
            map["Maximize"] = "\uCD5C\uB300\uD654";
            map["Create a GIF with trims, annotations, backdrop and the options below  (Enter)"] = "\uC790\uB974\uAE30, \uC8FC\uC11D, \uBC30\uACBD \uBC0F \uC544\uB798 \uC635\uC158\uC744 \uC801\uC6A9\uD574 GIF \uB9CC\uB4E4\uAE30  (Enter)";
            map["Export to share: creates a compressed MP4 with trims, annotations, backdrop and the options below. The lossless master remains unchanged  (Enter)"] = "\uACF5\uC720\uC6A9 MP4\uB85C \uB0B4\uBCF4\uB0B4\uAE30: \uC790\uB974\uAE30, \uC8FC\uC11D, \uBC30\uACBD \uBC0F \uC544\uB798 \uC635\uC158\uC744 \uC801\uC6A9\uD569\uB2C8\uB2E4. \uBB34\uC190\uC2E4 \uB9C8\uC2A4\uD130\uB294 \uBCF4\uC874\uB429\uB2C8\uB2E4  (Enter)";
            map["Create an MP4 with trims, annotations, backdrop and options below; the original is unchanged  (Enter)"] = "\uC790\uB974\uAE30, \uC8FC\uC11D, \uBC30\uACBD \uBC0F \uC544\uB798 \uC635\uC158\uC744 \uC801\uC6A9\uD574 MP4 \uB9CC\uB4E4\uAE30. \uC6D0\uBCF8\uC740 \uBCF4\uC874\uB429\uB2C8\uB2E4  (Enter)";
            map["Size: "] = "\uD06C\uAE30: ";
            map["Thickness: "] = "\uB450\uAED8: ";
            map["Enter copies and closes"] = "Enter: \uBCF5\uC0AC\uD558\uACE0 \uB2EB\uAE30";
            map["Enter creates GIF"] = "Enter: GIF \uB9CC\uB4E4\uAE30";
            map["Enter creates shareable MP4"] = "Enter: \uACF5\uC720\uC6A9 MP4 \uB9CC\uB4E4\uAE30";
            map["Enter exports"] = "Enter: \uB0B4\uBCF4\uB0B4\uAE30";
            map["Playing \u00B7 Space pauses"] = "\uC7AC\uC0DD \uC911 \u00B7 Space: \uC77C\uC2DC \uC815\uC9C0";
            map[" \u00B7 visible throughout video"] = " \u00B7 \uC601\uC0C1 \uC804\uCCB4\uC5D0 \uD45C\uC2DC";
            map["Edit video \u00B7 "] = "\uB3D9\uC601\uC0C1 \uD3B8\uC9D1 \u00B7 ";
            map["Edit \u00B7 "] = "\uD3B8\uC9D1 \u00B7 ";
            map["Could not save: "] = "\uC800\uC7A5 \uC2E4\uD328: ";
            map["Could not save edit"] = "\uD3B8\uC9D1\uBCF8\uC744 \uC800\uC7A5\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4";
            map["Saving {0}\u2026"] = "{0} \uC800\uC7A5 \uC911\u2026";
            map["Saved: {0}"] = "\uC800\uC7A5\uB428: {0}";
            map["Could not copy: "] = "\uBCF5\uC0AC \uC2E4\uD328: ";
            map["Reading text\u2026"] = "\uD14D\uC2A4\uD2B8 \uC77D\uB294 \uC911\u2026";
            map["Text copied to clipboard."] = "\uD074\uB9BD\uBCF4\uB4DC\uC5D0 \uD14D\uC2A4\uD2B8\uB97C \uBCF5\uC0AC\uD588\uC2B5\uB2C8\uB2E4.";
            map["Could not read text."] = "\uD14D\uC2A4\uD2B8\uB97C \uC77D\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
            map["Could not save"] = "\uC800\uC7A5\uD560 \uC218 \uC5C6\uC74C";
            map["Could not pin image to screen."] = "\uC774\uBBF8\uC9C0\uB97C \uD654\uBA74\uC5D0 \uACE0\uC815\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
            map["Could not open recording."] = "\uB179\uD654 \uD30C\uC77C\uC744 \uC5F4\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
            map["\n\nThe file cannot be read right now; please try again."] = "\n\n\uD604\uC7AC \uD30C\uC77C\uC744 \uC77D\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694.";
            map["Lossless master: Export creates a shareable MP4; the master does not change."] = "\uBB34\uC190\uC2E4 \uB9C8\uC2A4\uD130\uC785\uB2C8\uB2E4. \u2018\uB0B4\uBCF4\uB0B4\uAE30\u2019\uB97C \uB204\uB974\uBA74 \uC6D0\uBCF8\uC744 \uC720\uC9C0\uD558\uBA74\uC11C \uACF5\uC720\uC6A9 MP4\uB97C \uB9CC\uB4ED\uB2C8\uB2E4.";
            map["Cannot play {0}."] = "{0}\uC744(\uB97C) \uC7AC\uC0DD\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Could not start playback."] = "\uC7AC\uC0DD\uC744 \uC2DC\uC791\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
            map["Could not play the next video."] = "\uB2E4\uC74C \uB3D9\uC601\uC0C1\uC744 \uC7AC\uC0DD\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
            map["Track {0}"] = "\uD2B8\uB799 {0}";
            map["Mute"] = "\uC74C\uC18C\uAC70";
            map["Add video"] = "\uB3D9\uC601\uC0C1 \uCD94\uAC00";
            map["Adding {0}\u2026"] = "{0} \uCD94\uAC00 \uC911\u2026";
            map["Adding {0} videos\u2026"] = "\uB3D9\uC601\uC0C1 {0}\uAC1C \uCD94\uAC00 \uC911\u2026";
            map["Added: {0}"] = "\uCD94\uAC00\uB428: {0}";
            map["Added: {0} videos"] = "\uB3D9\uC601\uC0C1 {0}\uAC1C \uCD94\uAC00\uB428";
            map["Could not open as video:\n\n"] = "\uB3D9\uC601\uC0C1\uC73C\uB85C \uC5F4\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4:\n\n";
            map["No segment remains within crop."] = "\uC120\uD0DD\uD55C \uBC94\uC704\uC5D0 \uB0A8\uC740 \uAD6C\uAC04\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Nothing to export: video is already ready to share as is."] = "\uB0B4\uBCF4\uB0BC \uBCC0\uACBD \uC0AC\uD56D\uC774 \uC5C6\uC2B5\uB2C8\uB2E4. \uD604\uC7AC \uB3D9\uC601\uC0C1\uC744 \uADF8\uB300\uB85C \uACF5\uC720\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            map["Could not prepare video: "] = "\uB3D9\uC601\uC0C1\uC744 \uC900\uBE44\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4: ";
            map["The duration of this video is unknown, so it cannot be fit to {0}. Choose Maximum, High or Light in Quality."] = "\uC7AC\uC0DD \uC2DC\uAC04\uC744 \uC54C \uC218 \uC5C6\uC5B4 {0}\uC5D0 \uB9DE\uCD9C \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. \uD654\uC9C8\uC5D0\uC11C \uCD5C\uACE0, \uB192\uC74C \uB610\uB294 \uACBD\uB7C9\uC744 \uC120\uD0DD\uD558\uC138\uC694.";
            map["Too long to fit into {0} with good quality (holds {1}). Crop the video, increase speed or choose 720p or lower in Size."] = "\uC601\uC0C1\uC774 \uB108\uBB34 \uAE38\uC5B4 {0} \uC548\uC5D0 \uC88B\uC740 \uD654\uC9C8\uB85C \uB2F4\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4(\uAC00\uB2A5\uD55C \uAE38\uC774: {1}). \uC601\uC0C1\uC744 \uC790\uB974\uAC70\uB098 \uC18D\uB3C4\uB97C \uB192\uC774\uAC70\uB098 \uD06C\uAE30\uB97C 720p \uC774\uD558\uB85C \uC904\uC774\uC138\uC694.";
            map["Creating the GIF\u2026 "] = "GIF \uB9CC\uB4DC\uB294 \uC911\u2026 ";
            map["Creating the video\u2026 "] = "\uB3D9\uC601\uC0C1 \uB9CC\uB4DC\uB294 \uC911\u2026 ";
            map["Esc cancels"] = "Esc: \uCDE8\uC18C";
            map["Could not create video.\n\n"] = "\uB3D9\uC601\uC0C1\uC744 \uB9CC\uB4E4\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.\n\n";
            map["Checking video\u2026"] = "\uB3D9\uC601\uC0C1 \uD655\uC778 \uC911\u2026";
            map["Exported: {0} is now in stack."] = "\uB0B4\uBCF4\uB0B8 {0}\uC744(\uB97C) \uC378\uB124\uC77C \uBAA9\uB85D\uC5D0 \uCD94\uAC00\uD588\uC2B5\uB2C8\uB2E4.";
            map["Exported: {0}"] = "\uB0B4\uBCF4\uB0C4: {0}";
            map["\n\nThe file was not added to stack. Stored in:\n{0}\n\nThe editor remains open: you can export again."] = "\n\n\uC378\uB124\uC77C \uBAA9\uB85D\uC5D0 \uCD94\uAC00\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. \uC800\uC7A5 \uC704\uCE58:\n{0}\n\n\uD3B8\uC9D1\uAE30\uC5D0\uC11C \uB2E4\uC2DC \uB0B4\uBCF4\uB0BC \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            map["Cancel export?\n\nThe partially created video will be discarded."] = "\uB0B4\uBCF4\uB0B4\uAE30\uB97C \uCDE8\uC18C\uD560\uAE4C\uC694?\n\n\uB9CC\uB4E4\uB358 \uB3D9\uC601\uC0C1\uC740 \uBC84\uB9BD\uB2C8\uB2E4.";
            map["Cancel export"] = "\uB0B4\uBCF4\uB0B4\uAE30 \uCDE8\uC18C";
            map["Keep exporting"] = "\uACC4\uC18D \uB0B4\uBCF4\uB0B4\uAE30";
            map["Export recording with changes?\n\nIf you discard them, changes are lost; the original video does not change."] = "\uBCC0\uACBD \uC0AC\uD56D\uC744 \uC801\uC6A9\uD574 \uB179\uD654\uB97C \uB0B4\uBCF4\uB0BC\uAE4C\uC694?\n\n\uBC84\uB9AC\uBA74 \uD3B8\uC9D1 \uB0B4\uC6A9\uC774 \uC0AC\uB77C\uC9D1\uB2C8\uB2E4. \uC6D0\uBCF8 \uB3D9\uC601\uC0C1\uC740 \uBCF4\uC874\uB429\uB2C8\uB2E4.";
            map["Discard changes"] = "\uBCC0\uACBD \uC0AC\uD56D \uBC84\uB9AC\uAE30";
            map["Keep editing"] = "\uACC4\uC18D \uD3B8\uC9D1";
            map["Keep changes to capture?"] = "\uCEA1\uCC98\uC758 \uBCC0\uACBD \uC0AC\uD56D\uC744 \uC800\uC7A5\uD560\uAE4C\uC694?";
            map["Keep"] = "\uC800\uC7A5";
            map["{0} is currently exporting.\n\nIf you exit now, the half-created video will be discarded."] = "{0} \uB0B4\uBCF4\uB0B4\uAE30\uAC00 \uC9C4\uD589 \uC911\uC785\uB2C8\uB2E4.\n\n\uC9C0\uAE08 \uC885\uB8CC\uD558\uBA74 \uB9CC\uB4E4\uB358 \uB3D9\uC601\uC0C1\uC740 \uBC84\uB9BD\uB2C8\uB2E4.";
            map["Cancel and exit"] = "\uCDE8\uC18C\uD558\uACE0 \uC885\uB8CC";
            map["There are unexported changes in {0}.\n\nWith Export, Stackshot creates the video and stays open: exit again when finished."] = "{0}\uC5D0 \uC544\uC9C1 \uB0B4\uBCF4\uB0B4\uC9C0 \uC54A\uC740 \uBCC0\uACBD \uC0AC\uD56D\uC774 \uC788\uC2B5\uB2C8\uB2E4.\n\n\u2018\uB0B4\uBCF4\uB0B4\uAE30\u2019\uB97C \uB204\uB974\uBA74 \uB3D9\uC601\uC0C1\uC744 \uB9CC\uB4E0 \uB4A4\uC5D0\uB3C4 \uC571\uC774 \uC5F4\uB824 \uC788\uC2B5\uB2C8\uB2E4. \uC644\uB8CC\uB418\uBA74 \uB2E4\uC2DC \uC885\uB8CC\uD558\uC138\uC694.";
            map["Exit without exporting"] = "\uB0B4\uBCF4\uB0B4\uC9C0 \uC54A\uACE0 \uC885\uB8CC";
            map["Save changes in {0} before exiting?"] = "\uC885\uB8CC\uD558\uAE30 \uC804\uC5D0 {0}\uC758 \uBCC0\uACBD \uC0AC\uD56D\uC744 \uC800\uC7A5\uD560\uAE4C\uC694?";
            map["Arrow"] = "\uD654\uC0B4\uD45C";
            map["Line"] = "\uC120";
            map["Rectangle"] = "\uC0AC\uAC01\uD615";
            map["Ellipse"] = "\uD0C0\uC6D0";
            map["Numbers"] = "\uBC88\uD638";
            map["Highlight"] = "\uD615\uAD11\uD39C";
            map["Spotlight: dims the rest"] = "\uC9D1\uC911 \uC870\uBA85: \uB098\uBA38\uC9C0 \uC601\uC5ED\uC744 \uC5B4\uB461\uAC8C";
            map["Pixelate"] = "\uBAA8\uC790\uC774\uD06C";
            map["Redact (safest for sensitive data)"] = "\uAC00\uB9AC\uAE30(\uBBFC\uAC10 \uC815\uBCF4 \uBCF4\uD638\uC6A9)";
            map["Crop"] = "\uC790\uB974\uAE30";
            map["Thin"] = "\uC587\uAC8C";
            map["Medium"] = "\uC911\uAC04";
            map["Thick"] = "\uB450\uAECD\uAC8C";
            map["Small"] = "\uC791\uAC8C";
            map["Large"] = "\uD06C\uAC8C";

            // Editor.Sensitive
            map["Sensitive data"] = "\uBBFC\uAC10 \uC815\uBCF4";
            map["Find and redact ID, SSN, card, IBAN, email, phone numbers and secrets. Read locally on your PC and any mark can be removed  (Ctrl+Shift+X)"] = "\uC2E0\uBD84\uC99D \uBC88\uD638, \uBBF8\uAD6D \uC0AC\uD68C \uBCF4\uC7A5 \uBC88\uD638, \uCE74\uB4DC \uBC88\uD638, IBAN, \uC774\uBA54\uC77C, \uC804\uD654\uBC88\uD638, \uBE44\uBC00 \uD0A4\uB97C \uCC3E\uC544 \uAC00\uB9BD\uB2C8\uB2E4. PC\uC5D0\uC11C\uB9CC \uAC80\uC0AC\uD558\uBA70 \uAC00\uB9AC\uAE30 \uD45C\uC2DC\uB97C \uAC1C\uBCC4\uC801\uC73C\uB85C \uC81C\uAC70\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4  (Ctrl+Shift+X)";
            map["Searching\u2026"] = "\uAC80\uC0C9 \uC911\u2026";
            map["Could not read image."] = "\uC774\uBBF8\uC9C0\uB97C \uC77D\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
            map["Searching for sensitive data\u2026"] = "\uBBFC\uAC10 \uC815\uBCF4 \uAC80\uC0C9 \uC911\u2026";
            map["Not enough memory for such a large image."] = "\uC774\uBBF8\uC9C0\uAC00 \uB108\uBB34 \uCEE4\uC11C \uBA54\uBAA8\uB9AC\uAC00 \uBD80\uC871\uD569\uB2C8\uB2E4.";
            map["Could not search for sensitive data. Try again."] = "\uBBFC\uAC10 \uC815\uBCF4\uB97C \uAC80\uC0C9\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694.";
            map["Could not search for sensitive data. "] = "\uBBFC\uAC10 \uC815\uBCF4\uB97C \uAC80\uC0C9\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. ";
            map["Sensitive data was redacted on capture: it is a mark and you can remove it."] = "\uCEA1\uCC98\uD560 \uB54C \uBBFC\uAC10 \uC815\uBCF4\uB97C \uAC00\uB838\uC2B5\uB2C8\uB2E4. \uAC00\uB9AC\uAE30 \uD45C\uC2DC\uB294 \uC81C\uAC70\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            map["Sensitive data was redacted on capture: they are marks and you can remove any."] = "\uCEA1\uCC98\uD560 \uB54C \uBBFC\uAC10 \uC815\uBCF4\uB97C \uAC00\uB838\uC2B5\uB2C8\uB2E4. \uAC00\uB9AC\uAE30 \uD45C\uC2DC\uB294 \uAC1C\uBCC4\uC801\uC73C\uB85C \uC81C\uAC70\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";

            // Player
            map["Playback error: "] = "\uC7AC\uC0DD \uC624\uB958: ";
            map["No sound output"] = "\uC624\uB514\uC624 \uCD9C\uB825 \uC5C6\uC74C";
            map["Could not play: FFmpeg is not responding."] = "FFmpeg\uAC00 \uC751\uB2F5\uD558\uC9C0 \uC54A\uC544 \uC7AC\uC0DD\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Could not play the video."] = "\uB3D9\uC601\uC0C1\uC744 \uC7AC\uC0DD\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";

            // PlayerKit
            map["100 % (actual size: 1 video pixel = 1 screen pixel)"] = "100% (\uC2E4\uC81C \uD06C\uAE30: \uC601\uC0C1 1\uD53D\uC140 = \uD654\uBA74 1\uD53D\uC140)";
            map[" % (scaled to fill window)"] = "% (\uCC3D \uD06C\uAE30\uC5D0 \uB9DE\uCDA4)";
            map["window is smaller than video: maximize or press F"] = "\uCC3D\uC774 \uC601\uC0C1\uBCF4\uB2E4 \uC791\uC2B5\uB2C8\uB2E4. \uCD5C\uB300\uD654\uD558\uAC70\uB098 F\uB97C \uB204\uB974\uC138\uC694";
            map["video is larger than screen"] = "\uC601\uC0C1\uC774 \uD654\uBA74\uBCF4\uB2E4 \uD07D\uB2C8\uB2E4";
            map["File"] = "\uD30C\uC77C";
            map["Video codec"] = "\uB3D9\uC601\uC0C1 \uCF54\uB371";
            map["Pixel format"] = "\uD53D\uC140 \uD615\uC2DD";
            map["Size"] = "\uD06C\uAE30";
            map[" (non-square pixels {0}:{1}, displayed as {2} \u00D7 {3})"] = " (\uBE44\uC815\uC0AC\uAC01\uD615 \uD53D\uC140 {0}:{1}, \uD45C\uC2DC \uD06C\uAE30 {2} \u00D7 {3})";
            map["Frames per second"] = "\uCD08\uB2F9 \uD504\uB808\uC784 \uC218";
            map["Duration"] = "\uC7AC\uC0DD \uC2DC\uAC04";
            map["No audio"] = "\uC624\uB514\uC624 \uC5C6\uC74C";
            map["stereo"] = "\uC2A4\uD14C\uB808\uC624";
            map["File size"] = "\uD30C\uC77C \uD06C\uAE30";
            map["Lossless master"] = "\uBB34\uC190\uC2E4 \uB9C8\uC2A4\uD130";
            map["Yes: every pixel and audio exactly as delivered by Windows"] = "\uC608: \uBAA8\uB4E0 \uD53D\uC140\uACFC \uC624\uB514\uC624\uAC00 Windows\uC5D0\uC11C \uBC1B\uC740 \uC6D0\uBCF8\uACFC \uC77C\uCE58\uD569\uB2C8\uB2E4";
            map["Verification"] = "\uAC80\uC99D";
            map["Passed"] = "\uD1B5\uACFC";
            map[": {0} identical frames"] = ": {0}\uAC1C \uD504\uB808\uC784 \uC77C\uCE58";
            map["Failed"] = "\uC2E4\uD328";

            // PlayerWindow.Access
            map["Position"] = "\uC704\uCE58";
            map["Left / Right"] = "\uC67C\uCABD / \uC624\uB978\uCABD";
            map["Volume"] = "\uBCFC\uB968";
            map["Up / Down"] = "\uC704 / \uC544\uB798";

            // PlayerWindow
            map["Resume from where you left off ({0}) \u00B7 Home returns to start"] = "{0}\uBD80\uD130 \uC774\uC5B4 \uC7AC\uC0DD \u00B7 Home: \uCC98\uC74C\uC73C\uB85C";
            map["Volume {0} %"] = "\uBCFC\uB968 {0}%";
            map["Muted"] = "\uC74C\uC18C\uAC70\uB428";
            map["Speed {0}"] = "\uC18D\uB3C4 {0}";
            map["Loop: on"] = "\uBC18\uBCF5: \uCF1C\uC9D0";
            map["Loop: off"] = "\uBC18\uBCF5: \uAEBC\uC9D0";
            map["In-point at {0} \u00B7 S sets out-point"] = "\uC2DC\uC791\uC810 {0} \u00B7 S: \uB05D\uC810 \uC9C0\uC815";
            map["Clip {0} \u2013 {1}"] = "\uD074\uB9BD {0} \u2013 {1}";
            map["Out-point must be after in-point ({0})"] = "\uB05D\uC810\uC740 \uC2DC\uC791\uC810({0}) \uB4A4\uC5EC\uC57C \uD569\uB2C8\uB2E4";
            map["Clip {0} \u2013 {1} \u00B7 Export creates MP4; G creates GIF"] = "\uD074\uB9BD {0} \u2013 {1} \u00B7 \uB0B4\uBCF4\uB0B4\uAE30: MP4 \u00B7 G: GIF";
            map["Clip cleared"] = "\uD074\uB9BD \uBC94\uC704 \uD574\uC81C\uB428";
            map["Could not capture frame."] = "\uD504\uB808\uC784\uC744 \uCEA1\uCC98\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
            map["Frame at {0} added to stack"] = "{0}\uC758 \uD504\uB808\uC784\uC744 \uC378\uB124\uC77C \uBAA9\uB85D\uC5D0 \uCD94\uAC00\uD588\uC2B5\uB2C8\uB2E4";
            map[" frame "] = " \uD504\uB808\uC784 ";
            map["Frame saved: {0}"] = "\uD504\uB808\uC784 \uC800\uC7A5\uB428: {0}";
            map["Frame "] = "\uD504\uB808\uC784 ";
            map["Could not save frame: "] = "\uD504\uB808\uC784 \uC800\uC7A5 \uC2E4\uD328: ";
            map["Could not prepare export."] = "\uB0B4\uBCF4\uB0B4\uAE30\uB97C \uC900\uBE44\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
            map["Could not create MP4.\n\n"] = "MP4\uB97C \uB9CC\uB4E4\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.\n\n";
            map["MP4 ready and saved next to original"] = "MP4\uB97C \uC6D0\uBCF8\uACFC \uAC19\uC740 \uD3F4\uB354\uC5D0 \uC800\uC7A5\uD588\uC2B5\uB2C8\uB2E4";
            map["MP4 ready: added to stack"] = "MP4\uB97C \uC378\uB124\uC77C \uBAA9\uB85D\uC5D0 \uCD94\uAC00\uD588\uC2B5\uB2C8\uB2E4";
            map["MP4 ready"] = "MP4 \uC900\uBE44 \uC644\uB8CC";
            map["Could not verify video."] = "\uB3D9\uC601\uC0C1\uC744 \uAC80\uC99D\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
            map["\n\nThe file was not added to stack. Stored in:\n{0}"] = "\n\n\uC378\uB124\uC77C \uBAA9\uB85D\uC5D0 \uCD94\uAC00\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. \uC800\uC7A5 \uC704\uCE58:\n{0}";
            map["Cannot open that."] = "\uC5F4 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["That file is not a playable video."] = "\uC7AC\uC0DD\uD560 \uC218 \uC788\uB294 \uB3D9\uC601\uC0C1\uC774 \uC544\uB2D9\uB2C8\uB2E4.";
            map["Export clip"] = "\uD074\uB9BD \uB0B4\uBCF4\uB0B4\uAE30";
            map["Export MP4"] = "MP4 \uB0B4\uBCF4\uB0B4\uAE30";
            map["Capture this frame"] = "\uD604\uC7AC \uD504\uB808\uC784 \uCEA1\uCC98";
            map["Set in-point"] = "\uC2DC\uC791\uC810 \uC9C0\uC815";
            map["Set out-point"] = "\uB05D\uC810 \uC9C0\uC815";
            map["Clear range"] = "\uBC94\uC704 \uD574\uC81C";
            map["Open another video\u2026"] = "\uB2E4\uB978 \uB3D9\uC601\uC0C1 \uC5F4\uAE30\u2026";
            map["Information"] = "\uC815\uBCF4";
            map["Loop playback"] = "\uBC18\uBCF5 \uC7AC\uC0DD";
            map["Scaling: best quality"] = "\uD06C\uAE30 \uC870\uC808: \uCD5C\uACE0 \uD654\uC9C8";
            map["Scaling: faster"] = "\uD06C\uAE30 \uC870\uC808: \uC18D\uB3C4 \uC6B0\uC120";
            map["Image: 100% scale (sharpest)"] = "\uC601\uC0C1: \uC2E4\uC81C \uD06C\uAE30(\uAC00\uC7A5 \uC120\uBA85)";
            map["Image: fit window"] = "\uC601\uC0C1: \uCC3D\uC5D0 \uB9DE\uCDA4";
            map["Open videos in Stackshot"] = "Stackshot\uC5D0\uC11C \uB3D9\uC601\uC0C1 \uC5F4\uAE30";
            map["Videos will open in Stackshot"] = "Stackshot\uC5D0\uC11C \uB3D9\uC601\uC0C1\uC744 \uC5FD\uB2C8\uB2E4";
            map["Videos will open in Windows player"] = "Windows \uD50C\uB808\uC774\uC5B4\uC5D0\uC11C \uB3D9\uC601\uC0C1\uC744 \uC5FD\uB2C8\uB2E4";
            map["On screen"] = "\uD654\uBA74 \uD45C\uC2DC";
            map["Playback"] = "\uC7AC\uC0DD";
            map["{0} frames shown, {1} dropped"] = "\uD45C\uC2DC\uD55C \uD504\uB808\uC784 {0}\uAC1C, \uB204\uB77D\uB41C \uD504\uB808\uC784 {1}\uAC1C";
            map["Pause  (Space)"] = "\uC77C\uC2DC \uC815\uC9C0  (Space)";
            map["Play  (Space)"] = "\uC7AC\uC0DD  (Space)";
            map["Unmute  (M)"] = "\uC74C\uC18C\uAC70 \uD574\uC81C  (M)";
            map["Mute  (M)"] = "\uC74C\uC18C\uAC70  (M)";
            map["Full screen  (F)"] = "\uC804\uCCB4 \uD654\uBA74  (F)";
            map["Audio track"] = "\uC624\uB514\uC624 \uD2B8\uB799";
            map["Speed"] = "\uC18D\uB3C4";
            map["Information  (I)"] = "\uC815\uBCF4  (I)";
            map["Open in editor"] = "\uD3B8\uC9D1\uAE30\uC5D0\uC11C \uC5F4\uAE30";
            map["Create MP4 with marked range (E and S)"] = "\uC9C0\uC815\uD55C \uAD6C\uAC04(E, S)\uC73C\uB85C MP4 \uB9CC\uB4E4\uAE30";
            map["Create shareable MP4"] = "\uACF5\uC720\uC6A9 MP4 \uB9CC\uB4E4\uAE30";
            map["quality "] = "\uD654\uC9C8 ";

            // PlayerWindow.PieceGif
            map["GIF ready and saved next to original"] = "GIF\uB97C \uC6D0\uBCF8\uACFC \uAC19\uC740 \uD3F4\uB354\uC5D0 \uC800\uC7A5\uD588\uC2B5\uB2C8\uB2E4";
            map["GIF ready: added to stack"] = "GIF\uB97C \uC378\uB124\uC77C \uBAA9\uB85D\uC5D0 \uCD94\uAC00\uD588\uC2B5\uB2C8\uB2E4";
            map["Create a GIF from marked clip (E & S) \u00B7 {0}  (G)"] = "\uC9C0\uC815\uD55C \uAD6C\uAC04(E, S)\uC73C\uB85C GIF \uB9CC\uB4E4\uAE30 \u00B7 {0}  (G)";
            map["at GIF frame rate"] = "GIF \uD504\uB808\uC784 \uC18D\uB3C4 \uC0AC\uC6A9";
            map["Export GIF of clip"] = "\uD074\uB9BD\uC744 GIF\uB85C \uB0B4\uBCF4\uB0B4\uAE30";
            map["Clip is {0} long: such a long GIF has a large file size and takes time to create. For such a clip, \u201CExport clip\u201D (MP4) is usually better.\n\nCreate GIF anyway?"] = "\uD074\uB9BD \uAE38\uC774\uB294 {0}\uC785\uB2C8\uB2E4. \uAE34 GIF\uB294 \uC6A9\uB7C9\uC774 \uD06C\uACE0 \uB9CC\uB4DC\uB294 \uB370 \uC624\uB798 \uAC78\uB9BD\uB2C8\uB2E4. \uAE34 \uD074\uB9BD\uC740 \u2018\uD074\uB9BD \uB0B4\uBCF4\uB0B4\uAE30\u2019\uB85C MP4\uB97C \uB9CC\uB4DC\uB294 \uD3B8\uC774 \uC88B\uC2B5\uB2C8\uB2E4.\n\n\uADF8\uB798\uB3C4 GIF\uB97C \uB9CC\uB4E4\uAE4C\uC694?";
            map["Mark clip with E and S to create GIF"] = "E\uC640 S\uB85C GIF\uB97C \uB9CC\uB4E4 \uAD6C\uAC04\uC744 \uC9C0\uC815\uD558\uC138\uC694";
            map["Could not prepare GIF."] = "GIF\uB97C \uC900\uBE44\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
            map["Creating the GIF\u2026 0%  \u00B7  Esc cancels"] = "GIF \uB9CC\uB4DC\uB294 \uC911\u2026 0%  \u00B7  Esc: \uCDE8\uC18C";
            map["The GIF palette is not valid."] = "GIF \uD314\uB808\uD2B8\uAC00 \uC62C\uBC14\uB974\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
            map["Checking the GIF\u2026  \u00B7  Esc cancels"] = "GIF \uD655\uC778 \uC911\u2026  \u00B7  Esc: \uCDE8\uC18C";
            map["Creating the GIF\u2026 {0}%  \u00B7  Esc cancels"] = "GIF \uB9CC\uB4DC\uB294 \uC911\u2026 {0}%  \u00B7  Esc: \uCDE8\uC18C";
            map["Could not create GIF.\n\n"] = "GIF\uB97C \uB9CC\uB4E4\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.\n\n";
            map["GIF ready"] = "GIF \uC900\uBE44 \uC644\uB8CC";
            map["Could not verify GIF."] = "GIF\uB97C \uAC80\uC99D\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";

            // ShareCheck
            map["MP4 structure"] = "MP4 \uAD6C\uC870";
            map["Fast start"] = "\uBE60\uB978 \uC2DC\uC791";
            map["Sound"] = "\uC18C\uB9AC";
            map["Full read"] = "\uD30C\uC77C \uC804\uCCB4 \uC77D\uAE30";
            map["H.264 level"] = "H.264 \uB808\uBCA8";
            map["Size limit"] = "\uD06C\uAE30 \uC81C\uD55C";
            map["{0} extra bytes at the end"] = "\uD30C\uC77C \uB05D\uC5D0 {0}\uBC14\uC774\uD2B8\uAC00 \uB354 \uC788\uC74C";
            map["cannot read at {0}"] = "{0} \uC704\uCE58\uC5D0\uC11C \uC77D\uAE30 \uC2E4\uD328";
            map["the '{0}' box is cut short"] = "\u2018{0}\u2019 \uBC15\uC2A4\uC758 \uB05D\uC774 \uC798\uB838\uC2B5\uB2C8\uB2E4";
            map["the '{0}' box at {1} has an impossible size"] = "{1} \uC704\uCE58\uC758 \u2018{0}\u2019 \uBC15\uC2A4 \uD06C\uAE30\uAC00 \uC798\uBABB\uB418\uC5C8\uC2B5\uB2C8\uB2E4";
            map["the '{0}' box declares {1} bytes and only {2} are left (file cut short)"] = "\u2018{0}\u2019 \uBC15\uC2A4 \uD06C\uAE30\uB294 {1}\uBC14\uC774\uD2B8\uB85C \uD45C\uC2DC\uB418\uC9C0\uB9CC {2}\uBC14\uC774\uD2B8\uB9CC \uB0A8\uC544 \uC788\uC2B5\uB2C8\uB2E4(\uD30C\uC77C \uC798\uB9BC)";
            map["The video was verified: it opens completely and has the expected size, duration and sound."] = "\uAC80\uC99D\uC744 \uB9C8\uCCE4\uC2B5\uB2C8\uB2E4. \uB3D9\uC601\uC0C1 \uC804\uCCB4\uB97C \uC77D\uC744 \uC218 \uC788\uC73C\uBA70 \uD06C\uAE30, \uC7AC\uC0DD \uC2DC\uAC04, \uC624\uB514\uC624\uAC00 \uC608\uC0C1\uACFC \uC77C\uCE58\uD569\uB2C8\uB2E4.";
            map["The video did not pass verification: "] = "\uB3D9\uC601\uC0C1 \uAC80\uC99D\uC5D0 \uC2E4\uD328\uD588\uC2B5\uB2C8\uB2E4: ";
            map["does not exist"] = "\uD30C\uC77C\uC774 \uC5C6\uC74C";
            map[", too small"] = ", \uD06C\uAE30\uAC00 \uB108\uBB34 \uC791\uC74C";
            map["{0} MB, at most {1} MB"] = "{0}MB(\uCD5C\uB300 {1}MB)";
            map["complete GIF"] = "\uC815\uC0C1\uC801\uC778 GIF";
            map["the GIF is cut short or is not a GIF"] = "GIF\uAC00 \uC798\uB838\uAC70\uB098 GIF \uD30C\uC77C\uC774 \uC544\uB2D8";
            map["does not start with 'ftyp'"] = "\u2018ftyp\u2019\uB85C \uC2DC\uC791\uD558\uC9C0 \uC54A\uC74C";
            map["'moov' is missing"] = "\u2018moov\u2019 \uB204\uB77D";
            map["'mdat' is missing"] = "\u2018mdat\u2019 \uB204\uB77D";
            map["the index comes before the data"] = "\uC778\uB371\uC2A4\uAC00 \uB370\uC774\uD130 \uC55E\uC5D0 \uC788\uC74C";
            map["the index comes after the data: it does not start playing until fully downloaded"] = "\uC778\uB371\uC2A4\uAC00 \uB370\uC774\uD130 \uB4A4\uC5D0 \uC788\uC5B4 \uB2E4\uC6B4\uB85C\uB4DC\uB97C \uC644\uB8CC\uD574\uC57C \uC7AC\uC0DD\uD560 \uC218 \uC788\uC74C";
            map["FFmpeg did not finish reading the file"] = "FFmpeg\uAC00 \uD30C\uC77C\uC744 \uB05D\uAE4C\uC9C0 \uC77D\uC9C0 \uBABB\uD568";
            map[" video(s), "] = "\uAC1C \uC601\uC0C1, ";
            map["no codec"] = "\uCF54\uB371 \uC5C6\uC74C";
            map[", expected "] = ", \uC608\uC0C1\uAC12: ";
            map["no profile"] = "\uD504\uB85C\uD544 \uC5C6\uC74C";
            map["no format"] = "\uD615\uC2DD \uC5C6\uC74C";
            map["; expected h264 High/Main, yuv420p"] = ", \uC608\uC0C1 \uD615\uC2DD: h264 High/Main, yuv420p";
            map["could not be read"] = "\uC77D\uC744 \uC218 \uC5C6\uC74C";
            map["without the BT.709 tags ("] = "BT.709 \uD0DC\uADF8 \uC5C6\uC74C(";
            map["none"] = "\uC5C6\uC74C";
            map["BT.709, but it does not say the chroma is centered"] = "BT.709\uC774\uC9C0\uB9CC \uD06C\uB85C\uB9C8\uAC00 \uC911\uC559\uC5D0 \uC704\uCE58\uD558\uB294\uC9C0 \uBA85\uC2DC\uB418\uC9C0 \uC54A\uC74C";
            map["BT.709, limited range"] = "BT.709, \uC81C\uD55C \uBC94\uC704";
            map[", centered chroma"] = ", \uC911\uC559 \uD06C\uB85C\uB9C8";
            map["{0} sound tracks, expected 1"] = "\uC624\uB514\uC624 \uD2B8\uB799 {0}\uAC1C(\uC608\uC0C1: 1\uAC1C)";
            map["no sound, as expected"] = "\uC608\uC0C1\uB300\uB85C \uC624\uB514\uC624 \uC5C6\uC74C";
            map["{0} sound tracks, expected 0"] = "\uC624\uB514\uC624 \uD2B8\uB799 {0}\uAC1C(\uC608\uC0C1: 0\uAC1C)";
            map["{0} s, expected {1} s"] = "{0}\uCD08(\uC608\uC0C1: {1}\uCD08)";
            map["took too long and was cancelled"] = "\uC2DC\uAC04\uC774 \uB108\uBB34 \uC624\uB798 \uAC78\uB824 \uCDE8\uC18C\uB428";
            map["no errors"] = "\uC624\uB958 \uC5C6\uC74C";
            map["FFmpeg exited with "] = "FFmpeg \uC885\uB8CC \uCF54\uB4DC: ";
            map["{0} frames, expected {1}"] = "{0}\uAC1C \uD504\uB808\uC784(\uC608\uC0C1: {1}\uAC1C)";

            // Timeline
            map["Timeline"] = "\uD0C0\uC784\uB77C\uC778";
            map[", trimmed from {0} to {1}"] = ", {0}\uBD80\uD130 {1}\uAE4C\uC9C0 \uC790\uB984";
            map[", with pieces removed"] = ", \uC77C\uBD80 \uAD6C\uAC04 \uC81C\uAC70\uB428";
            map["Arrows: frame by frame. Shift and arrows: second by second. Home and End: start and end of the trim."] = "\uBC29\uD5A5\uD0A4: \uD504\uB808\uC784 \uB2E8\uC704 \uC774\uB3D9. Shift+\uBC29\uD5A5\uD0A4: \uCD08 \uB2E8\uC704 \uC774\uB3D9. Home/End: \uC120\uD0DD \uAD6C\uAC04\uC758 \uC2DC\uC791/\uB05D.";
            map["{0} videos  \u00B7  "] = "\uB3D9\uC601\uC0C1 {0}\uAC1C  \u00B7  ";
            map["Duration "] = "\uC7AC\uC0DD \uC2DC\uAC04 ";
            map["  \u00B7  from {0} to {1}"] = "  \u00B7  {0}\uBD80\uD130 {1}\uAE4C\uC9C0";
            map["  \u00B7  {0} pieces"] = "  \u00B7  {0}\uAC1C \uAD6C\uAC04";
            map["Base video: sets resolution and frame rate"] = "\uAE30\uC900 \uB3D9\uC601\uC0C1: \uD574\uC0C1\uB3C4\uC640 \uD504\uB808\uC784 \uC18D\uB3C4\uB97C \uACB0\uC815\uD569\uB2C8\uB2E4";
            map["Move left"] = "\uC67C\uCABD\uC73C\uB85C \uC774\uB3D9";
            map["Move right"] = "\uC624\uB978\uCABD\uC73C\uB85C \uC774\uB3D9";
            map["Remove this video"] = "\uC774 \uB3D9\uC601\uC0C1 \uC81C\uAC70";
            map["Split"] = "\uBD84\uD560";
            map["Remove piece"] = "\uAD6C\uAC04 \uC81C\uAC70";
            map["+ Video"] = "+ \uB3D9\uC601\uC0C1";

            // Toolbar.Access
            map["Editor toolbar"] = "\uD3B8\uC9D1\uAE30 \uB3C4\uAD6C \uBAA8\uC74C";

            // Toolbar
            map["Drag me"] = "\uB4DC\uB798\uADF8";
            map["Drag it into a chat, an email or another app"] = "\uCC44\uD305, \uC774\uBA54\uC77C \uB610\uB294 \uB2E4\uB978 \uC571\uC73C\uB85C \uB4DC\uB798\uADF8\uD558\uC138\uC694";

            // VideoExport
            map["about {0} min"] = "\uC57D {0}\uBD84";
            map["about {0} s"] = "\uC57D {0}\uCD08";

            // Player dialogs and diagnostics
            map["The file cannot be found."] = "\uD30C\uC77C\uC744 \uCC3E\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["This file cannot be played: FFmpeg does not find a video to show."] = "FFmpeg\uAC00 \uC774 \uD30C\uC77C\uC5D0\uC11C \uC7AC\uC0DD\uD560 \uC601\uC0C1 \uC2A4\uD2B8\uB9BC\uC744 \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
            map["The duration of this video is unknown, so it cannot be fit to {0}. Mark a clip (E and S) or choose another quality in More options > Quality when exporting."] = "\uC7AC\uC0DD \uC2DC\uAC04\uC744 \uC54C \uC218 \uC5C6\uC5B4 {0}\uC5D0 \uB9DE\uCD9C \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. E\uC640 S\uB85C \uAD6C\uAC04\uC744 \uC9C0\uC815\uD558\uAC70\uB098 \uCD94\uAC00 \uC635\uC158 > \uB0B4\uBCF4\uB0B4\uAE30 \uD654\uC9C8\uC5D0\uC11C \uD654\uC9C8\uC744 \uBCC0\uACBD\uD558\uC138\uC694.";
            map["It is too long to fit in {0} with good quality ({1} fit). Mark a shorter clip (E and S) or choose another quality in More options > Quality when exporting."] = "\uB3D9\uC601\uC0C1\uC774 \uB108\uBB34 \uAE38\uC5B4 {0} \uC548\uC5D0 \uC88B\uC740 \uD654\uC9C8\uB85C \uB2F4\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4(\uAC00\uB2A5\uD55C \uAE38\uC774: {1}). E\uC640 S\uB85C \uC9E7\uC740 \uAD6C\uAC04\uC744 \uC9C0\uC815\uD558\uAC70\uB098 \uCD94\uAC00 \uC635\uC158 > \uB0B4\uBCF4\uB0B4\uAE30 \uD654\uC9C8\uC5D0\uC11C \uD654\uC9C8\uC744 \uBCC0\uACBD\uD558\uC138\uC694.";
            map["Creating the MP4\u2026 {0}%  \u00B7  Esc cancels"] = "MP4 \uB9CC\uB4DC\uB294 \uC911\u2026 {0}%  \u00B7  Esc: \uCDE8\uC18C";
            map["Checking the video\u2026  \u00B7  Esc cancels"] = "\uB3D9\uC601\uC0C1 \uD655\uC778 \uC911\u2026  \u00B7  Esc: \uCDE8\uC18C";
            map["  (normal)"] = "  (\uAE30\uBCF8)";
            map["Audio"] = "\uC624\uB514\uC624";
            map["Audio {0}"] = "\uC624\uB514\uC624 {0}";
            map["Color {0}"] = "\uC0C9\uC0C1: {0}";
            map["A kept piece must end after it starts."] = "\uB0A8\uAE38 \uAD6C\uAC04\uC758 \uB05D\uC810\uC740 \uC2DC\uC791\uC810 \uB4A4\uC5EC\uC57C \uD569\uB2C8\uB2E4.";
        }
    }
}
