// Stackshot - Offers English, once, to whoever kept Spanish from before there was a choice while Windows is in another
// language. Nothing changes unless they accept; Ajustes > General > Idioma switches it any time.
// MIT License - https://github.com/rubenitx/stackshot
using System.Globalization;

namespace Stackshot
{
    public partial class ShotStack
    {
        // Spanish chosen by an earlier version (or by hand) on a Windows that is not in Spanish, not offered yet.
        internal static bool ShouldOfferEnglish(Settings s, CultureInfo windows)
        {
            return s.Language == "es" && !s.LanguageOffered && !I18n.SpanishWindows(windows);
        }

        // Said once, a few seconds after startup, in English: the language of this Windows or the closest Stackshot has.
        void OfferEnglish()
        {
            CultureInfo windows = CultureInfo.CurrentUICulture;
            if (Test || !ShouldOfferEnglish(settings, windows)) return;
            settings.LanguageOffered = true;
            settings.Save();
            string language;
            try { language = new CultureInfo(windows.TwoLetterISOLanguageName).EnglishName; }
            catch (CultureNotFoundException) { language = "another language"; }
            Notify("Stackshot is now available in English",
                   "Your Windows is in " + language + ". Switch Stackshot to English? You can change it any time in Ajustes \u203A General \u203A Apariencia \u203A Idioma (Settings \u203A General \u203A Appearance \u203A Language).",
                   NoticeKind.Info, "Use English", delegate
                   {
                       settings.Language = "en";
                       settings.Save();
                       I18n.Apply("en");
                       Log("Idioma: ingl\u00E9s, aceptado desde el aviso");
                   });
            Log("Idioma: se ofrece el ingl\u00E9s (Windows en " + windows.Name + ")");
        }
    }
}
