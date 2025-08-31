using Microsoft.Maui;                      
using Microsoft.Maui.Controls;              
using Microsoft.Maui.Graphics;              
using Microsoft.Maui.Graphics.Platform;     
using System;
using System.IO;
using System.Threading.Tasks;


using GImage = Microsoft.Maui.Graphics.IImage;
using GColor = Microsoft.Maui.Graphics.Color;

namespace PersonalFinanceTracker.Controls
{
    public class TiledBackgroundView : GraphicsView, IDrawable
    {
        // File name with extension, e.g. "background_grass.jpg"
        // Recommended: Resources/Raw/<file> with Build Action = MauiAsset
        public static readonly BindableProperty SourceProperty =
            BindableProperty.Create(nameof(Source), typeof(string), typeof(TiledBackgroundView),
                default(string), propertyChanged: OnSourceChanged);

        // Tile size (DIPs)
        public static readonly BindableProperty TileWidthProperty =
            BindableProperty.Create(nameof(TileWidth), typeof(double), typeof(TiledBackgroundView),
                24.0, propertyChanged: OnVisualChanged);

        public static readonly BindableProperty TileHeightProperty =
            BindableProperty.Create(nameof(TileHeight), typeof(double), typeof(TiledBackgroundView),
                24.0, propertyChanged: OnVisualChanged);

        // Extra spacing around each tile in DIPs
        public static readonly BindableProperty SpacingXProperty =
            BindableProperty.Create(nameof(SpacingX), typeof(double), typeof(TiledBackgroundView),
                8.0, propertyChanged: OnVisualChanged);

        public static readonly BindableProperty SpacingYProperty =
            BindableProperty.Create(nameof(SpacingY), typeof(double), typeof(TiledBackgroundView),
                8.0, propertyChanged: OnVisualChanged);

        // Bitmap alpha (0..1)
        public static readonly BindableProperty PatternOpacityProperty =
            BindableProperty.Create(nameof(PatternOpacity), typeof(double), typeof(TiledBackgroundView),
                0.10, propertyChanged: OnVisualChanged);

        // Non-null tint color (use Transparent to disable together with TintOpacity=0)
        public static readonly BindableProperty TintColorProperty =
            BindableProperty.Create(nameof(TintColor), typeof(GColor), typeof(TiledBackgroundView),
                Colors.Transparent, propertyChanged: OnVisualChanged);

        // Tint alpha (0..1); set 0 to disable
        public static readonly BindableProperty TintOpacityProperty =
            BindableProperty.Create(nameof(TintOpacity), typeof(double), typeof(TiledBackgroundView),
                0.0, propertyChanged: OnVisualChanged);

        private GImage? _image;

        public string? Source
        {
            get => (string?)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public double TileWidth
        {
            get => (double)GetValue(TileWidthProperty);
            set => SetValue(TileWidthProperty, value);
        }

        public double TileHeight
        {
            get => (double)GetValue(TileHeightProperty);
            set => SetValue(TileHeightProperty, value);
        }

        public double SpacingX { 
            get => (double)GetValue(SpacingXProperty); 
            set => SetValue(SpacingXProperty, value); 
        }
        public double SpacingY {
            get => (double)GetValue(SpacingYProperty); 
            set => SetValue(SpacingYProperty, value); 
        }

        public double PatternOpacity
        {
            get => (double)GetValue(PatternOpacityProperty);
            set => SetValue(PatternOpacityProperty, value);
        }

        public GColor TintColor
        {
            get => (GColor)GetValue(TintColorProperty);
            set => SetValue(TintColorProperty, value);
        }

        public double TintOpacity
        {
            get => (double)GetValue(TintOpacityProperty);
            set => SetValue(TintOpacityProperty, value);
        }

        public TiledBackgroundView()
        {
            Drawable = this;
            InputTransparent = true; // let touches pass through
        }

        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();
            _ = LoadImageAsync();
        }

        private static void OnSourceChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var v = (TiledBackgroundView)bindable;
            _ = v.LoadImageAsync();
        }

        private static void OnVisualChanged(BindableObject bindable, object oldValue, object newValue)
        {
            ((TiledBackgroundView)bindable).Invalidate();
        }

        private async Task LoadImageAsync()
        {
            _image = null;

            if (string.IsNullOrWhiteSpace(Source))
            {
                Invalidate();
                return;
            }

            // Try as-is, then Resources/Raw/<file>
            Stream? s = await TryOpenAsync(Source)
                      ?? await TryOpenAsync(Path.Combine("Resources", "Raw", Source));

            if (s != null)
            {
                using (s)
                {
                    try { _image = PlatformImage.FromStream(s); }
                    catch { _image = null; }
                }
            }

            Invalidate();
        }

        private static async Task<Stream?> TryOpenAsync(string path)
        {
            try { return await FileSystem.OpenAppPackageFileAsync(path); }
            catch { return null; }
        }

        // IDrawable
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            if (_image is null) return;

            float w = (float)Math.Max(1, TileWidth);
            float h = (float)Math.Max(1, TileHeight);

            // Draw image tiles
            canvas.SaveState();
            canvas.Alpha = (float)Math.Clamp(PatternOpacity, 0, 1);

            float stepX = (float)(w + SpacingX);
            float stepY = (float)(h + SpacingY);

            for (float y = 0; y < dirtyRect.Height; y += stepY)
                for (float x = 0; x < dirtyRect.Width; x += stepX)
                    canvas.DrawImage(_image, x, y, w, h);

            canvas.RestoreState();

            // Optional tint overlay (only if visible)
            if (TintOpacity > 0 && TintColor.Alpha > 0)
            {
                canvas.SaveState();
                canvas.Alpha = (float)Math.Clamp(TintOpacity, 0, 1);
                canvas.FillColor = TintColor; // Graphics.Color
                for (float y = 0; y < dirtyRect.Height; y += stepY)
                    for (float x = 0; x < dirtyRect.Width; x += stepX)
                        canvas.FillRectangle(x, y, w, h);
                canvas.RestoreState();
            }
        }
    }
}
