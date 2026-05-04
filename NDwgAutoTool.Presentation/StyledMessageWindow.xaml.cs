using NDwgAutoTool.Helpers;
using System.Windows;

namespace NDwgAutoTool
{
    public partial class StyledMessageWindow : Window
    {
        public StyledMessageWindow(string title, string message, Window? owner = null)
        {
            InitializeComponent();

            TitleTextBlock.Text = title;
            MessageTextBlock.Text = message;

            if (owner != null)
                Owner = owner;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public static void ShowMessage(string title, string message, Window? owner = null)
        {
            var popup = new StyledMessageWindow(title, message, owner);
            popup.ShowDialog();
        }

        private void WindowDragArea_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                WindowMoveResizeHelper.Move(this);
        }        
    }
}