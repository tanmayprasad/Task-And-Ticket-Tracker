using System;
using System.Windows;
using TaskTrackerApp.Theming;

namespace TaskTrackerApp.Dialogs;

/// <summary>Asks for a short text. <c>validate</c> returns the value to use, or throws ArgumentException with a message to show.</summary>
public partial class TextPromptDialog : Window
{
    private Func<string, string>? _validate;
    private string? _result;

    private TextPromptDialog()
    {
        InitializeComponent();
        WindowEffects.Attach(this);
        InputBox.TextChanged += (s, e) => ErrorText.Visibility = Visibility.Collapsed;
    }

    /// <returns>The validated text, or null when cancelled.</returns>
    public static string? Show(Window owner, string heading, string prompt, string okText, string initial, int maxLength, Func<string, string> validate)
    {
        var dialog = new TextPromptDialog { Owner = owner, _validate = validate };
        dialog.HeadingText.Text = heading;
        dialog.PromptText.Text = prompt;
        dialog.OkButton.Content = okText;
        dialog.InputBox.MaxLength = maxLength;
        dialog.InputBox.Text = initial;
        dialog.Loaded += (s, e) => { dialog.InputBox.Focus(); dialog.InputBox.SelectAll(); };
        dialog.ShowDialog();
        return dialog._result;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _result = _validate!(InputBox.Text);
            Close();
        }
        catch (ArgumentException ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
            InputBox.Focus();
        }
    }
}
