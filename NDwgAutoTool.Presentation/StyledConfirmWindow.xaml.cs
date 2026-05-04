using NDwgAutoTool.Helpers;
using System.Windows;

namespace NDwgAutoTool
{
    public partial class StyledConfirmWindow : Window
    {
        public StyledConfirmWindow(string title, string question, string details = "", Window? owner = null)
        {
            InitializeComponent();

            TitleTextBlock.Text = title;
            QuestionTextBlock.Text = question;
            DetailsTextBox.Text = details ?? "";

            if (string.IsNullOrWhiteSpace(details))
                DetailsTextBox.Visibility = Visibility.Collapsed;

            if (owner != null)
                Owner = owner;
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public static bool ShowConfirm(string title, string question, string details = "", Window? owner = null)
        {
            var popup = new StyledConfirmWindow(title, question, details, owner);
            return popup.ShowDialog() == true;
        }
    }
}