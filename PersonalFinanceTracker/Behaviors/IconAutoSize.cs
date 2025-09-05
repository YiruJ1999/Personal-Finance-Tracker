using System.Linq;
using Microsoft.Maui.Controls;

namespace PersonalFinanceTracker.Behaviors
{
    // Attached properties to enable/configure IconAutoSizeBehavior from XAML styles.
    public static class IconAutoSize
    {
        public static readonly BindableProperty EnableProperty =
            BindableProperty.CreateAttached("Enable", typeof(bool), typeof(IconAutoSize), false,
                propertyChanged: OnEnableChanged);

        public static readonly BindableProperty TargetElementNameProperty =
            BindableProperty.CreateAttached("TargetElementName", typeof(string), typeof(IconAutoSize), default(string),
                propertyChanged: (b, o, n) => UpdateBehavior(b, bh => bh.TargetElementName = (string?)n));

        public static readonly BindableProperty ScaleProperty =
            BindableProperty.CreateAttached("Scale", typeof(double), typeof(IconAutoSize), 1.0,
                propertyChanged: (b, o, n) => UpdateBehavior(b, bh => bh.Scale = (double)n));

        public static readonly BindableProperty MinProperty =
            BindableProperty.CreateAttached("Min", typeof(double), typeof(IconAutoSize), 12d,
                propertyChanged: (b, o, n) => UpdateBehavior(b, bh => bh.Min = (double)n));

        public static readonly BindableProperty MaxProperty =
            BindableProperty.CreateAttached("Max", typeof(double), typeof(IconAutoSize), 32d,
                propertyChanged: (b, o, n) => UpdateBehavior(b, bh => bh.Max = (double)n));

        public static bool GetEnable(BindableObject view) => (bool)view.GetValue(EnableProperty);
        public static void SetEnable(BindableObject view, bool value) => view.SetValue(EnableProperty, value);

        public static string? GetTargetElementName(BindableObject view) => (string?)view.GetValue(TargetElementNameProperty);
        public static void SetTargetElementName(BindableObject view, string? value) => view.SetValue(TargetElementNameProperty, value);

        public static double GetScale(BindableObject view) => (double)view.GetValue(ScaleProperty);
        public static void SetScale(BindableObject view, double value) => view.SetValue(ScaleProperty, value);

        public static double GetMin(BindableObject view) => (double)view.GetValue(MinProperty);
        public static void SetMin(BindableObject view, double value) => view.SetValue(MinProperty, value);

        public static double GetMax(BindableObject view) => (double)view.GetValue(MaxProperty);
        public static void SetMax(BindableObject view, double value) => view.SetValue(MaxProperty, value);

        private static void OnEnableChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is not ImageButton icon) return;

            if ((bool)newValue)
            {
                var bh = icon.Behaviors.OfType<IconAutoSizeBehavior>().FirstOrDefault();
                if (bh is null)
                {
                    bh = new IconAutoSizeBehavior
                    {
                        TargetElementName = GetTargetElementName(bindable),
                        Scale = GetScale(bindable),
                        Min = GetMin(bindable),
                        Max = GetMax(bindable),
                    };
                    icon.Behaviors.Add(bh);
                }
            }
            else
            {
                var existing = icon.Behaviors.OfType<IconAutoSizeBehavior>().FirstOrDefault();
                if (existing != null) icon.Behaviors.Remove(existing);
            }
        }

        private static void UpdateBehavior(BindableObject bindable, System.Action<IconAutoSizeBehavior> mutator)
        {
            if (bindable is not ImageButton icon) return;

            var bh = icon.Behaviors.OfType<IconAutoSizeBehavior>().FirstOrDefault();
            if (bh is null)
            {
                if (!GetEnable(bindable)) return;
                bh = new IconAutoSizeBehavior();
                icon.Behaviors.Add(bh);
            }

            mutator(bh);
        }
    }
}
