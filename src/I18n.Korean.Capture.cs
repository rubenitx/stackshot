// Stackshot - Korean translations for screen capture, recording and floating thumbnails.
// MIT License - https://github.com/rubenitx/stackshot
using System.Collections.Generic;

namespace Stackshot
{
    public static partial class I18n
    {
        static void RegisterKoreanCapture(Dictionary<string, string> map)
        {
            // Audio
            map["Microphone"] = "\uB9C8\uC774\uD06C";
            map["Sound output"] = "\uC624\uB514\uC624 \uCD9C\uB825";
            map["The audio device is not responding."] = "\uC624\uB514\uC624 \uC7A5\uCE58\uAC00 \uC751\uB2F5\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
            map["There is no sound output."] = "\uC624\uB514\uC624 \uCD9C\uB825 \uC7A5\uCE58\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["There is no microphone connected."] = "\uC5F0\uACB0\uB41C \uB9C8\uC774\uD06C\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Device not found (0x{0})."] = "\uC7A5\uCE58\uB97C \uCC3E\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4(0x{0}).";
            map["Windows does not allow recording computer audio."] = "Windows\uC5D0\uC11C \uC2DC\uC2A4\uD15C \uC18C\uB9AC \uB179\uC74C\uC744 \uD5C8\uC6A9\uD558\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4.";
            map["Windows does not allow using the microphone. Enable it in Settings > Privacy & security > Microphone (\u201CLet desktop apps access your microphone\u201D)."] = "Windows\uC5D0\uC11C \uB9C8\uC774\uD06C \uC0AC\uC6A9\uC744 \uCC28\uB2E8\uD588\uC2B5\uB2C8\uB2E4. \uC124\uC815 > \uAC1C\uC778 \uC815\uBCF4 \uBC0F \uBCF4\uC548 > \uB9C8\uC774\uD06C\uC5D0\uC11C \u2018\uB370\uC2A4\uD06C\uD1B1 \uC571\uC5D0\uC11C \uB9C8\uC774\uD06C\uC5D0 \uC561\uC138\uC2A4\uD558\uB3C4\uB85D \uD5C8\uC6A9\u2019\uC744 \uCF1C\uC138\uC694.";
            map["The audio device cannot be used (0x{0})."] = "\uC624\uB514\uC624 \uC7A5\uCE58\uB97C \uC0AC\uC6A9\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4(0x{0}).";

            // Cine
            map["This version of FFmpeg does not include the lossless encoder (libx264rgb), required for Cinema quality.\n\nUpdate it from Stackshot settings or choose another quality."] = "\uC774 FFmpeg \uBC84\uC804\uC5D0\uB294 \uC2DC\uB124\uB9C8 \uD654\uC9C8\uC5D0 \uD544\uC694\uD55C \uBB34\uC190\uC2E4 \uC778\uCF54\uB354(libx264rgb)\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.\n\nStackshot \uC124\uC815\uC5D0\uC11C FFmpeg\uB97C \uC5C5\uB370\uC774\uD2B8\uD558\uAC70\uB098 \uB2E4\uB978 \uD654\uC9C8\uC744 \uC120\uD0DD\uD558\uC138\uC694.";
            map["Not enough disk space for lossless recording: {0} GB left, need at least {1} GB (a minute takes from several MBs up to multiple GBs depending on motion)."] = "\uBB34\uC190\uC2E4 \uB179\uD654\uB97C \uC704\uD55C \uB514\uC2A4\uD06C \uACF5\uAC04\uC774 \uBD80\uC871\uD569\uB2C8\uB2E4. \uB0A8\uC740 \uACF5\uAC04\uC740 {0}GB\uC774\uBA70 \uCD5C\uC18C {1}GB\uAC00 \uD544\uC694\uD569\uB2C8\uB2E4. \uC6C0\uC9C1\uC784\uC5D0 \uB530\uB77C 1\uBD84 \uB179\uD654\uC5D0 \uC218 MB\uC5D0\uC11C \uC218 GB\uAC00 \uD544\uC694\uD569\uB2C8\uB2E4.";
            map["The area of {0} \u00D7 {1} is too large to encode losslessly at 60 fps on most computers.\n\nYes: record at 30 fps, lossless.\nNo: try at 60 fps (if the PC cannot keep up, recording stops and saved portion is kept).\nCancel: do not record."] = "\uB300\uBD80\uBD84\uC758 PC\uC5D0\uC11C {0} \u00D7 {1} \uC601\uC5ED\uC744 60 fps\uB85C \uBB34\uC190\uC2E4 \uB179\uD654\uD558\uAE30\uB294 \uC5B4\uB835\uC2B5\uB2C8\uB2E4.\n\n\uC608: 30 fps\uB85C \uBB34\uC190\uC2E4 \uB179\uD654\uD569\uB2C8\uB2E4.\n\uC544\uB2C8\uC694: 60 fps\uB85C \uC2DC\uB3C4\uD569\uB2C8\uB2E4. PC\uAC00 \uC18D\uB3C4\uB97C \uB530\uB77C\uAC00\uC9C0 \uBABB\uD558\uBA74 \uB179\uD654\uB97C \uBA48\uCD94\uACE0 \uADF8\uB54C\uAE4C\uC9C0\uC758 \uC601\uC0C1\uC744 \uC800\uC7A5\uD569\uB2C8\uB2E4.\n\uCDE8\uC18C: \uB179\uD654\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
            map["FFmpeg exited with code {0}."] = "FFmpeg\uAC00 \uC885\uB8CC \uCF54\uB4DC {0}\uC744(\uB97C) \uBC18\uD658\uD588\uC2B5\uB2C8\uB2E4.";
            map["FFmpeg did not create the file."] = "FFmpeg\uAC00 \uD30C\uC77C\uC744 \uB9CC\uB4E4\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
            map["Check"] = "\uAC80\uC99D";
            map["Cancelled before finishing."] = "\uC644\uB8CC \uC804\uC5D0 \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4.";
            map["Stackshot Cine: lossless H.264 RGB video and 32-bit PCM audio"] = "Stackshot Cine: \uBB34\uC190\uC2E4 H.264 RGB \uC601\uC0C1 \uBC0F 32\uBE44\uD2B8 PCM \uC624\uB514\uC624";
            map["Could not finish: "] = "\uC644\uB8CC\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4: ";
            map["Reading the file"] = "\uD30C\uC77C \uC77D\uAE30";
            map["FFmpeg reads all of it without errors."] = "FFmpeg\uAC00 \uC624\uB958 \uC5C6\uC774 \uD30C\uC77C \uC804\uCCB4\uB97C \uC77D\uC5C8\uC2B5\uB2C8\uB2E4.";
            map["Container"] = "\uCEE8\uD14C\uC774\uB108";
            map["Not a Matroska file."] = "Matroska \uD30C\uC77C\uC774 \uC544\uB2D9\uB2C8\uB2E4.";
            map["H.264 RGB video"] = "H.264 RGB \uC601\uC0C1";
            map["High 4:4:4 Predictive profile"] = "High 4:4:4 Predictive \uD504\uB85C\uD544";
            map["The profile is not High 4:4:4 Predictive"] = "High 4:4:4 Predictive \uD504\uB85C\uD544\uC774 \uC544\uB2D8";
            map["full RGB (gbrp)"] = "\uC804\uCCB4 RGB(gbrp)";
            map["not full RGB"] = "\uC804\uCCB4 RGB\uAC00 \uC544\uB2D8";
            map["size differs from the recording"] = "\uB179\uD654\uD560 \uB54C\uC640 \uD06C\uAE30\uAC00 \uB2E4\uB984";
            map["Expected a single video stream and there are {0}."] = "\uC601\uC0C1 \uC2A4\uD2B8\uB9BC\uC774 \uD558\uB098\uC5EC\uC57C \uD558\uC9C0\uB9CC {0}\uAC1C\uAC00 \uC788\uC2B5\uB2C8\uB2E4.";
            map["Sound tracks"] = "\uC624\uB514\uC624 \uD2B8\uB799";
            map["Expected {0} tracks of 32-bit PCM at 48 kHz stereo and there are {1} (or in another format)."] = "48 kHz \uC2A4\uD14C\uB808\uC624 32\uBE44\uD2B8 PCM \uD2B8\uB799\uC774 {0}\uAC1C\uC5EC\uC57C \uD558\uC9C0\uB9CC {1}\uAC1C\uC774\uAC70\uB098 \uD615\uC2DD\uC774 \uB2E4\uB985\uB2C8\uB2E4.";
            map["Frame count"] = "\uD504\uB808\uC784 \uC218";
            map["{0} in the file, {1} sent to the encoder."] = "\uD30C\uC77C\uC5D0\uB294 {0}\uAC1C, \uC778\uCF54\uB354\uB85C \uBCF4\uB0B8 \uD504\uB808\uC784\uC740 {1}\uAC1C\uC785\uB2C8\uB2E4.";
            map["Frame {0} does not come after the one before."] = "{0}\uBC88 \uD504\uB808\uC784\uC758 \uC2DC\uAC01\uC774 \uC774\uC804 \uD504\uB808\uC784\uBCF4\uB2E4 \uB2A6\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
            map["Frame {0} is {1} ms off its time."] = "{0}\uBC88 \uD504\uB808\uC784\uC758 \uC2DC\uAC01\uC774 {1}ms \uC5B4\uAE0B\uB0AC\uC2B5\uB2C8\uB2E4.";
            map["The file has no frame list."] = "\uD30C\uC77C\uC5D0 \uD504\uB808\uC784 \uBAA9\uB85D\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Frame timing"] = "\uD504\uB808\uC784 \uD0C0\uC774\uBC0D";
            map["Every frame at its time (within 1 ms), always moving forward."] = "\uBAA8\uB4E0 \uD504\uB808\uC784\uC758 \uC2DC\uAC01\uC774 \uC21C\uC11C\uB300\uB85C \uC99D\uAC00\uD558\uBA70 \uC624\uCC28\uAC00 1ms \uC774\uB0B4\uC785\uB2C8\uB2E4.";
            map["Sound duration: "] = "\uC624\uB514\uC624 \uAE38\uC774: ";
            map["{0} s in the file, {1} s recorded."] = "\uD30C\uC77C\uC5D0\uB294 {0}\uCD08, \uB179\uC74C\uD55C \uAE38\uC774\uB294 {1}\uCD08\uC785\uB2C8\uB2E4.";
            map["This track has no packets."] = "\uC774 \uD2B8\uB799\uC5D0 \uD328\uD0B7\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["{0} of {1} frames identical (every pixel)."] = "{1}\uAC1C \uC911 {0}\uAC1C \uD504\uB808\uC784\uC758 \uBAA8\uB4E0 \uD53D\uC140\uC774 \uC77C\uCE58\uD569\uB2C8\uB2E4.";
            map["Frame {0} does not match; {1} of {2} identical."] = "{0}\uBC88 \uD504\uB808\uC784\uC774 \uB2E4\uB985\uB2C8\uB2E4. {2}\uAC1C \uC911 {1}\uAC1C\uAC00 \uC77C\uCE58\uD569\uB2C8\uB2E4.";
            map["{0} frames were decoded and {1} were recorded."] = "\uB514\uCF54\uB529\uD55C \uD504\uB808\uC784\uC740 {0}\uAC1C, \uB179\uD654\uD55C \uD504\uB808\uC784\uC740 {1}\uAC1C\uC785\uB2C8\uB2E4.";
            map["Frames identical to the captured ones"] = "\uCEA1\uCC98 \uC6D0\uBCF8\uACFC \uD504\uB808\uC784 \uC77C\uCE58 \uC5EC\uBD80";
            map["Sound length: "] = "\uC624\uB514\uC624 \uC0D8\uD50C \uC218: ";
            map["{0} samples, as recorded."] = "\uB179\uC74C\uD55C \uC6D0\uBCF8\uACFC \uAC19\uC740 {0}\uAC1C \uC0D8\uD50C\uC785\uB2C8\uB2E4.";
            map["{0} samples in the file and {1} recorded."] = "\uD30C\uC77C\uC5D0\uB294 {0}\uAC1C, \uB179\uC74C\uD55C \uC6D0\uBCF8\uC5D0\uB294 {1}\uAC1C\uC758 \uC0D8\uD50C\uC774 \uC788\uC2B5\uB2C8\uB2E4.";
            map["Original sound identical: "] = "\uC624\uB514\uC624 \uC6D0\uBCF8\uACFC \uC77C\uCE58 \uC5EC\uBD80: ";
            map["The bits are exactly the recorded ones (SHA-256)."] = "\uB179\uC74C\uD55C \uC6D0\uBCF8\uACFC \uBE44\uD2B8 \uB2E8\uC704\uB85C \uC77C\uCE58\uD569\uB2C8\uB2E4(SHA-256).";
            map["The bits do not match the recording."] = "\uB179\uC74C\uD55C \uC6D0\uBCF8\uACFC \uBE44\uD2B8\uAC00 \uC77C\uCE58\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";

