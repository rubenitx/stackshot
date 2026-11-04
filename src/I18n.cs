// Stackshot - Internationalization & localization engine (Spanish / English / German / Auto).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Globalization;

namespace Stackshot
{
    public static partial class I18n
    {
        public static string Mode = "auto";
        public static string Language { get; private set; }
        public static bool IsSpanish { get { return Language == "es"; } }
        public static bool IsEnglish { get { return Language == "en"; } }
        public static bool IsGerman { get { return Language == "de"; } }
        public static event Action Changed;

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
            if (FindLanguage(mode) == null) mode = "auto";

            string wasLang = Language;
            string wasMode = Mode;
            Mode = mode;

            if (mode != "auto") Language = mode;
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
            return PreviewText(en) ?? en;
        }

        public static string T(string es, string en, string de)
        {
            if (IsSpanish) return es;
            if (IsGerman && !string.IsNullOrEmpty(de)) return de;
            return PreviewText(en) ?? en;
        }

        // Formats a localized string.
        public static string Fmt(string esFmt, string enFmt, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, T(esFmt, enFmt), args);
        }



        // Common UI verbs & labels
        public static string Save { get { return T("Guardar", "Save"); } }
        public static string Copy { get { return T("Copiar", "Copy"); } }
        public static string Close { get { return T("Cerrar", "Close"); } }
        public static string Delete { get { return T("Eliminar", "Delete"); } }
        public static string Cancel { get { return T("Cancelar", "Cancel"); } }
        public static string Done { get { return T("Listo", "Done"); } }
        public static string Open { get { return T("Abrir", "Open"); } }
        public static string Quit { get { return T("Salir", "Quit"); } }
        public static string SettingsText { get { return T("Ajustes", "Settings"); } }
    }
}
