using System.Windows;

namespace TaskTrackerApp.Theming;

/// <summary>
/// Placeholder text for TextBox, drawn inside the control template with the same padding as the typed text,
/// so the placeholder and the text always start at exactly the same position.
/// </summary>
public static class TextBoxHelper
{
    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.RegisterAttached("Placeholder", typeof(string), typeof(TextBoxHelper), new PropertyMetadata(string.Empty));

    public static string GetPlaceholder(DependencyObject d) => (string)d.GetValue(PlaceholderProperty);
    public static void SetPlaceholder(DependencyObject d, string value) => d.SetValue(PlaceholderProperty, value);

    /// <summary>Show the placeholder in the accent colour (used by the Quick add box).</summary>
    public static readonly DependencyProperty AccentPlaceholderProperty =
        DependencyProperty.RegisterAttached("AccentPlaceholder", typeof(bool), typeof(TextBoxHelper), new PropertyMetadata(false));

    public static bool GetAccentPlaceholder(DependencyObject d) => (bool)d.GetValue(AccentPlaceholderProperty);
    public static void SetAccentPlaceholder(DependencyObject d, bool value) => d.SetValue(AccentPlaceholderProperty, value);
}
