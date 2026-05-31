using System.Windows;
using System.Windows.Controls;

namespace VideohostingPanel.Services
{
    public static class PasswordBoxHelper
    {
        public static readonly DependencyProperty BindPasswordProperty =
            DependencyProperty.RegisterAttached("BindPassword", typeof(string), typeof(PasswordBoxHelper),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBindPasswordChanged));

        public static string GetBindPassword(DependencyObject obj) => (string)obj.GetValue(BindPasswordProperty);
        public static void SetBindPassword(DependencyObject obj, string value) => obj.SetValue(BindPasswordProperty, value);

        private static void OnBindPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PasswordBox passwordBox)
            {
                passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;
                if (e.NewValue != null && passwordBox.Password != (string)e.NewValue)
                    passwordBox.Password = (string)e.NewValue;
                passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
            }
        }

        private static void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
                SetBindPassword(passwordBox, passwordBox.Password);
        }
    }
}