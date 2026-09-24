using System.Windows;
using TaskTrackerApp.Theming;

namespace TaskTrackerApp
{
    public partial class DocumentationWindow : Window
    {
        public DocumentationWindow()
        {
            InitializeComponent();
            WindowEffects.Attach(this); // Mica + themed title bar on Windows 11
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
