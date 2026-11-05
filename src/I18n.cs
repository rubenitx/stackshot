// Stackshot - Internationalization & localization engine (explicit choice or Windows display language).
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace Stackshot
{
    public static partial class I18n
    {
        public static string Mode = "auto";
        public static string Language { get; private set; }
        public static bool IsSpanish { get { return Language == "es"; } }
        public static bool IsEnglish { get { return Language == "en"; } }
        public static bool IsGerman { get { return Language == "de"; } }
        public static bool IsKorean { get { return Language == "ko"; } }
        public static event Action Changed;

        // The culture of the active interface language: shown numbers (thousands, decimals) follow it, not the Windows
        // locale — everything around them is in that language. Dates keep their own per-language format strings.
        public static CultureInfo Culture
        {
            get { return CultureInfo.GetCultureInfo(IsGerman ? "de-DE" : IsSpanish ? "es-ES" : "en-US"); }
        }

        static I18n()
        {
            Apply("auto");
        }

        // Catalan, Galician and Basque Windows installations use Spanish in automatic mode.
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
                    else if (ui.TwoLetterISOLanguageName == "ko") Language = "ko";
                    else Language = "en";
                }
                catch { Language = "en"; }
            }

            if (wasLang != Language || wasMode != Mode)
            {
                Action h = Changed;
                if (h != null)
                    // One delegate each: a listener that throws must not take the others (and their live
                    // relocalization) down with it.
                    foreach (Action d in h.GetInvocationList())
                    {
                        try { d(); }
                        catch (Exception ex) { ShotStack.Log("I18n: un oyente de Changed ha fallado: " + ex.Message); }
                    }
            }
        }

        // Returns the localized text based on current locale setting.
        public static string T(string es, string en)
        {
            if (IsSpanish) return es;
            if (IsKorean) return Localize(en);
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
            if (IsKorean) return Localize(en);
            if (IsGerman && !string.IsNullOrEmpty(de)) return de;
            return PreviewText(en) ?? en;
        }

        // Formats a localized string, with the numbers in the interface language's culture.
        public static string Fmt(string esFmt, string enFmt, params object[] args)
        {
            return string.Format(Culture, T(esFmt, enFmt), args);
        }

        // Shown text back to the English key it translates (the key T starts from): the catalog maps every English key
        // to its German, and the Spanish ones among them to theirs, so one inverted dictionary reads both back. Built
        // once, on first use; read-only afterwards, so any thread can ask it.
        static readonly Lazy<Dictionary<string, string>> ReverseKeys = new Lazy<Dictionary<string, string>>(
            delegate
            {
                var catalog = GermanCatalog.Value;
                var byDe = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, string> p in catalog.Item1)
                {
                    if (!byDe.ContainsKey(p.Value)) byDe[p.Value] = p.Key;
                    // Catalog keys may carry a trailing separator (": ") that a notice title loses when it is trimmed.
                    string trimmed = p.Value.Trim();
                    if (trimmed.Length > 0 && !byDe.ContainsKey(trimmed)) byDe[trimmed] = p.Key;
                }
                var shown = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, string> p in catalog.Item2)
                {
                    string en;
                    if (!byDe.TryGetValue(p.Value, out en)) continue;
                    if (!shown.ContainsKey(p.Key)) shown[p.Key] = en;
                    string trimmed = p.Key.Trim();
                    if (trimmed.Length > 0 && !shown.ContainsKey(trimmed)) shown[trimmed] = en;
                }
                foreach (KeyValuePair<string, string> p in byDe)
                    if (!shown.ContainsKey(p.Key)) shown[p.Key] = p.Value;
                return shown;
            }, LazyThreadSafetyMode.ExecutionAndPublication);

        // The English key the shown German or Spanish text translates, or null when it is not a catalog text. Used to
        // classify notices (Toast.cs): their kind comes from the key, not from the words of one language.
        public static string EnglishKey(string shown)
        {
            if (string.IsNullOrEmpty(shown)) return null;
            string en;
            return ReverseKeys.Value.TryGetValue(shown.Trim(), out en) ? en : null;
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
