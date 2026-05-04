// Personal Finance Tracker
// File: PersonalFinanceTracker/Behaviors/ResponsiveFontBehavior.cs
// Purpose: Provides reusable UI behavior for responsive MAUI views.

using System;
using System.Reflection; 
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;

namespace PersonalFinanceTracker.Behaviors
{
    // Applies responsive font sizing to text-bearing controls.
    public class ResponsiveFontBehavior : Behavior<VisualElement>
    {
        // Public enum so XAML can set it by name (Width/Height/Min).
        public enum BaseDimension { Width, Height, Min }

        public static readonly BindableProperty RatioProperty =
            BindableProperty.Create(nameof(Ratio), typeof(double), typeof(ResponsiveFontBehavior), 0.05d);

        public static readonly BindableProperty MinFontSizeProperty =
            BindableProperty.Create(nameof(MinFontSize), typeof(double), typeof(ResponsiveFontBehavior), 12d);

        public static readonly BindableProperty MaxFontSizeProperty =
            BindableProperty.Create(nameof(MaxFontSize), typeof(double), typeof(ResponsiveFontBehavior), 48d);

        public static readonly BindableProperty DimensionProperty =
            BindableProperty.Create(nameof(Dimension), typeof(BaseDimension), typeof(ResponsiveFontBehavior), BaseDimension.Width);

        public double Ratio { get => (double)GetValue(RatioProperty); set => SetValue(RatioProperty, value); }
        public double MinFontSize { get => (double)GetValue(MinFontSizeProperty); set => SetValue(MinFontSizeProperty, value); }
        public double MaxFontSize { get => (double)GetValue(MaxFontSizeProperty); set => SetValue(MaxFontSizeProperty, value); }
        public BaseDimension Dimension { get => (BaseDimension)GetValue(DimensionProperty); set => SetValue(DimensionProperty, value); }

        private VisualElement? _associated;

        protected override void OnAttachedTo(VisualElement bindable)
        {
            base.OnAttachedTo(bindable);
            _associated = bindable;
            bindable.SizeChanged += OnSizeChanged;
            DeviceDisplay.MainDisplayInfoChanged += OnDisplayInfoChanged;
            ApplyFontSize();
        }

        protected override void OnDetachingFrom(VisualElement bindable)
        {
            base.OnDetachingFrom(bindable);
            bindable.SizeChanged -= OnSizeChanged;
            DeviceDisplay.MainDisplayInfoChanged -= OnDisplayInfoChanged;
            _associated = null;
        }

        // Expose a public method so attached-property glue can trigger recalculation.
        public void Recalculate() => ApplyFontSize();

        private void OnSizeChanged(object? sender, EventArgs e) => ApplyFontSize();
        private void OnDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e) => ApplyFontSize();

        private void ApplyFontSize()
        {
            if (_associated is null)
                return;

            double width = _associated.Width;
            double height = _associated.Height;

            // Fallback to screen logical size if not laid out yet.
            if (double.IsNaN(width) || width <= 0 || double.IsNaN(height) || height <= 0)
            {
                var info = DeviceDisplay.MainDisplayInfo;
                width = info.Width / info.Density;
                height = info.Height / info.Density;
            }

            double basis = Dimension switch
            {
                BaseDimension.Height => height,
                BaseDimension.Min => Math.Min(width, height),
                _ => width,
            };

            double computed = basis * Ratio;
            double clamped = Math.Max(MinFontSize, Math.Min(MaxFontSize, computed));

            // Fast paths for common controls.
            if (TrySetFontSizeByType(_associated, clamped))
                return;

            // BindableProperty fallback (FontSizeProperty).
            if (_associated is BindableObject bo && TrySetFontSizeByBindableProperty(bo, clamped))
                return;

            // Last resort: reflection to a public "FontSize" property.
            TrySetFontSizeByReflection(_associated, clamped);
        }

        private static bool TrySetFontSizeByType(VisualElement v, double size)
        {
            if (v is Label lbl) { lbl.FontSize = size; return true; }
            if (v is Button btn) { btn.FontSize = size; return true; }
            if (v is Entry entry) { entry.FontSize = size; return true; }
            if (v is Editor editor) { editor.FontSize = size; return true; }
            if (v is SearchBar searchBar) { searchBar.FontSize = size; return true; }
            if (v is Picker picker) { picker.FontSize = size; return true; }
            if (v is DatePicker dp) { dp.FontSize = size; return true; }
            if (v is TimePicker tp) { tp.FontSize = size; return true; }
            if (v is RadioButton rb) { rb.FontSize = size; return true; }
            return false;
        }

        private static bool TrySetFontSizeByBindableProperty(BindableObject v, double size)
        {
            var field = v.GetType().GetField("FontSizeProperty",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (field is not null && typeof(BindableProperty).IsAssignableFrom(field.FieldType))
            {
                var bp = (BindableProperty?)field.GetValue(null);
                if (bp is not null)
                {
                    v.SetValue(bp, size);
                    return true;
                }
            }
            return false;
        }

        private static void TrySetFontSizeByReflection(object v, double size)
        {
            var prop = v.GetType().GetProperty("FontSize", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite && prop.PropertyType == typeof(double))
            {
                try { prop.SetValue(v, size); } catch { /* ignore */ }
            }
        }
    }
}