            // Duplication
            map["monitors on two graphics cards"] = "\uBAA8\uB2C8\uD130\uAC00 \uC11C\uB85C \uB2E4\uB978 \uADF8\uB798\uD53D \uCE74\uB4DC\uC5D0 \uC5F0\uACB0\uB428";
            map["no monitors"] = "\uBAA8\uB2C8\uD130 \uC5C6\uC74C";
            map["DXGI 1.2 not available"] = "DXGI 1.2\uB97C \uC0AC\uC6A9\uD560 \uC218 \uC5C6\uC74C";
            map["rotated monitor"] = "\uD68C\uC804\uB41C \uBAA8\uB2C8\uD130";
            map["format "] = "\uD615\uC2DD ";

            // FfmpegSetup
            map["Record video and GIF"] = "\uB3D9\uC601\uC0C1 \uBC0F GIF \uB179\uD654";
            map["To record and play video, Stackshot uses FFmpeg {0}, a free open-source program under its own license (GPL). It is downloaded once (about 110 MB) "] = "Stackshot\uC740 \uB3D9\uC601\uC0C1\uC744 \uB179\uD654\uD558\uACE0 \uC7AC\uC0DD\uD560 \uB54C GPL\uB85C \uBC30\uD3EC\uB418\uB294 \uBB34\uB8CC \uC624\uD508 \uC18C\uC2A4 \uD504\uB85C\uADF8\uB7A8 FFmpeg {0}\uC744(\uB97C) \uC0AC\uC6A9\uD569\uB2C8\uB2E4. \uCD5C\uCD08 \uD55C \uBC88 GitHub\uC5D0\uC11C \uC57D 110MB\uB97C \uB0B4\uB824\uBC1B\uC544 ";
            map["from GitHub, verified to be the original, and saved only for your user account."] = "\uC6D0\uBCF8\uC778\uC9C0 \uAC80\uC99D\uD55C \uB4A4 \uD604\uC7AC \uC0AC\uC6A9\uC790 \uACC4\uC815\uC5D0\uB9CC \uC800\uC7A5\uD569\uB2C8\uB2E4.";
            map["Download"] = "\uB2E4\uC6B4\uB85C\uB4DC";
            map["I already have it\u2026"] = "\uAE30\uC874 \uD30C\uC77C \uC120\uD0DD\u2026";
            map["Choose an ffmpeg.exe you already have on your PC"] = "PC\uC5D0 \uC788\uB294 ffmpeg.exe \uC120\uD0DD";
            map["Where is ffmpeg.exe?"] = "ffmpeg.exe \uC704\uCE58 \uC120\uD0DD";
            map["Checking {0}\u2026"] = "{0} \uD655\uC778 \uC911\u2026";
            map["That file does not respond as FFmpeg. Choose the ffmpeg.exe of a full version."] = "\uC120\uD0DD\uD55C \uD30C\uC77C\uC774 FFmpeg\uB85C \uC751\uB2F5\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4. \uC815\uC2DD \uC804\uCCB4 \uBC84\uC804\uC5D0 \uD3EC\uD568\uB41C ffmpeg.exe\uB97C \uC120\uD0DD\uD558\uC138\uC694.";
            map["Need about 400 MB free on {0} to download and prepare FFmpeg."] = "FFmpeg\uB97C \uB0B4\uB824\uBC1B\uC544 \uC124\uCE58\uD558\uB824\uBA74 {0}\uC5D0 \uC57D 400MB\uC758 \uC5EC\uC720 \uACF5\uAC04\uC774 \uD544\uC694\uD569\uB2C8\uB2E4.";
            map["Connecting\u2026"] = "\uC5F0\uACB0 \uC911\u2026";
            map["{0} MB of {1} MB"] = "{1}MB \uC911 {0}MB";
            map["The download stalled for more than {0} s. Check your connection and try again."] = "{0}\uCD08 \uB118\uAC8C \uB2E4\uC6B4\uB85C\uB4DC\uAC00 \uC9C4\uD589\uB418\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4. \uC5F0\uACB0 \uC0C1\uD0DC\uB97C \uD655\uC778\uD55C \uB4A4 \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694.";
            map["Verifying authenticity and setting up\u2026"] = "\uC6D0\uBCF8 \uD655\uC778 \uBC0F \uC124\uCE58 \uC911\u2026";
            map["The downloaded file is not original and was discarded for security. Try again."] = "\uB0B4\uB824\uBC1B\uC740 \uD30C\uC77C\uC774 \uC6D0\uBCF8\uACFC \uB2EC\uB77C \uD3D0\uAE30\uD588\uC2B5\uB2C8\uB2E4. \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694.";
            map["The downloaded package does not contain ffmpeg.exe."] = "\uB0B4\uB824\uBC1B\uC740 \uD328\uD0A4\uC9C0\uC5D0 ffmpeg.exe\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["No connection to download server. Check Internet or ask if your network allows it."] = "\uB2E4\uC6B4\uB85C\uB4DC \uC11C\uBC84\uC5D0 \uC5F0\uACB0\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. \uC778\uD130\uB137 \uC5F0\uACB0\uACFC \uB124\uD2B8\uC6CC\uD06C\uC758 \uC811\uC18D \uD5C8\uC6A9 \uC5EC\uBD80\uB97C \uD655\uC778\uD558\uC138\uC694.";
            map["The download timed out. Try again."] = "\uB2E4\uC6B4\uB85C\uB4DC \uC2DC\uAC04\uC774 \uCD08\uACFC\uB418\uC5C8\uC2B5\uB2C8\uB2E4. \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694.";
            map["Could not establish a secure connection (perhaps a corporate proxy). You can download FFmpeg yourself and pick it with \u201CI already have it\u2026\u201D."] = "\uBCF4\uC548 \uC5F0\uACB0\uC744 \uC124\uC815\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. \uD68C\uC0AC \uD504\uB85D\uC2DC\uAC00 \uC6D0\uC778\uC77C \uC218 \uC788\uC2B5\uB2C8\uB2E4. FFmpeg\uB97C \uC9C1\uC811 \uB0B4\uB824\uBC1B\uC740 \uB4A4 \u2018\uAE30\uC874 \uD30C\uC77C \uC120\uD0DD\u2026\u2019\uC5D0\uC11C \uC5EC\uC138\uC694.";
            map["Your network proxy requires username and password. You can download FFmpeg yourself and pick it with \u201CI already have it\u2026\u201D."] = "\uB124\uD2B8\uC6CC\uD06C \uD504\uB85D\uC2DC\uC5D0 \uC0AC\uC6A9\uC790 \uC774\uB984\uACFC \uC554\uD638\uAC00 \uD544\uC694\uD569\uB2C8\uB2E4. FFmpeg\uB97C \uC9C1\uC811 \uB0B4\uB824\uBC1B\uC740 \uB4A4 \u2018\uAE30\uC874 \uD30C\uC77C \uC120\uD0DD\u2026\u2019\uC5D0\uC11C \uC5EC\uC138\uC694.";
            map["Could not save download: disk might be full."] = "\uB2E4\uC6B4\uB85C\uB4DC\uD55C \uD30C\uC77C\uC744 \uC800\uC7A5\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. \uB514\uC2A4\uD06C \uACF5\uAC04\uC744 \uD655\uC778\uD558\uC138\uC694.";
            map["Could not download FFmpeg. Try again."] = "FFmpeg\uB97C \uB0B4\uB824\uBC1B\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694.";

