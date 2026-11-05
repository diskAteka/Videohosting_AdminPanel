using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace VideohostingPanel.Services
{
    public static class PasswordBoxHelper
    {
        public static readonly DependencyProperty BindPasswordProperty =
            DependencyProperty.RegisterAttached("BindPassword", typeof(string), typeof(PasswordBoxHelper),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBindPasswordChanged));

        private static readonly DependencyProperty IsUpdatingProperty =
            DependencyProperty.RegisterAttached("IsUpdating", typeof(bool), typeof(PasswordBoxHelper),
                new PropertyMetadata(false));

        static PasswordBoxHelper()
        {
            // Класс-обработчик срабатывает для ВСЕХ PasswordBox, даже если
            // OnBindPasswordChanged не был вызван (например, когда исходное
            // значение совпадает со значением по умолчанию).
            EventManager.RegisterClassHandler(
                typeof(PasswordBox),
                PasswordBox.PasswordChangedEvent,
                new RoutedEventHandler(OnPasswordBoxPasswordChanged));
        }

        public static string? GetBindPassword(DependencyObject obj) => (string?)obj.GetValue(BindPasswordProperty);
        public static void SetBindPassword(DependencyObject obj, string? value) => obj.SetValue(BindPasswordProperty, value);

        private static void OnBindPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not PasswordBox passwordBox || (bool)passwordBox.GetValue(IsUpdatingProperty))
                return;

            passwordBox.SetValue(IsUpdatingProperty, true);

            if (e.NewValue is string newPassword && passwordBox.Password != newPassword)
            {
                passwordBox.Password = newPassword;
            }

            passwordBox.SetValue(IsUpdatingProperty, false);
        }

        private static void OnPasswordBoxPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is not PasswordBox passwordBox || (bool)passwordBox.GetValue(IsUpdatingProperty))
                return;

            var binding = BindingOperations.GetBindingExpression(passwordBox, BindPasswordProperty);
            if (binding == null)
            {
                return;
            }

            passwordBox.SetValue(IsUpdatingProperty, true);
            passwordBox.SetCurrentValue(BindPasswordProperty, passwordBox.Password);
            passwordBox.SetValue(IsUpdatingProperty, false);

            binding.UpdateSource();
        }
    }
}
