// Stackshot - Arrastrar ficheros a otras aplicaciones con la miniatura pegada al cursor.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace Stackshot
{
    public static class FileDrag
    {
        static readonly Guid BHID_DataObject = new Guid("B8C0BD9F-ED24-455c-83E6-D5390C4FE8C4");
        static readonly Guid IID_IDataObject = new Guid("0000010e-0000-0000-C000-000000000046");

        // ghost: lo que acompaña al cursor (se libera aquí); grab: el punto de ghost que queda bajo el cursor.
        public static DragDropEffects Run(Control source, string path, Bitmap ghost, Point grab)
        {
            object shell = ShellData(path);
            try
            {
                if (shell != null && ghost != null) AttachImage((ComTypes.IDataObject)shell, ghost, grab);
            }
            catch (Exception ex) { ShotStack.Log("Arrastrar (miniatura): " + ex.Message); }
            finally { if (ghost != null) ghost.Dispose(); }
            object data = shell;
            if (data == null)
            {
                // Plan B: la lista de ficheros de siempre, sin miniatura.
                DataObject d = new DataObject();
                StringCollection sc = new StringCollection();
                sc.Add(path);
                d.SetFileDropList(sc);
                data = d;
            }
            try { return source.DoDragDrop(data, DragDropEffects.Copy); }
            finally { if (shell != null) Marshal.ReleaseComObject(shell); }
        }

        // El mismo objeto que usa el Explorador al arrastrar ese fichero: lo entiende cualquier aplicación.
        static object ShellData(string path)
        {
            IShellItem item = null;
            try
            {
                Native.SHCreateShellItem(path, IntPtr.Zero, typeof(IShellItem).GUID, out item);
                IntPtr p;
                if (item.BindToHandler(IntPtr.Zero, BHID_DataObject, IID_IDataObject, out p) != 0 || p == IntPtr.Zero) return null;
                try { return Marshal.GetObjectForIUnknown(p); }
                finally { Marshal.Release(p); }
            }
            catch (Exception ex)
            {
                ShotStack.Log("Arrastrar: " + ex.Message);
                return null;
            }
            finally { if (item != null) Marshal.ReleaseComObject(item); }
        }

        static void AttachImage(ComTypes.IDataObject data, Bitmap ghost, Point grab)
        {
            object helper = new DragDropHelper();
            try
            {
                IDragSourceHelper2 h = (IDragSourceHelper2)helper;
                h.SetFlags(1); // DSH_ALLOWDROPDESCRIPTIONTEXT: "Copiar en ..." debajo de la miniatura
                Native.SHDRAGIMAGE di;
                di.sizeDragImage.cx = ghost.Width;
                di.sizeDragImage.cy = ghost.Height;
                di.ptOffset.X = grab.X;
                di.ptOffset.Y = grab.Y;
                di.hbmpDragImage = ghost.GetHbitmap(Color.FromArgb(0, 0, 0, 0)); // alfa premultiplicado
                di.crColorKey = unchecked((int)0xFFFFFFFF);
                // Si sale bien, el mapa de bits pasa a ser del sistema; si no, hay que liberarlo.
                if (h.InitializeFromBitmap(ref di, data) != 0) Native.DeleteObject(di.hbmpDragImage);
            }
            finally { Marshal.ReleaseComObject(helper); }
        }

        // Miniatura con las esquinas redondeadas y un borde fino. grab se escala con ella.
        public static Bitmap Ghost(Bitmap src, int maxDim, float radius, ref Point grab)
        {
            double k = Math.Min(1.0, (double)maxDim / Math.Max(src.Width, src.Height));
            int w = Math.Max(2, (int)Math.Round(src.Width * k)), h = Math.Max(2, (int)Math.Round(src.Height * k));
            grab = new Point((int)Math.Round(grab.X * k), (int)Math.Round(grab.Y * k));
            Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (Bitmap scaled = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(scaled))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(src, new Rectangle(0, 0, w, h));
                }
                using (Graphics g = Graphics.FromImage(b))
                using (GraphicsPath p = Theme.Round(new RectangleF(0.5f, 0.5f, w - 1, h - 1), radius))
                using (TextureBrush tb = new TextureBrush(scaled))
                using (Pen pen = new Pen(Theme.Border))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.FillPath(tb, p);
                    g.DrawPath(pen, p);
                }
            }
            return b;
        }
    }

}
