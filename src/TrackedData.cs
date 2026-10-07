// Stackshot - Imagen en el portapapeles que avisa cuando se pega.
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
    public class TrackedData : DataObject
    {
        public readonly string FilePath;
        readonly DateTime since = DateTime.Now;
        readonly Action<TrackedData> onUsed;
        readonly Bitmap image;
        readonly MemoryStream png = new MemoryStream();
        bool used;

        public TrackedData(string path, Bitmap image, bool fromCapture, Action<TrackedData> onUsed)
        {
            FilePath = path;
            this.image = image;
            this.onUsed = onUsed;
            image.Save(png, ImageFormat.Png);
            png.Position = 0;
            SetData(DataFormats.Bitmap, true, image);
            SetData("PNG", false, png);
            if (fromCapture)
            {
                // Ya está en el historial de Win+V: que esta copia no salga repetida.
                SetData("CanIncludeInClipboardHistory", false, new MemoryStream(BitConverter.GetBytes(0)));
                SetData("CanUploadToCloudClipboard", false, new MemoryStream(BitConverter.GetBytes(0)));
            }
        }

        public override object GetData(string format, bool autoConvert)
        {
            object o = base.GetData(format, autoConvert);
            if (!used && o != null && !format.StartsWith("Can") && (DateTime.Now - since).TotalMilliseconds > 1000)
            {
                used = true;
                if (onUsed != null) onUsed(this);
            }
            return o;
        }

        public void Release()
        {
            png.Dispose();
            image.Dispose();
        }
    }

}
