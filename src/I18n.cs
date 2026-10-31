// Stackshot - Internationalization & localization engine (Spanish / English / Auto).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Globalization;

namespace Stackshot
{
    public static class I18n
    {
        public static string Mode = "auto"; // "auto", "es", "en"
        public static bool IsSpanish { get; private set; }
        public static event Action Changed;

        static I18n()
        {
            Apply("auto");
        }

        // Sets the active language: "auto" (follows Windows UI culture), "es", or "en".
        // What Automático picks for a Windows display language: Spanish on a Spanish Windows, and on a Catalan, Galician or
        // Basque one (Spanish is closer to home there than English); English everywhere else.
        public static bool SpanishWindows(CultureInfo ui)
        {
            string l = ui.TwoLetterISOLanguageName.ToLowerInvariant();
            return l == "es" || l == "ca" || l == "gl" || l == "eu";
        }

        public static void Apply(string mode)
        {
            if (string.IsNullOrEmpty(mode)) mode = "auto";
            mode = mode.Trim().ToLowerInvariant();
            if (mode != "es" && mode != "en") mode = "auto";

            bool wasSpanish = IsSpanish;
            string wasMode = Mode;
            Mode = mode;

            if (mode == "es")
            {
                IsSpanish = true;
            }
            else if (mode == "en")
            {
                IsSpanish = false;
            }
            else
            {
                try { IsSpanish = SpanishWindows(CultureInfo.CurrentUICulture); }
                catch { IsSpanish = false; }
            }

            if (wasSpanish != IsSpanish || wasMode != Mode)
            {
                Action h = Changed;
                if (h != null)
                {
                    try { h(); }
                    catch { }
                }
            }
        }

        // Returns either the Spanish or English text based on current locale setting.
        public static string T(string es, string en)
        {
            return IsSpanish ? es : en;
        }

        // Formats a localized string.
        public static string Fmt(string esFmt, string enFmt, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, IsSpanish ? esFmt : enFmt, args);
        }

        // Common UI verbs & labels
        public static string Save { get { return IsSpanish ? "Guardar" : "Save"; } }
        public static string Copy { get { return IsSpanish ? "Copiar" : "Copy"; } }
        public static string Close { get { return IsSpanish ? "Cerrar" : "Close"; } }
        public static string Delete { get { return IsSpanish ? "Eliminar" : "Delete"; } }
        public static string Cancel { get { return IsSpanish ? "Cancelar" : "Cancel"; } }
        public static string Done { get { return IsSpanish ? "Listo" : "Done"; } }
        public static string Open { get { return IsSpanish ? "Abrir" : "Open"; } }
        public static string Quit { get { return IsSpanish ? "Salir" : "Quit"; } }
        public static string SettingsText { get { return IsSpanish ? "Ajustes" : "Settings"; } }
    }
}