            // Hotkeys
            map["No shortcut"] = "\uB2E8\uCD95\uD0A4 \uC5C6\uC74C";
            map["Clear"] = "\uC9C0\uC6B0\uAE30";
            map["{0} cannot be a shortcut"] = "{0} \uD0A4\uB294 \uB2E8\uCD95\uD0A4\uB85C \uC9C0\uC815\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4";
            map["Alt + Tab switches windows"] = "Alt + Tab\uC740 \uCC3D \uC804\uD658\uC5D0 \uC0AC\uC6A9\uB429\uB2C8\uB2E4";
            map["Ctrl + Tab switches tabs"] = "Ctrl + Tab\uC740 \uD0ED \uC804\uD658\uC5D0 \uC0AC\uC6A9\uB429\uB2C8\uB2E4";
            map["Print"] = "\uC778\uC1C4";
            map["New tab"] = "\uC0C8 \uD0ED";
            map["Context menu"] = "\uBC14\uB85C \uAC00\uAE30 \uBA54\uB274";
            map["Close window"] = "\uCC3D \uB2EB\uAE30";
            map["Window menu"] = "\uCC3D \uBA54\uB274";
            map["Back"] = "\uB4A4\uB85C";
            map["Forward"] = "\uC55E\uC73C\uB85C";
            map["{0} is already {1}"] = "{0}\uC740(\uB294) \uC774\uBBF8 {1}\uC5D0 \uC0AC\uC6A9\uB429\uB2C8\uB2E4";
            map["Combine with Ctrl or Alt"] = "Ctrl \uB610\uB294 Alt\uC640 \uD568\uAED8 \uC0AC\uC6A9\uD558\uC138\uC694";
            map["Used while typing: try another"] = "\uC785\uB825\uC5D0 \uC0AC\uC6A9\uD558\uB294 \uD0A4\uC785\uB2C8\uB2E4. \uB2E4\uB978 \uD0A4\uB97C \uC120\uD0DD\uD558\uC138\uC694";
            map["This types \u201C{0}\u201D: try another"] = "\uC774 \uD0A4\uB294 \u2018{0}\u2019 \uC785\uB825\uC5D0 \uC0AC\uC6A9\uB429\uB2C8\uB2E4. \uB2E4\uB978 \uD0A4\uB97C \uC120\uD0DD\uD558\uC138\uC694";

            // MicMeterView
            map["Test microphone"] = "\uB9C8\uC774\uD06C \uD14C\uC2A4\uD2B8";
            map["Microphone level"] = "\uB9C8\uC774\uD06C \uC785\uB825 \uB808\uBCA8";
            map["Clipping"] = "\uD074\uB9AC\uD551";
            map["Opening microphone\u2026"] = "\uB9C8\uC774\uD06C \uC5EC\uB294 \uC911\u2026";
            map["No microphone"] = "\uB9C8\uC774\uD06C \uC5C6\uC74C";
            map["Microphone not responding"] = "\uB9C8\uC774\uD06C\uAC00 \uC751\uB2F5\uD558\uC9C0 \uC54A\uC74C";
            map["Windows blocked microphone access"] = "Windows\uC5D0\uC11C \uB9C8\uC774\uD06C \uC811\uADFC\uC744 \uCC28\uB2E8\uD568";
            map["Currently recording"] = "\uB179\uD654 \uC911";
            map["Stop testing"] = "\uD14C\uC2A4\uD2B8 \uC911\uC9C0";
            map["Microphone: "] = "\uB9C8\uC774\uD06C: ";
            map["Enable it in Settings > Privacy & security > Microphone."] = "\uC124\uC815 > \uAC1C\uC778 \uC815\uBCF4 \uBC0F \uBCF4\uC548 > \uB9C8\uC774\uD06C\uC5D0\uC11C \uC0AC\uC6A9\uC744 \uD5C8\uC6A9\uD558\uC138\uC694.";
            map["While recording, audio levels are shown on the recording bar."] = "\uB179\uD654 \uC911\uC5D0\uB294 \uB179\uD654 \uB9C9\uB300\uC5D0 \uC624\uB514\uC624 \uB808\uBCA8\uC774 \uD45C\uC2DC\uB429\uB2C8\uB2E4.";

            // Ocr
            map["This Windows does not have text recognition (OCR) installed."] = "\uC774 Windows\uC5D0\uB294 \uD14D\uC2A4\uD2B8 \uC778\uC2DD(OCR)\uC774 \uC124\uCE58\uB418\uC5B4 \uC788\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
            map["Not enough memory to read such a large image."] = "\uC774\uBBF8\uC9C0\uAC00 \uB108\uBB34 \uCEE4\uC11C \uC77D\uC744 \uBA54\uBAA8\uB9AC\uAC00 \uBD80\uC871\uD569\uB2C8\uB2E4.";
            map["Could not read the image."] = "\uC774\uBBF8\uC9C0\uB97C \uC77D\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
            map["Could not read the text: "] = "\uD14D\uC2A4\uD2B8\uB97C \uC77D\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4: ";

            // PinWindow
            map["Copy text"] = "\uD14D\uC2A4\uD2B8 \uBCF5\uC0AC";
            map["Actual size"] = "\uC2E4\uC81C \uD06C\uAE30";
            map["Image copied"] = "\uC774\uBBF8\uC9C0 \uBCF5\uC0AC\uB428";
            map["Ready to paste anywhere."] = "\uC5B4\uB514\uB4E0 \uBC14\uB85C \uBD99\uC5EC\uB123\uC744 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            map["PNG Image|*.png"] = "PNG \uC774\uBBF8\uC9C0|*.png";
            map["Image saved"] = "\uC774\uBBF8\uC9C0 \uC800\uC7A5\uB428";
            map["Could not save image.\n\n"] = "\uC774\uBBF8\uC9C0\uB97C \uC800\uC7A5\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.\n\n";

            // RecordBar
            map["Discard?"] = "\uBC84\uB9B4\uAE4C\uC694?";
            map["Cancel?"] = "\uCDE8\uC18C\uD560\uAE4C\uC694?";
            map["Paused"] = "\uC77C\uC2DC \uC815\uC9C0\uB428";
            map["Pause"] = "\uC77C\uC2DC \uC815\uC9C0";
            map["Countdown\u2026"] = "\uCE74\uC6B4\uD2B8\uB2E4\uC6B4\u2026";
            map["Preparing\u2026"] = "\uC900\uBE44 \uC911\u2026";
            map["Saving\u2026"] = "\uC800\uC7A5 \uC911\u2026";
            map["Preserving recording\u2026"] = "\uB179\uD654 \uBCF5\uAD6C\uBCF8 \uBCF4\uAD00 \uC911\u2026";
            map["Saved"] = "\uC800\uC7A5\uB428";
            map["Stop"] = "\uC911\uC9C0";
            map["Recording bar"] = "\uB179\uD654 \uB9C9\uB300";
            map["Recording"] = "\uB179\uD654";
            map["Saving recording"] = "\uB179\uD654 \uC800\uC7A5 \uC911";
            map["Recording saved"] = "\uB179\uD654 \uC800\uC7A5\uB428";
            map["Stop and save"] = "\uC911\uC9C0\uD558\uACE0 \uC800\uC7A5";
            map["Discard (press again to confirm)"] = "\uB179\uD654 \uBC84\uB9AC\uAE30(\uB2E4\uC2DC \uB20C\uB7EC \uD655\uC778)";
            map["Unmute microphone"] = "\uB9C8\uC774\uD06C \uC74C\uC18C\uAC70 \uD574\uC81C";
            map["Mute microphone"] = "\uB9C8\uC774\uD06C \uC74C\uC18C\uAC70";
            map["Unmute system audio"] = "\uC2DC\uC2A4\uD15C \uC18C\uB9AC \uC74C\uC18C\uAC70 \uD574\uC81C";
            map["Mute system audio"] = "\uC2DC\uC2A4\uD15C \uC18C\uB9AC \uC74C\uC18C\uAC70";

