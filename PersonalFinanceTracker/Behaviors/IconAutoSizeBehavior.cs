using System;
using System.ComponentModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;

namespace PersonalFinanceTracker.Behaviors
{
    // Behavior that sizes an ImageButton to match a target text element's FontSize (optionally scaled).
    public class IconAutoSizeBehavior : Behavior<ImageButton>
    {
        // Optional: XAML name of the target text element (e.g., a Label) whose FontSize we mirror.
        public static readonly BindableProperty TargetElementNameProperty =
            BindableProperty.Create(nameof(TargetElementName), typeof(string), typeof(IconAutoSizeBehavior), default(string), propertyChanged: OnParamsChanged);

        // Multiplier applied to the target FontSize. 1.0 -> equal height, 0.9 -> slightly smaller, etc.
        public static readonly BindableProperty ScaleProperty =
            BindableProperty.Create(nameof(Scale), typeof(double), typeof(IconAutoSizeBehavior), 1.0, propertyChanged: OnParamsChanged);

        // Clamps for resulting icon size (device-independent units).
        public static readonly BindableProperty MinProperty =
            BindableProperty.Create(nameof(Min), typeof(double), typeof(IconAutoSizeBehavior), 12d, propertyChanged: OnParamsChanged);

        public static readonly BindableProperty MaxProperty =
            BindableProperty.Create(nameof(Max), typeof(double), typeof(IconAutoSizeBehavior), 32d, propertyChanged: OnParamsChanged);

        public string? TargetElementName
        {
            get => (string?)GetValue(TargetElementNameProperty);
            set => SetValue(TargetElementNameProperty, value);
        }

        public double Scale
        {
            get => (double)GetValue(ScaleProperty);
            set => SetValue(ScaleProperty, value);
        }

        public double Min
        {
            get => (double)GetValue(MinProperty);
            set => SetValue(MinProperty, value);
        }

        public double Max
        {
            get => (double)GetValue(MaxProperty);
            set => SetValue(MaxProperty, value);
        }

        private ImageButton? _icon;
        private VisualElement? _target; // typically a Label
        private bool _attached;

        protected override void OnAttachedTo(ImageButton bindable)
        {
            base.OnAttachedTo(bindable);
            _icon = bindable;
            _attached = true;

            // Re-evaluate on layout changes and orientation/density changes.
            bindable.SizeChanged += OnAnyChanged;
            DeviceDisplay.MainDisplayInfoChanged += OnDisplayInfoChanged;

            TryResolveTargetAndApply();
        }

        protected override void OnDetachingFrom(ImageButton bindable)
        {
            base.OnDetachingFrom(bindable);
            _attached = false;

            bindable.SizeChanged -= OnAnyChanged;
            DeviceDisplay.MainDisplayInfoChanged -= OnDisplayInfoChanged;

            if (_target is not null)
                _target.PropertyChanged -= OnTargetPropertyChanged;

            _icon = null;
            _target = null;
        }

        private static void OnParamsChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is IconAutoSizeBehavior bh && bh._attached)
                bh.TryResolveTargetAndApply();
        }

        private void OnDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e) => Apply();
        private void OnAnyChanged(object? sender, EventArgs e) => Apply();

        private void OnTargetPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Label.FontSize) || e.PropertyName == "FontSize")
                Apply();
        }

        private void TryResolveTargetAndApply()
        {
            if (_icon is null) return;

            // Unhook previous
            if (_target is not null)
                _target.PropertyChanged -= OnTargetPropertyChanged;

            _target = FindTargetElement(_icon, TargetElementName);

            if (_target is not null)
                _target.PropertyChanged += OnTargetPropertyChanged;

            Apply();
        }

        private void Apply()
        {
            if (_icon is null) return;

            double baseSize = 16; // fallback if no target is found

            // Compute scaled & clamped size
            double computed = baseSize * Scale;
            double clamped = Math.Max(Min, Math.Min(Max, computed));

            _icon.HeightRequest = clamped;
            _icon.WidthRequest = clamped;
        }

        // Try to find a reasonable text element to follow
        private static VisualElement? FindTargetElement(Element start, string? name)
        {
            // 1) If name is provided, search upwards then FindByName
            if (!string.IsNullOrWhiteSpace(name))
            {
                var owner = FindNearestNamescope(start);
                if (owner is not null)
                {
                    var byName = owner.FindByName<VisualElement>(name);
                    if (byName is not null)
                        return byName;
                }
            }

            // 2) Heuristic: same parent, same grid cell first, otherwise first Label sibling
            if (start.Parent is Layout parentLayout)
            {
                try
                {
                    int row = Grid.GetRow((BindableObject)start);
                    int col = Grid.GetColumn((BindableObject)start);

                    // Prefer a Label in the same Grid cell
                    var sameCell = parentLayout.Children
                        .OfType<VisualElement>()
                        .Where(v => v != start)
                        .Where(v => Grid.GetRow(v) == row && Grid.GetColumn(v) == col)
                        .OfType<Label>()
                        .Cast<VisualElement>()
                        .FirstOrDefault();

                    if (sameCell is not null)
                        return sameCell;

                    // Otherwise, any Label sibling
                    var anyLabel = parentLayout.Children.OfType<Label>().Cast<VisualElement>().FirstOrDefault();
                    if (anyLabel is not null)
                        return anyLabel;
                }
                catch { /* ignore grid queries when not in Grid */ }
            }

            return null;
        }

        private static Element? FindNearestNamescope(Element start)
        {
            // Walk up the tree; in MAUI, FindByName works from any Element that is the namescope root.
            Element? cur = start;
            while (cur is not null)
            {
                // ContentPage / Layout / Grid are fine owners for FindByName
                if (cur is Element e)
                    return e;
                cur = cur.Parent;
            }
            return null;
        }
    }
}
