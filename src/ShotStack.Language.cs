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
            return I18n.GermanWindows(windows) ? "de" : windows.TwoLetterISOLanguageName == "ko" ? "ko" : "en";
        }

        // The words of the offer, in the language it offers. The settings route is named as the user sees it now (Spanish)
        // and as it will read after the switch; the toast has three lines, so the sentence around it stays short.
        internal static void OfferTexts(string target, out string title, out string body, out string button)
        {
            bool korean = target == "ko", german = target == "de";
            title = korean ? "Stackshot\uC744 \uD55C\uAD6D\uC5B4\uB85C \uC0AC\uC6A9\uD560 \uC218 \uC788\uC5B4\uC694" :
                    german ? "Stackshot gibt es jetzt auf Deutsch" : "Stackshot is now available in English";
            body = korean ? "\uD55C\uAD6D\uC5B4\uB85C \uBC14\uAFC0\uAE4C\uC694? \uB098\uC911\uC5D0 Ajustes \u203A General \u203A Apariencia \u203A Idioma (\uC124\uC815 \u203A \uC77C\uBC18 \u203A \uD654\uBA74 \uBAA8\uC591 \u203A \uC5B8\uC5B4)\uC5D0\uC11C \uBC14\uAFC0 \uC218 \uC788\uC5B4\uC694." :
                   german ? "Auf Deutsch umstellen? \u00C4nderbar unter Ajustes \u203A General \u203A Apariencia \u203A Idioma (Einstellungen \u203A Allgemein \u203A Erscheinungsbild \u203A Sprache)." :
                   "Switch to English? You can change it later in Ajustes \u203A General \u203A Apariencia \u203A Idioma (Settings \u203A General \u203A Appearance \u203A Language).";
            button = korean ? "\uD55C\uAD6D\uC5B4 \uC0AC\uC6A9" : german ? "Deutsch verwenden" : "Use English";
        }

        // Keep the current UI in Spanish until the user accepts the offer.
        void OfferLanguage()
        {
            CultureInfo windows = CultureInfo.CurrentUICulture;
            if (Test || !ShouldOfferLanguage(settings, windows)) return;
            settings.LanguageOffered = true;
            settings.Save();
            string target = OfferedLanguage(windows);
            string title, body, button;
            OfferTexts(target, out title, out body, out button);
            Notify(title, body, NoticeKind.Info, button, delegate
                   {
                       LanguagePicker.Apply(settings, target);
                       Log("Idioma: " + target + ", aceptado desde el aviso");
                   });
            Log("Idioma: se ofrece " + target + " (Windows en " + windows.Name + ")");
        }
    }
}