            // Recorder
            map["Could not start recording: "] = "\uB179\uD654\uB97C \uC2DC\uC791\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4: ";
            map["{0} free on disk"] = "\uB514\uC2A4\uD06C \uC5EC\uC720 \uACF5\uAC04 {0}";
            map["{0}: not even one minute fits in {1}.\n\nFree up space or choose a lighter quality."] = "{0}: {1}\uC5D0\uB294 1\uBD84 \uBD84\uB7C9\uB3C4 \uC800\uC7A5\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.\n\n\uACF5\uAC04\uC744 \uD655\uBCF4\uD558\uAC70\uB098 \uB0AE\uC740 \uD654\uC9C8\uC744 \uC120\uD0DD\uD558\uC138\uC694.";
            map["{0}: {1} holds {2}, depending on motion."] = "{0}: \uC6C0\uC9C1\uC784\uC5D0 \uB530\uB77C {1}\uC5D0 {2} \uBD84\uB7C9\uC744 \uC800\uC7A5\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            map["recording stops automatically before disk is full and keeps what was recorded"] = "\uB514\uC2A4\uD06C\uAC00 \uAC00\uB4DD \uCC28\uAE30 \uC804\uC5D0 \uC790\uB3D9\uC73C\uB85C \uB179\uD654\uB97C \uBA48\uCD94\uACE0 \uADF8\uB54C\uAE4C\uC9C0\uC758 \uC601\uC0C1\uC744 \uC800\uC7A5\uD569\uB2C8\uB2E4";
            map["\n\nOK: record anyway ({0}).\nCancel: do not record."] = "\n\n\uD655\uC778: \uADF8\uB300\uB85C \uB179\uD654\uD569\uB2C8\uB2E4({0}).\n\uCDE8\uC18C: \uB179\uD654\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
            map["\n\nYes: record in {0} (holds {1}).\nNo: record in {2} anyway ({3}).\nCancel: do not record."] = "\n\n\uC608: {0} \uD654\uC9C8\uB85C \uB179\uD654\uD569\uB2C8\uB2E4({1} \uBD84\uB7C9).\n\uC544\uB2C8\uC694: {2} \uD654\uC9C8\uB85C \uB179\uD654\uD569\uB2C8\uB2E4({3}).\n\uCDE8\uC18C: \uB179\uD654\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
            map["Computer was locked"] = "\uCEF4\uD4E8\uD130\uAC00 \uC7A0\uACBC\uC2B5\uB2C8\uB2E4";
            map["Computer is suspending"] = "\uCEF4\uD4E8\uD130\uAC00 \uC808\uC804 \uBAA8\uB4DC\uB85C \uC804\uD658\uB429\uB2C8\uB2E4";
            map["Low disk space"] = "\uB514\uC2A4\uD06C \uACF5\uAC04 \uBD80\uC871";
            map["You cancelled the save."] = "\uC800\uC7A5\uC744 \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4.";
            map["The conversion got stuck (FFmpeg stopped making progress)."] = "\uBCC0\uD658\uC774 \uBA48\uCDC4\uC2B5\uB2C8\uB2E4(FFmpeg\uAC00 \uC751\uB2F5\uD558\uC9C0 \uC54A\uC74C).";
            map["under 1 min"] = "1\uBD84 \uBBF8\uB9CC";
            map["many hours"] = "\uC218 \uC2DC\uAC04 \uC774\uC0C1";
            map["plug in your laptop"] = "\uB178\uD2B8\uBD81 \uC804\uC6D0 \uC5F0\uACB0";
            map["lower GIF frame rate"] = "GIF \uD504\uB808\uC784 \uC18D\uB3C4 \uB0AE\uCD94\uAE30";
            map["lower to 30 fps"] = "30 fps\uB85C \uB0AE\uCD94\uAE30";
            map["record a smaller area"] = "\uC791\uC740 \uC601\uC5ED\uB9CC \uB179\uD654";
            map["use High quality"] = "\uACE0\uD654\uC9C8 \uC0AC\uC6A9";
            map["close unused apps"] = "\uC0AC\uC6A9\uD558\uC9C0 \uC54A\uB294 \uC571 \uB2EB\uAE30";
            map["Windows does not allow screen capture right now (locked screen or a security prompt). Try again from the desktop."] = "Windows\uC5D0\uC11C \uD604\uC7AC \uD654\uBA74 \uCEA1\uCC98\uB97C \uD5C8\uC6A9\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4. \uC7A0\uAE08\uC744 \uD574\uC81C\uD558\uAC70\uB098 \uBCF4\uC548 \uBA54\uC2DC\uC9C0\uB97C \uB2EB\uACE0 \uBC14\uD0D5 \uD654\uBA74\uC5D0\uC11C \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694.";
            map["Could not start FFmpeg: "] = "FFmpeg\uB97C \uC2DC\uC791\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4: ";
            map["no Desktop Duplication"] = "\uB370\uC2A4\uD06C\uD1B1 \uBCF5\uC81C \uAE30\uB2A5 \uC5C6\uC74C";
            map["This area cannot be recorded with Windows screen capture ({0}): fallback (GDI) would be used, which cannot see hardware-accelerated content (videos, games, some apps) and may not be frame-accurate.\n\nRecord in Cinema mode anyway?"] = "Windows \uD654\uBA74 \uCEA1\uCC98\uB85C \uC774 \uC601\uC5ED\uC744 \uB179\uD654\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4({0}). \uB300\uC2E0 GDI\uB97C \uC0AC\uC6A9\uD558\uBA74 \uD558\uB4DC\uC6E8\uC5B4 \uAC00\uC18D \uCF58\uD150\uCE20(\uB3D9\uC601\uC0C1, \uAC8C\uC784, \uC77C\uBD80 \uC571)\uAC00 \uBCF4\uC774\uC9C0 \uC54A\uC73C\uBA70 \uD504\uB808\uC784\uC774 \uC815\uD655\uD558\uC9C0 \uC54A\uC744 \uC218 \uC788\uC2B5\uB2C8\uB2E4.\n\n\uADF8\uB798\uB3C4 \uC2DC\uB124\uB9C8 \uBAA8\uB4DC\uB85C \uB179\uD654\uD560\uAE4C\uC694?";
            map[" and the other runs at a different refresh rate, so its portion may appear choppy."] = "\uC774\uBA70 \uB2E4\uB978 \uBAA8\uB2C8\uD130\uB294 \uC8FC\uC0AC\uC728\uC774 \uB2EC\uB77C \uD574\uB2F9 \uC601\uC5ED\uC774 \uB04A\uACA8 \uBCF4\uC77C \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            map["; if one runs at a different rate, its portion may appear choppy."] = "\uC785\uB2C8\uB2E4. \uC8FC\uC0AC\uC728\uC774 \uB2E4\uB978 \uBAA8\uB2C8\uD130\uC758 \uC601\uC5ED\uC740 \uB04A\uACA8 \uBCF4\uC77C \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            map["The area spans {0} monitors. Frames match the primary display rate ({1} Hz){2}\n\nRecord in Cinema mode anyway?"] = "\uC601\uC5ED\uC774 \uBAA8\uB2C8\uD130 {0}\uB300\uC5D0 \uAC78\uCCD0 \uC788\uC2B5\uB2C8\uB2E4. \uB179\uD654 \uAE30\uC900\uC740 \uC8FC \uBAA8\uB2C8\uD130\uC758 \uC8FC\uC0AC\uC728({1} Hz){2}\n\n\uADF8\uB798\uB3C4 \uC2DC\uB124\uB9C8 \uBAA8\uB4DC\uB85C \uB179\uD654\uD560\uAE4C\uC694?";
            map["Could not open camera."] = "\uCE74\uBA54\uB77C\uB97C \uC5F4\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
            map["Camera timed out."] = "\uCE74\uBA54\uB77C \uC751\uB2F5 \uC2DC\uAC04\uC774 \uCD08\uACFC\uB418\uC5C8\uC2B5\uB2C8\uB2E4.";
            map["Recording without part of audio"] = "\uC77C\uBD80 \uC624\uB514\uC624 \uC5C6\uC774 \uB179\uD654 \uC911";
            map["Recording without camera"] = "\uCE74\uBA54\uB77C \uC5C6\uC774 \uB179\uD654 \uC911";
            map["The computer could not keep up with the screen"] = "\uCEF4\uD4E8\uD130\uAC00 \uD654\uBA74\uC758 \uBCC0\uD654 \uC18D\uB3C4\uB97C \uB530\uB77C\uAC00\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4";
            map["Screen capture failed: "] = "\uD654\uBA74 \uCEA1\uCC98 \uC2E4\uD328: ";
            map["No free memory left to keep recording losslessly"] = "\uBB34\uC190\uC2E4 \uB179\uD654\uB97C \uACC4\uC18D\uD560 \uBA54\uBAA8\uB9AC\uAC00 \uBD80\uC871\uD569\uB2C8\uB2E4";
            map["The computer could not encode losslessly at the screen's pace"] = "\uCEF4\uD4E8\uD130\uAC00 \uD654\uBA74\uC758 \uBCC0\uD654 \uC18D\uB3C4\uC5D0 \uB9DE\uCDB0 \uBB34\uC190\uC2E4 \uC778\uCF54\uB529\uC744 \uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4";
            map["The screen's resolution or orientation changed"] = "\uD654\uBA74 \uD574\uC0C1\uB3C4 \uB610\uB294 \uBC29\uD5A5\uC774 \uBC14\uB00C\uC5C8\uC2B5\uB2C8\uB2E4";
            map["This computer cannot hide the recording bar: it will appear in the video if inside the area."] = "\uC774 PC\uC5D0\uC11C\uB294 \uB179\uD654 \uB9C9\uB300\uB97C \uC228\uAE38 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. \uB9C9\uB300\uAC00 \uB179\uD654 \uC601\uC5ED \uC548\uC5D0 \uC788\uC73C\uBA74 \uC601\uC0C1\uC5D0 \uD45C\uC2DC\uB429\uB2C8\uB2E4.";
            map["Recording with fallback method: may be slower and not capture videos, games or some apps."] = "\uB300\uCCB4 \uBC29\uC2DD\uC73C\uB85C \uB179\uD654\uD569\uB2C8\uB2E4. \uC18D\uB3C4\uAC00 \uB290\uB9AC\uAC70\uB098 \uB3D9\uC601\uC0C1, \uAC8C\uC784, \uC77C\uBD80 \uC571\uC774 \uB179\uD654\uB418\uC9C0 \uC54A\uC744 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            map["Windows is not allowing mouse and keyboard tracking right now: video will be recorded without clicks or keystrokes."] = "\uD604\uC7AC Windows\uC5D0\uC11C \uB9C8\uC6B0\uC2A4\uC640 \uD0A4\uBCF4\uB4DC \uC785\uB825 \uAC10\uC9C0\uB97C \uD5C8\uC6A9\uD558\uC9C0 \uC54A\uC544 \uD074\uB9AD\uACFC \uD0A4 \uC785\uB825\uC774 \uC601\uC0C1\uC5D0 \uD45C\uC2DC\uB418\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
            map["Recording with limitations"] = "\uC81C\uD55C\uB41C \uAE30\uB2A5\uC73C\uB85C \uB179\uD654 \uC911";
            map["system audio"] = "\uC2DC\uC2A4\uD15C \uC18C\uB9AC";
            map["microphone"] = "\uB9C8\uC774\uD06C";
            map["System audio restored"] = "\uC2DC\uC2A4\uD15C \uC18C\uB9AC \uBCF5\uAD6C\uB428";
            map["Microphone restored"] = "\uB9C8\uC774\uD06C \uBCF5\uAD6C\uB428";
            map["Recording resumed from \u201C{0}\u201D."] = "\u2018{0}\u2019\uC5D0\uC11C \uB179\uC74C\uC744 \uC7AC\uAC1C\uD588\uC2B5\uB2C8\uB2E4.";
            map["Selected audio output is unavailable"] = "\uC120\uD0DD\uD55C \uC624\uB514\uC624 \uCD9C\uB825\uC744 \uC0AC\uC6A9\uD560 \uC218 \uC5C6\uC74C";
            map["Selected microphone disconnected"] = "\uC120\uD0DD\uD55C \uB9C8\uC774\uD06C \uC5F0\uACB0 \uB04A\uAE40";
            map["Recording with \u201C{0}\u201D, default Windows device, until it returns."] = "\uC7A5\uCE58\uAC00 \uB2E4\uC2DC \uC5F0\uACB0\uB420 \uB54C\uAE4C\uC9C0 Windows \uAE30\uBCF8 \uC7A5\uCE58 \u2018{0}\u2019(\uC73C)\uB85C \uB179\uC74C\uD569\uB2C8\uB2E4.";
            map["No system audio"] = "\uC2DC\uC2A4\uD15C \uC18C\uB9AC \uC5C6\uC74C";
            map["No device available: this part will record without {0}."] = "\uC0AC\uC6A9\uD560 \uC218 \uC788\uB294 \uC7A5\uCE58\uAC00 \uC5C6\uC5B4 \uC774 \uAD6C\uAC04\uC740 {0} \uC5C6\uC774 \uB179\uD654\uD569\uB2C8\uB2E4.";
            map["FFmpeg closed during the recording. "] = "\uB179\uD654 \uC911 FFmpeg\uAC00 \uC885\uB8CC\uB418\uC5C8\uC2B5\uB2C8\uB2E4. ";
            map["Could not save the recording: "] = "\uB179\uD654\uB97C \uC800\uC7A5\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4: ";
            map["FFmpeg stopped responding."] = "FFmpeg\uAC00 \uC751\uB2F5\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
            map["FFmpeg did not finish saving the video."] = "FFmpeg\uAC00 \uB3D9\uC601\uC0C1 \uC800\uC7A5\uC744 \uB9C8\uCE58\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
            map["FFmpeg could not save the video. "] = "FFmpeg\uAC00 \uB3D9\uC601\uC0C1\uC744 \uC800\uC7A5\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. ";
            map["No frames were recorded."] = "\uB179\uD654\uD55C \uD504\uB808\uC784\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
            map["Computer sound"] = "\uC2DC\uC2A4\uD15C \uC18C\uB9AC";
            map[". The recording keeps it as it is; if it sounds distorted, turn that source down before recording."] = ". \uC6D0\uB798 \uC18C\uB9AC \uADF8\uB300\uB85C \uB179\uC74C\uB429\uB2C8\uB2E4. \uC18C\uB9AC\uAC00 \uCC0C\uADF8\uB7EC\uC9C0\uBA74 \uB2E4\uC74C \uB179\uD654 \uC804\uC5D0 \uD574\uB2F9 \uC18C\uC2A4\uC758 \uBCFC\uB968\uC744 \uB0AE\uCD94\uC138\uC694.";
            map[" overlapping samples discarded. "] = "\uAC1C\uC758 \uACB9\uCE58\uB294 \uC0D8\uD50C\uC744 \uBC84\uB838\uC2B5\uB2C8\uB2E4. ";
            map["Sound clock adjustment: {0} samples duplicated and {1} removed. "] = "\uC624\uB514\uC624 \uC2DC\uAC04 \uBCF4\uC815: {0}\uAC1C \uC0D8\uD50C\uC744 \uBCF5\uC81C\uD558\uACE0 {1}\uAC1C\uB97C \uC81C\uAC70\uD588\uC2B5\uB2C8\uB2E4. ";
            map["The device does not deliver 48 kHz floating-point stereo: Stackshot converted it (not an exact copy). "] = "\uC7A5\uCE58\uAC00 48 kHz \uBD80\uB3D9\uC18C\uC218\uC810 \uC2A4\uD14C\uB808\uC624\uB97C \uCD9C\uB825\uD558\uC9C0 \uC54A\uC544 Stackshot\uC5D0\uC11C \uBCC0\uD658\uD588\uC2B5\uB2C8\uB2E4. \uC6D0\uBCF8\uACFC \uBE44\uD2B8 \uB2E8\uC704\uB85C \uC77C\uCE58\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4. ";
            map[" s of silence inserted (gaps, or nothing playing)."] = "\uCD08\uC758 \uBB34\uC74C\uC744 \uC0BD\uC785\uD588\uC2B5\uB2C8\uB2E4(\uC785\uB825\uC774 \uC5C6\uAC70\uB098 \uC7AC\uC0DD\uB418\uC9C0 \uC54A\uC740 \uAD6C\uAC04).";
            map["system audio "] = "\uC2DC\uC2A4\uD15C \uC18C\uB9AC ";
            map["microphone "] = "\uB9C8\uC774\uD06C ";
            map["Note: "] = "\uCC38\uACE0: ";
            map["Saving lossless master\u2026"] = "\uBB34\uC190\uC2E4 \uB9C8\uC2A4\uD130 \uC800\uC7A5 \uC911\u2026";
            map["Could not create the lossless master. "] = "\uBB34\uC190\uC2E4 \uB9C8\uC2A4\uD130 \uD30C\uC77C\uC744 \uB9CC\uB4E4\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. ";
            map["Verifying lossless capture\u2026"] = "\uBB34\uC190\uC2E4 \uCEA1\uCC98 \uAC80\uC99D \uC911\u2026";
            map["You cancelled the check: the file was saved unverified as \u201C{0}\u201D."] = "\uAC80\uC99D\uC744 \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4. \uAC80\uC99D\uD558\uC9C0 \uC54A\uC740 \uD30C\uC77C\uC744 \u2018{0}\u2019\uC5D0 \uC800\uC7A5\uD588\uC2B5\uB2C8\uB2E4.";
            map["The check found differences: the file was saved as \u201C{0}\u201D."] = "\uAC80\uC99D \uACB0\uACFC \uCC28\uC774\uAC00 \uC788\uC2B5\uB2C8\uB2E4. \uD30C\uC77C\uC744 \u2018{0}\u2019\uC5D0 \uC800\uC7A5\uD588\uC2B5\uB2C8\uB2E4.";
            map["What was recorded has not been deleted."] = "\uB179\uD654\uD55C \uC601\uC0C1\uC740 \uBCF4\uC874\uB418\uC5B4 \uC788\uC2B5\uB2C8\uB2E4.";
            map[" It is in:\n"] = " \uBCF4\uAD00 \uC704\uCE58:\n";
            map["\nCould not be moved and is still where it was:\n"] = "\n\uC62E\uAE30\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. \uC6D0\uB798 \uC704\uCE58\uC5D0 \uBCF4\uAD00\uB429\uB2C8\uB2E4:\n";
            map["Could not save the recording."] = "\uB179\uD654\uB97C \uC800\uC7A5\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
            map["Creating GIF\u2026"] = "GIF \uB9CC\uB4DC\uB294 \uC911\u2026";
            map["Could not create GIF."] = "GIF\uB97C \uB9CC\uB4E4\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
            map["Saving in maximum quality\u2026"] = "\uCD5C\uACE0 \uD654\uC9C8\uB85C \uC800\uC7A5 \uC911\u2026";
            map["Adding audio\u2026"] = "\uC624\uB514\uC624 \uCD94\uAC00 \uC911\u2026";
            map["Could not save video."] = "\uB3D9\uC601\uC0C1\uC744 \uC800\uC7A5\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
            map["\n\nOpen folder?"] = "\n\n\uD3F4\uB354\uB97C \uC5F4\uAE4C\uC694?";
            map["Recording stopped"] = "\uB179\uD654 \uC911\uC9C0\uB428";
            map[": what was recorded up to that point was saved."] = ". \uC911\uC9C0 \uC2DC\uC810\uAE4C\uC9C0\uC758 \uC601\uC0C1\uC744 \uC800\uC7A5\uD588\uC2B5\uB2C8\uB2E4.";
            map["Recording was choppy"] = "\uB179\uD654 \uC911 \uD504\uB808\uC784 \uB04A\uAE40";
            map["Audio clipped"] = "\uC624\uB514\uC624 \uD074\uB9AC\uD551 \uBC1C\uC0DD";
            map[" If it sounds distorted, lower volume for that source before recording."] = " \uC18C\uB9AC\uAC00 \uCC0C\uADF8\uB7EC\uC9C0\uBA74 \uB2E4\uC74C \uB179\uD654 \uC804\uC5D0 \uD574\uB2F9 \uC18C\uC2A4\uC758 \uBCFC\uB968\uC744 \uB0AE\uCD94\uC138\uC694.";
            map["{0} frames identical to captured. To share, open and export to MP4 (that copy compresses)."] = "{0}\uAC1C \uD504\uB808\uC784\uC774 \uC6D0\uBCF8\uACFC \uC77C\uCE58\uD569\uB2C8\uB2E4. \uACF5\uC720\uD558\uB824\uBA74 \uD30C\uC77C\uC744 \uC5F4\uACE0 MP4\uB85C \uB0B4\uBCF4\uB0B4\uC138\uC694. \uB0B4\uBCF4\uB0B8 \uC0AC\uBCF8\uC740 \uC555\uCD95\uB429\uB2C8\uB2E4.";
            map["Cinema: lossless, verified"] = "\uC2DC\uB124\uB9C8: \uBB34\uC190\uC2E4 \uAC80\uC99D \uC644\uB8CC";
            map["\n\nTry recording at 30 fps, smaller area or more memory for motion peaks (Recording > Cinema)."] = "\n\n30 fps\uB85C \uB179\uD654\uD558\uAC70\uB098 \uC601\uC5ED\uC744 \uC904\uC5EC \uBCF4\uC138\uC694. \uB179\uD654 > \uC2DC\uB124\uB9C8\uC5D0\uC11C \uC6C0\uC9C1\uC784\uC774 \uB9CE\uC740 \uAD6C\uAC04\uC5D0 \uC0AC\uC6A9\uD560 \uBA54\uBAA8\uB9AC\uB97C \uB298\uB9B4 \uC218\uB3C4 \uC788\uC2B5\uB2C8\uB2E4.";
            map["Standard"] = "\uD45C\uC900";
            map["Maximum"] = "\uCD5C\uACE0";
            map["High"] = "\uB192\uC74C";
            map["Cinema"] = "\uC2DC\uB124\uB9C8";

