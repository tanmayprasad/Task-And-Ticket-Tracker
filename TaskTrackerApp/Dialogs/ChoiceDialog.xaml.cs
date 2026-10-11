using System.Windows;
using System.Windows.Controls;
using TaskTrackerApp.Theming;

namespace TaskTrackerApp.Dialogs;

public enum ChoiceKind { Standard, Primary, Danger }

public sealed record Choice(string Text, ChoiceKind Kind = ChoiceKind.Standard);

/// <summary>A themed replacement for MessageBox with custom buttons. Returns the chosen index, or -1 when closed.</summary>
public partial class ChoiceDialog : Window
{
    private int _result = -1;

    private ChoiceDialog()
    {
        InitializeComponent();
        WindowEffects.Attach(this); // Mica + themed title bar on Windows 11
    }

    /// <param name="cancelIndex">The button Esc picks (usually Cancel), or -1.</param>
    public static int Show(Window owner, string heading, string message, int cancelIndex, params Choice[] choices)
    {
        var dialog = new ChoiceDialog { Owner = owner };
        dialog.HeadingText.Text = heading;
        dialog.MessageText.Text = message;
        for (int i = 0; i < choices.Length; i++)
        {
            int index = i;
            var button = new Button
            {
                Content = choices[i].Text,
                Margin = new Thickness(i == 0 ? 0 : 8, 0, 0, 0),
                Style = (Style)dialog.FindResource(choices[i].Kind switch
                {
                    ChoiceKind.Primary => "PrimaryButton",
                    ChoiceKind.Danger => "DangerButton",
                    _ => "StandardButton",
                }),
                IsDefault = choices[i].Kind == ChoiceKind.Primary,
                IsCancel = index == cancelIndex,
            };
            button.Click += (s, e) => { dialog._result = index; dialog.Close(); };
            dialog.ButtonRow.Children.Add(button);
        }
        dialog.ShowDialog();
        return dialog._result;
    }
}
