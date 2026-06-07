using System.Windows;

namespace TaskTrackerApp
{
    public partial class DocumentationWindow : Window
    {
        public DocumentationWindow()
        {
            InitializeComponent();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