            // RecordSetup
            map["Very high quality while recording (lossless not supported on an area this large) and very high quality MP4 when finished; takes a moment to save."] = "\uD070 \uC601\uC5ED\uC5D0\uC11C\uB294 \uBB34\uC190\uC2E4 \uB179\uD654\uB97C \uC9C0\uC6D0\uD558\uC9C0 \uC54A\uC544 \uB9E4\uC6B0 \uB192\uC740 \uD654\uC9C8\uB85C \uB179\uD654\uD569\uB2C8\uB2E4. \uC644\uB8CC \uD6C4 \uACE0\uD654\uC9C8 MP4\uB85C \uC800\uC7A5\uD558\uB294 \uB370 \uC2DC\uAC04\uC774 \uC870\uAE08 \uAC78\uB9BD\uB2C8\uB2E4.";
            map["Record GIF"] = "GIF \uB179\uD654";
            map["Record video"] = "\uB3D9\uC601\uC0C1 \uB179\uD654";
            map["Area "] = "\uC601\uC5ED ";
            map["Quality"] = "\uD654\uC9C8";
            map["Frame rate"] = "\uD504\uB808\uC784 \uC18D\uB3C4";
            map["per second"] = "\uCD08\uB2F9";
            map["60 per second"] = "\uCD08\uB2F9 60\uD504\uB808\uC784";
            map["High and Maximum always record at 60 fps for smooth motion."] = "\uB192\uC74C \uBC0F \uCD5C\uACE0 \uD654\uC9C8\uC5D0\uC11C\uB294 \uC6C0\uC9C1\uC784\uC744 \uBD80\uB4DC\uB7FD\uAC8C \uB2F4\uAE30 \uC704\uD574 \uD56D\uC0C1 60 fps\uB85C \uB179\uD654\uD569\uB2C8\uB2E4.";
            map["System audio"] = "\uC2DC\uC2A4\uD15C \uC624\uB514\uC624";
            map["None"] = "\uC5C6\uC74C";
            map["Circle"] = "\uC6D0\uD615";
            map["Square"] = "\uC815\uC0AC\uAC01\uD615";
            map["Webcam"] = "\uC6F9\uCEA0";
            map["Countdown"] = "\uCE74\uC6B4\uD2B8\uB2E4\uC6B4";
            map["Always ask"] = "\uD56D\uC0C1 \uBB3B\uAE30";
            map["If turned off, recording starts immediately with these settings (re-enable in Recording)."] = "\uB044\uBA74 \uC774 \uC124\uC815\uC73C\uB85C \uC989\uC2DC \uB179\uD654\uD569\uB2C8\uB2E4. \uB179\uD654 \uC124\uC815\uC5D0\uC11C \uB2E4\uC2DC \uCF24 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            map["Record"] = "\uB179\uD654";
            map["Input level"] = "\uC785\uB825 \uB808\uBCA8";
            map["Esc: cancel"] = "Esc: \uCDE8\uC18C";
            map["Small files, ready to send in seconds."] = "\uD30C\uC77C\uC774 \uC791\uC544 \uBC14\uB85C \uBCF4\uB0BC \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            map["60 fps with rich detail; saves instantly."] = "60 fps\uB85C \uC120\uBA85\uD558\uAC8C \uB179\uD654\uD558\uACE0 \uC989\uC2DC \uC800\uC7A5\uD569\uB2C8\uB2E4.";
            map["Lossless while recording and very high quality MP4 when finished; takes a moment to save."] = "\uBB34\uC190\uC2E4\uB85C \uB179\uD654\uD55C \uB4A4 \uACE0\uD654\uC9C8 MP4\uB85C \uBCC0\uD658\uD569\uB2C8\uB2E4. \uC800\uC7A5\uC5D0 \uC2DC\uAC04\uC774 \uC870\uAE08 \uAC78\uB9BD\uB2C8\uB2E4.";
            map["Lossless master verified frame by frame (.mkv); takes up significant space."] = "\uD504\uB808\uC784\uB9C8\uB2E4 \uAC80\uC99D\uD558\uB294 \uBB34\uC190\uC2E4 \uB9C8\uC2A4\uD130(.mkv)\uB85C \uC800\uC7A5\uD569\uB2C8\uB2E4. \uB514\uC2A4\uD06C \uACF5\uAC04\uC774 \uB9CE\uC774 \uD544\uC694\uD569\uB2C8\uB2E4.";

