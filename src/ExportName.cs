// Stackshot - The words an exported file adds to the name of the video it comes from ("Cine A (editado).mp4"), in the
// interface language. Nothing reads them back (recordings are told by their "V\u00EDdeo", "GIF" and "Cine" prefixes), so they
// can follow the language; the other languages' previews use the English ones.
// MIT License - https://github.com/rubenitx/stackshot
namespace Stackshot
{
    internal static class ExportName
    {
        // A clip marked with E and S.
        public static string Clip { get { return I18n.Own(" (tramo)", " (clip)", " (Ausschnitt)", " (\uAD6C\uAC04)"); } }
        // Exported with a backdrop, annotations, a trim or cuts.
        public static string Edited { get { return I18n.Own(" (editado)", " (edited)", " (bearbeitet)", " (\uD3B8\uC9D1\uB428)"); } }
        // A lossless master compressed for sending as it is.
        public static string ForSharing { get { return I18n.Own(" (para compartir)", " (for sharing)", " (zum Teilen)", " (\uACF5\uC720\uC6A9)"); } }
        // The file the check could not vouch for, kept beside the one that was expected.
        public static string Unverified { get { return I18n.Own(" (sin validar)", " (unverified)", " (ungepr\u00FCft)", " (\uBBF8\uAC80\uC99D)"); } }
    }
}
