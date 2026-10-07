// Stackshot - Drag files to other apps with a thumbnail attached to the cursor.
// MIT License - https://github.com/rubenitx/stackshot
using System;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace Stackshot
{
    public static class FileDrag
    {
        static readonly Guid BHID_DataObject = new Guid("B8C0BD9F-ED24-455c-83E6-D5390C4FE8C4");
        static readonly Guid IID_IDataObject = new Guid("0000010e-0000-0000-C000-000000000046");

        // ghost: image attached to the cursor (disposed here); grab: point of ghost under the cursor.
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
                // Fallback: plain file drop list, no thumbnail.
                DataObject d = new DataObject();
                StringCollection sc = new StringCollection();
                sc.Add(path);
                d.SetFileDropList(sc);
                data = d;
            }
            try { return source.DoDragDrop(data, DragDropEffects.Copy); }
            finally { if (shell != null) Marshal.ReleaseComObject(shell); }
        }

        // The same data object Explorer uses for this file, understood by every app.
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
                h.SetFlags(1); // DSH_ALLOWDROPDESCRIPTIONTEXT
                Native.SHDRAGIMAGE di;
                di.sizeDragImage.cx = ghost.Width;
                di.sizeDragImage.cy = ghost.Height;
                di.ptOffset.X = grab.X;
                di.ptOffset.Y = grab.Y;
                di.hbmpDragImage = ghost.GetHbitmap(Color.FromArgb(0, 0, 0, 0)); // premultiplied alpha
                di.crColorKey = unchecked((int)0xFFFFFFFF);
                // On success the system owns the bitmap; otherwise free it.
                if (h.InitializeFromBitmap(ref di, data) != 0) Native.DeleteObject(di.hbmpDragImage);
            }
            finally { Marshal.ReleaseComObject(helper); }
        }

        // Thumbnail with rounded corners and a thin border. grab is scaled with it.
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