            // RegionPicker
            map["Video"] = "\uB3D9\uC601\uC0C1";
            map["Text"] = "\uD14D\uC2A4\uD2B8";
            map["Drag an area or click a window  \u00B7  Then choose what to do  \u00B7  C: color  \u00B7  Shift: measure  \u00B7  Esc: cancel"] = "\uC601\uC5ED\uC744 \uB4DC\uB798\uADF8\uD558\uAC70\uB098 \uCC3D\uC744 \uD074\uB9AD\uD55C \uB4A4 \uC791\uC5C5 \uC120\uD0DD  \u00B7  C: \uC0C9\uC0C1  \u00B7  Shift: \uCE21\uC815  \u00B7  Esc: \uCDE8\uC18C";
            map["Drag an area or click a window  \u00B7  Then adjust it  \u00B7  C: color  \u00B7  Shift: measure  \u00B7  Esc: cancel"] = "\uC601\uC5ED\uC744 \uB4DC\uB798\uADF8\uD558\uAC70\uB098 \uCC3D\uC744 \uD074\uB9AD\uD55C \uB4A4 \uBC94\uC704 \uC870\uC815  \u00B7  C: \uC0C9\uC0C1  \u00B7  Shift: \uCE21\uC815  \u00B7  Esc: \uCDE8\uC18C";
            map["Drag to capture an area  \u00B7  Click: window or full screen  \u00B7  C: color  \u00B7  Shift: measure  \u00B7  Esc: cancel"] = "\uB4DC\uB798\uADF8: \uC601\uC5ED \uCEA1\uCC98  \u00B7  \uD074\uB9AD: \uCC3D \uB610\uB294 \uC804\uCCB4 \uD654\uBA74  \u00B7  C: \uC0C9\uC0C1  \u00B7  Shift: \uCE21\uC815  \u00B7  Esc: \uCDE8\uC18C";
            map["Record video  \u00B7  Drag an area or click a window  \u00B7  Esc: cancel"] = "\uB3D9\uC601\uC0C1 \uB179\uD654  \u00B7  \uC601\uC5ED\uC744 \uB4DC\uB798\uADF8\uD558\uAC70\uB098 \uCC3D\uC744 \uD074\uB9AD  \u00B7  Esc: \uCDE8\uC18C";
            map["Copy text  \u00B7  Drag over text or click a window  \u00B7  Esc: cancel"] = "\uD14D\uC2A4\uD2B8 \uBCF5\uC0AC  \u00B7  \uD14D\uC2A4\uD2B8 \uC704\uB97C \uB4DC\uB798\uADF8\uD558\uAC70\uB098 \uCC3D\uC744 \uD074\uB9AD  \u00B7  Esc: \uCDE8\uC18C";
            map["Record GIF  \u00B7  Drag an area or click a window  \u00B7  Esc: cancel"] = "GIF \uB179\uD654  \u00B7  \uC601\uC5ED\uC744 \uB4DC\uB798\uADF8\uD558\uAC70\uB098 \uCC3D\uC744 \uD074\uB9AD  \u00B7  Esc: \uCDE8\uC18C";
            map["Scrolling capture  \u00B7  Drag over the scrolling area, excluding fixed header  \u00B7  Esc: cancel"] = "\uC2A4\uD06C\uB864 \uCEA1\uCC98  \u00B7  \uACE0\uC815\uB41C \uBA38\uB9AC\uAE00\uC744 \uC81C\uC678\uD558\uACE0 \uC2A4\uD06C\uB864\uD560 \uC601\uC5ED\uC744 \uB4DC\uB798\uADF8  \u00B7  Esc: \uCDE8\uC18C";

            // RegionPicker.Tools
            map["Color copied"] = "\uC0C9\uC0C1 \uBCF5\uC0AC\uB428";
            map["Could not copy color"] = "\uC0C9\uC0C1\uC744 \uBCF5\uC0AC\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4";
            map[" is ready to paste."] = " \uC0C9\uC0C1 \uCF54\uB4DC\uB97C \uBD99\uC5EC\uB123\uC744 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            map["Clipboard is busy: please try again."] = "\uD074\uB9BD\uBCF4\uB4DC\uAC00 \uC0AC\uC6A9 \uC911\uC785\uB2C8\uB2E4. \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694.";

            // ScrollCapture
            map["The area is too small: choose one of at least 40 \u00D7 40 pixels."] = "\uC601\uC5ED\uC774 \uB108\uBB34 \uC791\uC2B5\uB2C8\uB2E4. \uCD5C\uC18C 40 \u00D7 40\uD53D\uC140 \uC774\uC0C1 \uC120\uD0DD\uD558\uC138\uC694.";
            map["The capture stopped before finishing: what was captured so far has been saved."] = "\uC644\uB8CC\uD558\uAE30 \uC804\uC5D0 \uCEA1\uCC98\uAC00 \uC911\uC9C0\uB418\uC5B4 \uC9C0\uAE08\uAE4C\uC9C0\uC758 \uB0B4\uC6A9\uC744 \uC800\uC7A5\uD588\uC2B5\uB2C8\uB2E4.";
            map["Could not compose the image: it is too large for available memory. Try a narrower or shorter area."] = "\uBA54\uBAA8\uB9AC\uAC00 \uBD80\uC871\uD574 \uC774\uBBF8\uC9C0\uB97C \uD569\uCE58\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. \uC601\uC5ED\uC758 \uB108\uBE44\uB098 \uB192\uC774\uB97C \uC904\uC5EC \uBCF4\uC138\uC694.";
            map["Could not finish capture. Please try again."] = "\uCEA1\uCC98\uB97C \uC644\uB8CC\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694.";
            map["Scrolling"] = "\uC2A4\uD06C\uB864";
            map["The area did not scroll, so it was saved as a normal capture. Choose only the part that scrolls (the list or text), without the fixed header."] = "\uC601\uC5ED\uC774 \uC6C0\uC9C1\uC774\uC9C0 \uC54A\uC544 \uC77C\uBC18 \uCEA1\uCC98\uB85C \uC800\uC7A5\uD588\uC2B5\uB2C8\uB2E4. \uACE0\uC815\uB41C \uBA38\uB9AC\uAE00\uC744 \uBE7C\uACE0 \uC2A4\uD06C\uB864\uB418\uB294 \uBAA9\uB85D\uC774\uB098 \uBCF8\uBB38\uB9CC \uC120\uD0DD\uD558\uC138\uC694.";
            map["Composing\u2026"] = "\uC774\uBBF8\uC9C0 \uD569\uCE58\uB294 \uC911\u2026";
            map["Esc again to discard"] = "Esc\uB97C \uB2E4\uC2DC \uB20C\uB7EC \uBC84\uB9AC\uAE30";
            map["You haven't scrolled \u00B7 Done again saves it"] = "\uC2A4\uD06C\uB864 \uC548 \uD568 \u00B7 \uC644\uB8CC\uB97C \uB2E4\uC2DC \uB20C\uB7EC \uC800\uC7A5";
            map["Maximum height"] = "\uCD5C\uB300 \uB192\uC774 \uB3C4\uB2EC";
            map["Downward only"] = "\uC544\uB798\uB85C\uB9CC \uC2A4\uD06C\uB864";
            map["Slow down"] = "\uB354 \uCC9C\uCC9C\uD788 \uC2A4\uD06C\uB864\uD558\uC138\uC694";
            map["Pause: stop and scroll with the wheel"] = "\uC77C\uC2DC \uC815\uC9C0: \uC790\uB3D9 \uC2A4\uD06C\uB864\uC744 \uBA48\uCD94\uACE0 \uD720\uB85C \uACC4\uC18D";
            map["Auto: scroll automatically"] = "\uC790\uB3D9: \uC790\uB3D9\uC73C\uB85C \uC2A4\uD06C\uB864";
            map["Done: save capture (Enter)"] = "\uC644\uB8CC: \uCEA1\uCC98 \uC800\uC7A5(Enter)";
            map["Cancel without saving (Esc)"] = "\uC800\uC7A5\uD558\uC9C0 \uC54A\uACE0 \uCDE8\uC18C(Esc)";
            map["Scrolling down\u2026"] = "\uC544\uB798\uB85C \uC2A4\uD06C\uB864 \uC911\u2026";
            map["Not scrolling? Click Auto"] = "\uC2A4\uD06C\uB864\uB418\uC9C0 \uC54A\uC73C\uBA74 \u2018\uC790\uB3D9\u2019\uC744 \uD074\uB9AD\uD558\uC138\uC694";
            map["Scroll with wheel \u00B7 Enter to finish"] = "\uD720\uB85C \uC2A4\uD06C\uB864 \u00B7 Enter\uB85C \uC644\uB8CC";
            map["1 screen"] = "\uD654\uBA74 1\uAC1C";
            map["Scroll slowly with the mouse wheel"] = "\uB9C8\uC6B0\uC2A4 \uD720\uB85C \uCC9C\uCC9C\uD788 \uC2A4\uD06C\uB864\uD558\uC138\uC694";
            map["Or click Auto to scroll automatically. When at the bottom, Done or Enter."] = "\u2018\uC790\uB3D9\u2019\uC744 \uB204\uB974\uBA74 \uC790\uB3D9\uC73C\uB85C \uC2A4\uD06C\uB864\uD569\uB2C8\uB2E4. \uB05D\uC5D0 \uB3C4\uB2EC\uD558\uBA74 \u2018\uC644\uB8CC\u2019\uB098 Enter\uB97C \uB204\uB974\uC138\uC694.";

