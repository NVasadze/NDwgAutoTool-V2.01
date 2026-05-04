using NDwgAutoTool.Helpers;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace NDwgAutoTool
{
    public partial class StyledPasswordWindow : Window
    {
        private readonly string _expectedPassword;

        public StyledPasswordWindow(string expectedPassword, Window? owner = null)
        {
            InitializeComponent();

            _expectedPassword = expectedPassword;

            if (owner != null)
                Owner = owner;

            Loaded += (_, _) => PasswordInput.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            CheckPassword();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void PasswordInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                CheckPassword();
        }

        private void CheckPassword()
        {
            if (PasswordInput.Password == _expectedPassword)
            {
                DialogResult = true;
                Close();
                return;
            }

            StyledMessageWindow.ShowMessage("Password Required", "Incorrect password.", this);
            PasswordInput.Clear();
            PasswordInput.Focus();
        }

        public static bool Ask(string expectedPassword, Window? owner = null)
        {
            var popup = new StyledPasswordWindow(expectedPassword, owner);
            return popup.ShowDialog() == true;
        }
        private void WindowDragArea_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                WindowMoveResizeHelper.Move(this);
        }
    }
}
