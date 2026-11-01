// Stackshot - Internationalization & localization engine (Spanish / English / German / Auto).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Globalization;

namespace Stackshot
{
    public static partial class I18n
    {
        public static string Mode = "auto"; // "auto", "es", "en", "de"
        public static string Language { get; private set; } // "es", "en", "de"
        public static bool IsSpanish { get { return Language == "es"; } }
        public static bool IsEnglish { get { return Language == "en"; } }
        public static bool IsGerman { get { return Language == "de"; } }
        public static event Action Changed;

        public static readonly string[] SupportedLanguages = { "es", "en", "de" };
        public static readonly string[] SupportedLanguageNames = { "Espa\u00F1ol", "English", "Deutsch" };

        static I18n()
        {
            Apply("auto");
        }

        // Sets the active language: "auto" (follows Windows UI culture), "es", "en", or "de".
        // What Automático picks for a Windows display language: Spanish on a Spanish Windows, and on a Catalan, Galician or
        // Basque one; German on German Windows; English everywhere else.
        public static bool SpanishWindows(CultureInfo ui)
        {
            string l = ui.TwoLetterISOLanguageName.ToLowerInvariant();
            return l == "es" || l == "ca" || l == "gl" || l == "eu";
        }

        public static bool GermanWindows(CultureInfo ui)
        {
            string l = ui.TwoLetterISOLanguageName.ToLowerInvariant();
            return l == "de";
        }

        public static void Apply(string mode)
        {
            if (string.IsNullOrEmpty(mode)) mode = "auto";
            mode = mode.Trim().ToLowerInvariant();
            if (mode != "es" && mode != "en" && mode != "de") mode = "auto";

            string wasLang = Language;
            string wasMode = Mode;
            Mode = mode;

            if (mode == "es")
            {
                Language = "es";
            }
            else if (mode == "en")
            {
                Language = "en";
            }
            else if (mode == "de")
            {
                Language = "de";
            }
            else
            {
                try
                {
                    CultureInfo ui = CultureInfo.CurrentUICulture;
                    if (SpanishWindows(ui)) Language = "es";
                    else if (GermanWindows(ui)) Language = "de";
                    else Language = "en";
                }
                catch { Language = "en"; }
            }

            if (wasLang != Language || wasMode != Mode)
            {
                Action h = Changed;
                if (h != null)
                {
                    try { h(); }
                    catch { }
                }
            }
        }

        // Returns the localized text based on current locale setting.
        public static string T(string es, string en)
        {
            if (IsSpanish) return es;
            if (IsGerman)
            {
                string de = LookupGerman(en, es);
                if (de != null) return de;
            }
            return en;
        }

        public static string T(string es, string en, string de)
        {
            if (IsSpanish) return es;
            if (IsGerman && !string.IsNullOrEmpty(de)) return de;
            return en;
        }

        // Formats a localized string.
        public static string Fmt(string esFmt, string enFmt, params object[] args)
        {
            string fmt = IsSpanish ? esFmt : (IsGerman ? (LookupGerman(enFmt, esFmt) ?? enFmt) : enFmt);
            return string.Format(CultureInfo.CurrentCulture, fmt, args);
        }



        // Common UI verbs & labels
        public static string Save { get { return IsSpanish ? "Guardar" : IsGerman ? "Speichern" : "Save"; } }
        public static string Copy { get { return IsSpanish ? "Copiar" : IsGerman ? "Kopieren" : "Copy"; } }
        public static string Close { get { return IsSpanish ? "Cerrar" : IsGerman ? "Schlie\u00DFen" : "Close"; } }
        public static string Delete { get { return IsSpanish ? "Eliminar" : IsGerman ? "L\u00F6schen" : "Delete"; } }
        public static string Cancel { get { return IsSpanish ? "Cancelar" : IsGerman ? "Abbrechen" : "Cancel"; } }
        public static string Done { get { return IsSpanish ? "Listo" : IsGerman ? "Fertig" : "Done"; } }
        public static string Open { get { return IsSpanish ? "Abrir" : IsGerman ? "\u00D6ffnen" : "Open"; } }
        public static string Quit { get { return IsSpanish ? "Salir" : IsGerman ? "Beenden" : "Quit"; } }
        public static string SettingsText { get { return IsSpanish ? "Ajustes" : IsGerman ? "Einstellungen" : "Settings"; } }
    }
}