            // Sensitive
            map["ID (DNI)"] = "\uC2A4\uD398\uC778 \uC2E0\uBD84\uC99D \uBC88\uD638(DNI)";
            map["Foreigner ID (NIE)"] = "\uC2A4\uD398\uC778 \uC678\uAD6D\uC778 \uBC88\uD638(NIE)";
            map["Social Security no."] = "\uC0AC\uD68C \uBCF4\uC7A5 \uBC88\uD638";
            map["card"] = "\uCE74\uB4DC \uBC88\uD638";
            map["email"] = "\uC774\uBA54\uC77C \uC8FC\uC18C";
            map["phone"] = "\uC804\uD654\uBC88\uD638";
            map["SSN"] = "\uBBF8\uAD6D \uC0AC\uD68C \uBCF4\uC7A5 \uBC88\uD638(SSN)";
            map["secret key"] = "\uBE44\uBC00 \uD0A4";
            map["IDs (DNI)"] = "\uC2A4\uD398\uC778 \uC2E0\uBD84\uC99D \uBC88\uD638(DNI)";
            map["Foreigner IDs (NIE)"] = "\uC2A4\uD398\uC778 \uC678\uAD6D\uC778 \uBC88\uD638(NIE)";
            map["Social Security nos."] = "\uC0AC\uD68C \uBCF4\uC7A5 \uBC88\uD638";
            map["IBANs"] = "IBAN";
            map["cards"] = "\uCE74\uB4DC \uBC88\uD638";
            map["emails"] = "\uC774\uBA54\uC77C \uC8FC\uC18C";
            map["phones"] = "\uC804\uD654\uBC88\uD638";
            map["SSNs"] = "\uBBF8\uAD6D \uC0AC\uD68C \uBCF4\uC7A5 \uBC88\uD638(SSN)";
            map["secret keys"] = "\uBE44\uBC00 \uD0A4";

            // SensitiveCover
            map["Unchecked"] = "\uBBF8\uAC80\uC0AC";
            map["1 piece of data redacted"] = "\uBBFC\uAC10 \uC815\uBCF4 1\uAC1C\uB97C \uAC00\uB838\uC2B5\uB2C8\uB2E4";
            map["{0} items redacted"] = "\uBBFC\uAC10 \uC815\uBCF4 {0}\uAC1C\uB97C \uAC00\uB838\uC2B5\uB2C8\uB2E4";
            map["Not enough memory to check such a large image."] = "\uC774\uBBF8\uC9C0\uAC00 \uB108\uBB34 \uCEE4\uC11C \uAC80\uC0AC\uD560 \uBA54\uBAA8\uB9AC\uAC00 \uBD80\uC871\uD569\uB2C8\uB2E4.";
            map["Could not check the image."] = "\uC774\uBBF8\uC9C0\uB97C \uAC80\uC0AC\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";

            // Webcam
            map["Connecting the camera\u2026"] = "\uCE74\uBA54\uB77C\uC5D0 \uC5F0\uACB0 \uC911\u2026";
            map["Cannot open the camera"] = "\uCE74\uBA54\uB77C\uB97C \uC5F4 \uC218 \uC5C6\uC74C";
            map["no image for {0} s"] = "{0}\uCD08 \uB3D9\uC548 \uC601\uC0C1 \uC5C6\uC74C";
            map["no response in {0} s"] = "{0}\uCD08 \uB3D9\uC548 \uC751\uB2F5 \uC5C6\uC74C";
            map["FFmpeg closed"] = "FFmpeg \uC885\uB8CC\uB428";
            map["Reconnecting the camera\u2026"] = "\uCE74\uBA54\uB77C\uC5D0 \uB2E4\uC2DC \uC5F0\uACB0 \uC911\u2026";
            map["The camera disconnected"] = "\uCE74\uBA54\uB77C \uC5F0\uACB0 \uB04A\uAE40";

            // Additional labels
            map["Light"] = "\uBC1D\uAC8C";

            // Card.Access
            map["Video "] = "\uB3D9\uC601\uC0C1 ";
            map["Capture "] = "\uCEA1\uCC98 ";
            map["Saved in "] = "\uC800\uC7A5 \uC704\uCE58: ";
            map["Not saved yet"] = "\uC544\uC9C1 \uC800\uC7A5\uB418\uC9C0 \uC54A\uC74C";

            // Card
            map["Join"] = "\uD569\uCE58\uAE30";
            map["Reading\u2026"] = "\uC77D\uB294 \uC911\u2026";
            map["Could not read"] = "\uC77D\uAE30 \uC2E4\uD328";
            map["Could not pin capture"] = "\uCEA1\uCC98\uB97C \uD654\uBA74\uC5D0 \uACE0\uC815\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4";
            map["Could not read the image. Please try again."] = "\uC774\uBBF8\uC9C0\uB97C \uC77D\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. \uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694.";
            map["Please try again."] = "\uB2E4\uC2DC \uC2DC\uB3C4\uD558\uC138\uC694.";
            map["Could not show"] = "\uD45C\uC2DC \uC2E4\uD328";

            // Additional labels
            map["session locked or away"] = "\uC138\uC158 \uC7A0\uAE40 \uB610\uB294 \uC790\uB9AC \uBE44\uC6C0";
            map["full-screen application"] = "\uC804\uCCB4 \uD654\uBA74 \uC571";
            map["full-screen game"] = "\uC804\uCCB4 \uD654\uBA74 \uAC8C\uC784";
            map["presentation mode"] = "\uD504\uB808\uC820\uD14C\uC774\uC158 \uBAA8\uB4DC";
            map["Windows focus assist / quiet hours"] = "Windows \uC9D1\uC911 \uC9C0\uC6D0 / \uBC29\uD574 \uAE08\uC9C0 \uC2DC\uAC04";
            map["Mix (computer + microphone)"] = "\uD63C\uD569(\uC2DC\uC2A4\uD15C \uC18C\uB9AC + \uB9C8\uC774\uD06C)";
            map["Computer sound (original)"] = "\uC2DC\uC2A4\uD15C \uC18C\uB9AC(\uC6D0\uBCF8)";
            map["Microphone (original)"] = "\uB9C8\uC774\uD06C(\uC6D0\uBCF8)";
            map["{0} tracks of 32-bit PCM, 48 kHz, stereo."] = "32\uBE44\uD2B8 PCM, 48 kHz \uC2A4\uD14C\uB808\uC624 \uD2B8\uB799 {0}\uAC1C.";
            map["{0} track of 32-bit PCM, 48 kHz, stereo."] = "32\uBE44\uD2B8 PCM, 48 kHz \uC2A4\uD14C\uB808\uC624 \uD2B8\uB799 {0}\uAC1C.";
            map["~{0} min disk space"] = "\uC57D {0}\uBD84 \uBD84\uB7C9\uC758 \uB514\uC2A4\uD06C \uACF5\uAC04";
            map["up to {0}"] = "\uCD5C\uB300 {0}";
            map["about {0}"] = "\uC57D {0}";
            map["between {0} and {1}"] = "{0}~{1}";
            map["{0} min"] = "{0}\uBD84";
            map["{0} h"] = "{0}\uC2DC\uAC04";
            map["{0} frames ({1}%) were late or repeated."] = "{0}\uAC1C \uD504\uB808\uC784({1}%)\uC774 \uC9C0\uC5F0\uB418\uAC70\uB098 \uBC18\uBCF5\uB418\uC5C8\uC2B5\uB2C8\uB2E4.";
            map["The computer paused for {0} s; frames may have repeated."] = "\uCEF4\uD4E8\uD130\uAC00 {0}\uCD08 \uB3D9\uC548 \uBA48\uCDB0 \uC77C\uBD80 \uD504\uB808\uC784\uC774 \uBC18\uBCF5\uB418\uC5C8\uC744 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            map["{0} Try: {1}."] = "{0} \uB2E4\uC74C \uBC29\uBC95\uC744 \uC2DC\uB3C4\uD558\uC138\uC694: {1}.";
            map[" or "] = " \uB610\uB294 ";
            map["{0}: faded for {1} ms after each of {2} pauses."] = "{0}: \uC77C\uC2DC \uC815\uC9C0 {2}\uD68C \uD6C4 \uC7AC\uAC1C\uD560 \uB54C\uB9C8\uB2E4 {1}ms \uB3D9\uC548 \uC74C\uB7C9\uC744 \uC11C\uC11C\uD788 \uC870\uC808\uD588\uC2B5\uB2C8\uB2E4.";
            map["clipped {0} times (first at {1}, held at full scale for {2})"] = "\uD074\uB9AC\uD551 {0}\uD68C(\uCD5C\uCD08 \uBC1C\uC0DD {1}, \uCD5C\uB300 \uB808\uBCA8 \uC9C0\uC18D \uC2DC\uAC04 {2})";
            map["{0} lines: \u201C{1}\u201D"] = "{0}\uC904: \u201C{1}\u201D";
            map[" screens"] = "\uAC1C \uD654\uBA74";
            map["{0}: {1} items"] = "{0} {1}\uAC1C";
            map["No sensitive data found."] = "\uBBFC\uAC10 \uC815\uBCF4\uB97C \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
            map["Sensitive data is already redacted."] = "\uBBFC\uAC10 \uC815\uBCF4\uAC00 \uC774\uBBF8 \uAC00\uB824\uC838 \uC788\uC2B5\uB2C8\uB2E4.";
            map["Redacted {0} more items; {1} were already covered."] = "{0}\uAC1C\uB97C \uCD94\uAC00\uB85C \uAC00\uB838\uC2B5\uB2C8\uB2E4. \uB098\uBA38\uC9C0 {1}\uAC1C\uB294 \uC774\uBBF8 \uAC00\uB824\uC838 \uC788\uC5C8\uC2B5\uB2C8\uB2E4.";
            map["{0} items redacted: {1}. You can remove the marks."] = "\uBBFC\uAC10 \uC815\uBCF4 {0}\uAC1C \uAC00\uB9BC({1}). \uD45C\uC2DC\uB294 \uC81C\uAC70 \uAC00\uB2A5\uD569\uB2C8\uB2E4.";
            map["Original"] = "\uC6D0\uBCF8";
            map["Sunset"] = "\uB178\uC744";
            map["Mint"] = "\uBBFC\uD2B8";
            map["Ocean"] = "\uBC14\uB2E4";
            map["Peach"] = "\uBCF5\uC22D\uC544";
            map["Lavender"] = "\uB77C\uBCA4\uB354";
            map["Rose"] = "\uC7A5\uBBF8";
            map["Aurora"] = "\uC624\uB85C\uB77C";
            map["Night"] = "\uBC24";
            map["Graphite"] = "\uD751\uC5F0";
            map["Dark"] = "\uC5B4\uB461\uAC8C";
            map["Your desktop"] = "\uB0B4 \uBC14\uD0D5 \uD654\uBA74";
            map["Your image "] = "\uB0B4 \uC774\uBBF8\uC9C0 ";
            map["Export quality: "] = "\uB0B4\uBCF4\uB0B4\uAE30 \uD654\uC9C8: ";
            map[" (email)"] = " (\uC774\uBA54\uC77C)";
            map["Color"] = "\uC0C9\uC0C1";

