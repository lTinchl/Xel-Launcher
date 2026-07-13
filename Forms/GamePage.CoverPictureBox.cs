using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Hi3Helper.Plugin.Core.Management;
using XelLauncher.Helpers;
using XelLauncher.Models;
namespace XelLauncher.Forms
{
    class CoverPictureBox : Control
    {
        private Image _image;
        private Image _fadeFromImage;
        private Bitmap _renderedImage;
        private Bitmap _renderedFadeFromImage;
        private Size _renderedSize = Size.Empty;
        private bool _renderCacheDirty = true;
        private bool _fadeActive;
        private float _fadeProgress = 1F;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Image Image
        {
            get => _image;
            set
            {
                _image = value;
                _renderCacheDirty = true;
                Invalidate();
            }
        }

        public CoverPictureBox()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);
            BackColor = Color.Black;
        }

        public void PrepareFadeFrom(Image image)
        {
            _fadeFromImage?.Dispose();
            _fadeFromImage = image;
            _fadeActive = image != null;
            _fadeProgress = _fadeActive ? 0F : 1F;
            _renderCacheDirty = true;
            Invalidate();
        }

        public void SetFadeProgress(float progress)
        {
            if (!_fadeActive) return;

            _fadeProgress = Math.Max(0F, Math.Min(1F, progress));
            Invalidate();
        }

        public void FinishFade()
        {
            _fadeActive = false;
            _fadeProgress = 1F;
            _fadeFromImage?.Dispose();
            _fadeFromImage = null;
            _renderedFadeFromImage?.Dispose();
            _renderedFadeFromImage = null;
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            if (Image == null)
                base.OnPaintBackground(pevent);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Image == null) return;
            var g = e.Graphics;
            EnsureRenderCache();
            g.Clear(Color.Black);
            g.CompositingQuality = CompositingQuality.HighSpeed;
            g.CompositingMode = CompositingMode.SourceOver;

            if (_fadeActive && _renderedFadeFromImage != null)
            {
                g.DrawImageUnscaled(_renderedFadeFromImage, 0, 0);
                DrawRenderedImage(g, _renderedImage, _fadeProgress);
                return;
            }

            DrawRenderedImage(g, _renderedImage, 1F);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            _renderCacheDirty = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _fadeFromImage?.Dispose();
                _fadeFromImage = null;
                _renderedImage?.Dispose();
                _renderedImage = null;
                _renderedFadeFromImage?.Dispose();
                _renderedFadeFromImage = null;
            }
            base.Dispose(disposing);
        }

        private void EnsureRenderCache()
        {
            if (!_renderCacheDirty && _renderedSize == ClientSize && _renderedImage != null) return;
            if (Width <= 0 || Height <= 0 || Image == null) return;

            _renderedImage?.Dispose();
            _renderedFadeFromImage?.Dispose();

            _renderedImage = RenderCoverBitmap(Image);
            _renderedFadeFromImage = _fadeActive && _fadeFromImage != null
                ? RenderCoverBitmap(_fadeFromImage)
                : null;
            _renderedSize = ClientSize;
            _renderCacheDirty = false;
        }

        private Bitmap RenderCoverBitmap(Image image)
        {
            var bitmap = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using var g = Graphics.FromImage(bitmap);
            g.Clear(Color.Black);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighSpeed;
            var src = GetCoverSourceRect(image);
            using var attributes = new System.Drawing.Imaging.ImageAttributes();
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            g.DrawImage(
                image,
                new Rectangle(-1, -1, Width + 2, Height + 2),
                src.X,
                src.Y,
                src.Width,
                src.Height,
                GraphicsUnit.Pixel,
                attributes);
            return bitmap;
        }

        private static void DrawRenderedImage(Graphics g, Image image, float alpha)
        {
            if (image == null || alpha <= 0F) return;

            if (alpha >= 0.999F)
            {
                g.DrawImageUnscaled(image, 0, 0);
                return;
            }

            using var attributes = new System.Drawing.Imaging.ImageAttributes();
            var matrix = new System.Drawing.Imaging.ColorMatrix
            {
                Matrix33 = Math.Max(0F, Math.Min(1F, alpha))
            };
            attributes.SetColorMatrix(matrix, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);
            g.DrawImage(
                image,
                new Rectangle(0, 0, image.Width, image.Height),
                0,
                0,
                image.Width,
                image.Height,
                GraphicsUnit.Pixel,
                attributes);
        }

        private RectangleF GetCoverSourceRect(Image image)
        {
            // cover 模式：铺满控件，居中裁剪
            float srcRatio = (float)image.Width / image.Height;
            float dstRatio = (float)Width / Height;
            if (srcRatio > dstRatio)
            {
                float srcW = image.Height * dstRatio;
                float srcX = (image.Width - srcW) / 2f;
                return new RectangleF(srcX, 0, srcW, image.Height);
            }

            float srcH = image.Width / dstRatio;
            float srcY = (image.Height - srcH) / 2f;
            return new RectangleF(0, srcY, image.Width, srcH);
        }
    }
}
