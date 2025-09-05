using System.Linq; 
using Microsoft.Maui.Controls;

namespace PersonalFinanceTracker.Behaviors
{
    // Attached properties so you can enable/configure the behavior from a Style.
    public static class ResponsiveFont
    {
        // Enable/disable behavior
        public static readonly BindableProperty EnableProperty =
            BindableProperty.CreateAttached(
                "Enable", typeof(bool), typeof(ResponsiveFont), false,
                propertyChanged: OnEnableChanged);

        // Mirror behavior properties
        public static readonly BindableProperty RatioProperty =
            BindableProperty.CreateAttached(
                "Ratio", typeof(double), typeof(ResponsiveFont), 0.05d,
                propertyChanged: (b, o, n) => UpdateBehavior(b, bh => bh.Ratio = (double)n));

        public static readonly BindableProperty MinFontSizeProperty =
            BindableProperty.CreateAttached(
                "MinFontSize", typeof(double), typeof(ResponsiveFont), 12d,
                propertyChanged: (b, o, n) => UpdateBehavior(b, bh => bh.MinFontSize = (double)n));

        public static readonly BindableProperty MaxFontSizeProperty =
            BindableProperty.CreateAttached(
                "MaxFontSize", typeof(double), typeof(ResponsiveFont), 48d,
                propertyChanged: (b, o, n) => UpdateBehavior(b, bh => bh.MaxFontSize = (double)n));

        public static readonly BindableProperty DimensionProperty =
            BindableProperty.CreateAttached(
                "Dimension", typeof(ResponsiveFontBehavior.BaseDimension), typeof(ResponsiveFont),
                ResponsiveFontBehavior.BaseDimension.Width,
                propertyChanged: (b, o, n) => UpdateBehavior(b, bh => bh.Dimension = (ResponsiveFontBehavior.BaseDimension)n));

        // Get/Set required by attached property pattern
        public static bool GetEnable(BindableObject view) => (bool)view.GetValue(EnableProperty);
        public static void SetEnable(BindableObject view, bool value) => view.SetValue(EnableProperty, value);

        public static double GetRatio(BindableObject view) => (double)view.GetValue(RatioProperty);
        public static void SetRatio(BindableObject view, double value) => view.SetValue(RatioProperty, value);

        public static double GetMinFontSize(BindableObject view) => (double)view.GetValue(MinFontSizeProperty);
        public static void SetMinFontSize(BindableObject view, double value) => view.SetValue(MinFontSizeProperty, value);

        public static double GetMaxFontSize(BindableObject view) => (double)view.GetValue(MaxFontSizeProperty);
        public static void SetMaxFontSize(BindableObject view, double value) => view.SetValue(MaxFontSizeProperty, value);

        public static ResponsiveFontBehavior.BaseDimension GetDimension(BindableObject view) =>
            (ResponsiveFontBehavior.BaseDimension)view.GetValue(DimensionProperty);
        public static void SetDimension(BindableObject view, ResponsiveFontBehavior.BaseDimension value) =>
            view.SetValue(DimensionProperty, value);

        private static void OnEnableChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is not VisualElement ve) return;

            if ((bool)newValue)
            {
                // Attach if missing
                var bh = ve.Behaviors.OfType<ResponsiveFontBehavior>().FirstOrDefault();
                if (bh is null)
                {
                    bh = new ResponsiveFontBehavior
                    {
                        Ratio = GetRatio(bindable),
                        MinFontSize = GetMinFontSize(bindable),
                        MaxFontSize = GetMaxFontSize(bindable),
                        Dimension = GetDimension(bindable),
                    };
                    ve.Behaviors.Add(bh);
                }
                bh.Recalculate();
            }
            else
            {
                // Detach if present
                var existing = ve.Behaviors.OfType<ResponsiveFontBehavior>().FirstOrDefault();
                if (existing != null) ve.Behaviors.Remove(existing);
            }
        }

        // Ensure behavior exists (if Enable is true) and apply a change, then recalc.
        private static void UpdateBehavior(BindableObject bindable, System.Action<ResponsiveFontBehavior> mutator)
        {
            if (bindable is not VisualElement ve) return;

            var bh = ve.Behaviors.OfType<ResponsiveFontBehavior>().FirstOrDefault();
            if (bh is null)
            {
                if (!GetEnable(bindable)) return; // not enabled yet
                bh = new ResponsiveFontBehavior();
                ve.Behaviors.Add(bh);
            }

            mutator(bh);
            bh.Recalculate();
        }
    }
}
