// Stackshot - Offer a language change once to existing users who kept Spanish.
// MIT License - https://github.com/rubenitx/stackshot
using System.Globalization;

namespace Stackshot
{
    public partial class ShotStack
    {
        // Spanish chosen by an earlier version (or by hand) on a Windows that is not in Spanish, not offered yet.
        internal static bool ShouldOfferLanguage(Settings s, CultureInfo windows)
        {
            return s.Language == "es" && !s.LanguageOffered && !I18n.SpanishWindows(windows);
        }

        internal static string OfferedLanguage(CultureInfo windows)
        {
            return windows.TwoLetterISOLanguageName == "ko" ? "ko" : "en";
        }

        // Keep the current UI in Spanish until the user accepts the offer.
        void OfferLanguage()
        {
            CultureInfo windows = CultureInfo.CurrentUICulture;
            if (Test || !ShouldOfferLanguage(settings, windows)) return;
            settings.LanguageOffered = true;
            settings.Save();
            string target = OfferedLanguage(windows);
            bool korean = target == "ko";
            string language;
            try { language = new CultureInfo(windows.TwoLetterISOLanguageName).EnglishName; }
            catch (CultureNotFoundException) { language = "another language"; }
            Notify(korean ? "Stackshot\uC744 \uD55C\uAD6D\uC5B4\uB85C \uC0AC\uC6A9\uD560 \uC218 \uC788\uC5B4\uC694" : "Stackshot is now available in English",
                   korean ? "Stackshot\uC744 \uD55C\uAD6D\uC5B4\uB85C \uBC14\uAFC0\uAE4C\uC694? \uB098\uC911\uC5D0 \uC124\uC815\uC758 \uC77C\uBC18 > \uD654\uBA74 \uBAA8\uC591 > \uC5B8\uC5B4\uC5D0\uC11C \uBCC0\uACBD\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4." :
                   "Your Windows is in " + language + ". Switch Stackshot to English? You can change it any time in Ajustes \u203A General \u203A Apariencia \u203A Idioma (Settings \u203A General \u203A Appearance \u203A Language).",
                   NoticeKind.Info, korean ? "\uD55C\uAD6D\uC5B4 \uC0AC\uC6A9" : "Use English", delegate
                   {
                       LanguagePicker.Apply(settings, target);
                       Log("Idioma: " + target + ", aceptado desde el aviso");
                   });
            Log("Idioma: se ofrece " + target + " (Windows en " + windows.Name + ")");
        }
    }
}