            // Cinema verification report
            map["Cinema: {0} \u00D7 {1} at {2} fps, {3} s, {4} frames."] = "\uC2DC\uB124\uB9C8: {0} \u00D7 {1}, {2} fps, {3}\uCD08, {4}\uD504\uB808\uC784.";
            map["File: "] = "\uD30C\uC77C: ";
            map["The recording stopped early: {0}. What was recorded up to then has been saved."] = "\uB179\uD654\uAC00 \uC77C\uCC0D \uC885\uB8CC\uB418\uC5C8\uC2B5\uB2C8\uB2E4({0}). \uC911\uC9C0 \uC2DC\uC810\uAE4C\uC9C0\uC758 \uC601\uC0C1\uC744 \uC800\uC7A5\uD588\uC2B5\uB2C8\uB2E4.";
            map["- Paused once: what happened during the pause is not in the file."] = "- \uD55C \uBC88 \uC77C\uC2DC \uC815\uC9C0\uD588\uC2B5\uB2C8\uB2E4. \uC815\uC9C0 \uC911\uC758 \uB0B4\uC6A9\uC740 \uD30C\uC77C\uC5D0 \uD3EC\uD568\uB418\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
            map["- Paused {0} times: what happened during the pauses is not in the file."] = "- {0}\uD68C \uC77C\uC2DC \uC815\uC9C0\uD588\uC2B5\uB2C8\uB2E4. \uC815\uC9C0 \uC911\uC758 \uB0B4\uC6A9\uC740 \uD30C\uC77C\uC5D0 \uD3EC\uD568\uB418\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
            map["- 1 frame has clicks or pressed keys drawn on them (Recording > Clicks and keys)."] = "- 1\uAC1C \uD504\uB808\uC784\uC5D0 \uD074\uB9AD\uC774\uB098 \uD0A4 \uC785\uB825\uC774 \uD45C\uC2DC\uB418\uC5C8\uC2B5\uB2C8\uB2E4(\uB179\uD654 > \uD074\uB9AD \uBC0F \uD0A4 \uC785\uB825).";
            map["- {0} frames have clicks or pressed keys drawn on them (Recording > Clicks and keys)."] = "- {0}\uAC1C \uD504\uB808\uC784\uC5D0 \uD074\uB9AD\uC774\uB098 \uD0A4 \uC785\uB825\uC774 \uD45C\uC2DC\uB418\uC5C8\uC2B5\uB2C8\uB2E4(\uB179\uD654 > \uD074\uB9AD \uBC0F \uD0A4 \uC785\uB825).";
            map["- {0} frames may have taken the wrong screen image (the computer was too slow)."] = "- PC\uC758 \uCC98\uB9AC \uC18D\uB3C4\uAC00 \uBD80\uC871\uD574 {0}\uAC1C \uD504\uB808\uC784\uC5D0 \uB2E4\uB978 \uC2DC\uC810\uC758 \uD654\uBA74\uC774 \uB2F4\uACBC\uC744 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            map["- {0} frames repeat the one before because the screen could not be read."] = "- \uD654\uBA74\uC744 \uC77D\uC744 \uC218 \uC5C6\uC5B4 {0}\uAC1C \uD504\uB808\uC784\uC5D0\uC11C \uC9C1\uC804 \uD504\uB808\uC784\uC744 \uBC18\uBCF5\uD588\uC2B5\uB2C8\uB2E4.";
            map["- {0} black frames at the start because the screen could not be read."] = "- \uCC98\uC74C\uC5D0 \uD654\uBA74\uC744 \uC77D\uC744 \uC218 \uC5C6\uC5B4 \uAC80\uC740 \uD504\uB808\uC784 {0}\uAC1C\uAC00 \uB4E4\uC5B4\uAC14\uC2B5\uB2C8\uB2E4.";
            map["- {0} captured frames were never written."] = "- \uCEA1\uCC98\uD55C \uD504\uB808\uC784 \uC911 {0}\uAC1C\uAC00 \uC800\uC7A5\uB418\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4.";
            map["- Screen capture was interrupted once ({0} ms with no new image)."] = "- \uD654\uBA74 \uCEA1\uCC98\uAC00 1\uD68C \uB04A\uACBC\uC2B5\uB2C8\uB2E4({0}ms \uB3D9\uC548 \uC0C8 \uD654\uBA74 \uC5C6\uC74C).";
            map["- Screen capture was interrupted {1} times ({0} ms with no new image)."] = "- \uD654\uBA74 \uCEA1\uCC98\uAC00 {1}\uD68C \uB04A\uACBC\uC2B5\uB2C8\uB2E4({0}ms \uB3D9\uC548 \uC0C8 \uD654\uBA74 \uC5C6\uC74C).";
            map["- In {0} images Windows hid protected windows or windows excluded from capture on that screen (Stackshot\u2019s too); any inside the area is missing or black."] = "- Windows\uAC00 {0}\uAC1C \uD654\uBA74\uC5D0\uC11C \uBCF4\uD638\uB41C \uCC3D \uB610\uB294 \uCEA1\uCC98 \uC81C\uC678 \uCC3D(Stackshot \uD3EC\uD568)\uC744 \uC228\uACBC\uC2B5\uB2C8\uB2E4. \uC601\uC5ED \uC548\uC758 \uD574\uB2F9 \uCC3D\uC740 \uBCF4\uC774\uC9C0 \uC54A\uAC70\uB098 \uAC80\uAC8C \uD45C\uC2DC\uB429\uB2C8\uB2E4.";
            map["- The fallback capture (GDI) was used: it does not see hardware-accelerated content."] = "- \uB300\uCCB4 \uCEA1\uCC98 \uBC29\uC2DD(GDI)\uC744 \uC0AC\uC6A9\uD588\uC2B5\uB2C8\uB2E4. \uD558\uB4DC\uC6E8\uC5B4 \uAC00\uC18D \uCF58\uD150\uCE20\uB294 \uCEA1\uCC98\uB418\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
            map["- The screen is in HDR: Windows hands over the image converted to SDR."] = "- HDR \uD654\uBA74\uC785\uB2C8\uB2E4. Windows\uC5D0\uC11C SDR\uB85C \uBCC0\uD658\uD55C \uC774\uBBF8\uC9C0\uB97C \uC804\uB2EC\uD588\uC2B5\uB2C8\uB2E4.";
            map["- The monitor runs at {0} Hz and recording is at {1} fps: {2} screen images are not in any frame."] = "- \uBAA8\uB2C8\uD130 \uC8FC\uC0AC\uC728\uC740 {0} Hz\uC774\uACE0 \uB179\uD654 \uC18D\uB3C4\uB294 {1} fps\uC785\uB2C8\uB2E4. \uD654\uBA74 {2}\uAC1C\uAC00 \uB179\uD654 \uD504\uB808\uC784\uC5D0 \uD3EC\uD568\uB418\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4.";
            map["The master was not created: "] = "\uB9C8\uC2A4\uD130 \uD30C\uC77C \uC0DD\uC131 \uC2E4\uD328: ";
            map["What was recorded is in: "] = "\uB179\uD654 \uD30C\uC77C \uC704\uCE58: ";
            map["Check: not done."] = "\uAC80\uC99D: \uC218\uD589\uD558\uC9C0 \uC54A\uC74C.";
            map["Check: passed, {0} frames identical to the ones sent to the encoder."] = "\uAC80\uC99D \uD1B5\uACFC: \uC778\uCF54\uB354\uB85C \uBCF4\uB0B8 \uD504\uB808\uC784 {0}\uAC1C\uAC00 \uBAA8\uB450 \uC77C\uCE58\uD569\uB2C8\uB2E4.";
            map["Check: FAILED."] = "\uAC80\uC99D \uC2E4\uD328.";

            map["Volume Down"] = "\uBCFC\uB968 \uC791\uAC8C";
            map["Volume Up"] = "\uBCFC\uB968 \uD06C\uAC8C";
            map["Play/Pause"] = "\uC7AC\uC0DD/\uC77C\uC2DC \uC815\uC9C0";
            map["Next Track"] = "\uB2E4\uC74C \uD2B8\uB799";
            map["Previous Track"] = "\uC774\uC804 \uD2B8\uB799";
            map["Stop loading"] = "\uB85C\uB529 \uC911\uC9C0";
            map["Browser Home"] = "\uBE0C\uB77C\uC6B0\uC800 \uD648";
            map["Mail"] = "\uBA54\uC77C";
            map["App 1"] = "\uC571 1";
            map["Calculator"] = "\uACC4\uC0B0\uAE30";
            map["Media"] = "\uBBF8\uB514\uC5B4";
            map["From 0 to 100"] = "0~100";

            RegisterKoreanEditor(map);
        }
    }
}
