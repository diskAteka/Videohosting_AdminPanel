using System.Windows;
using System.Windows.Controls;

namespace VideohostingPanel.Services
{
    public static class PasswordBoxHelper
    {
        public static readonly DependencyProperty BindPasswordProperty =
            DependencyProperty.RegisterAttached(
                "BindPassword",
                typeof(string),
                typeof(PasswordBoxHelper),
                new FrameworkPropertyMetadata(string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnBindPasswordChanged));

        public static string GetBindPassword(DependencyObject obj)
        {
            return (string)obj.GetValue(BindPasswordProperty);
        }

        public static void SetBindPassword(DependencyObject obj, string value)
        {
            obj.SetValue(BindPasswordProperty, value);
        }

        private static readonly DependencyProperty IsUpdatingProperty =
            DependencyProperty.RegisterAttached(
                "IsUpdating",
                typeof(bool),
                typeof(PasswordBoxHelper),
                new PropertyMetadata(false));

        private static bool GetIsUpdating(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsUpdatingProperty);
        }

        private static void SetIsUpdating(DependencyObject obj, bool value)
        {
            obj.SetValue(IsUpdatingProperty, value);
        }

        private static void OnBindPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PasswordBox passwordBox)
            {
                // Отписываемся от старого обработчика
                passwordBox.PasswordChanged -= HandlePasswordChanged;

                if (!GetIsUpdating(passwordBox))
                {
                    // Обновляем пароль в контроле только если он загружен
                    if (passwordBox.IsLoaded)
                    {
                        SetIsUpdating(passwordBox, true);
                        passwordBox.Password = e.NewValue?.ToString() ?? string.Empty;
                        SetIsUpdating(passwordBox, false);
                    }
                    else
                    {
                        // Если контрол еще не загружен, подписываемся на событие загрузки
                        passwordBox.Loaded += (s, args) =>
                        {
                            SetIsUpdating(passwordBox, true);
                            passwordBox.Password = e.NewValue?.ToString() ?? string.Empty;
                            SetIsUpdating(passwordBox, false);
                        };
                    }
                }

                // Подписываемся на изменения
                passwordBox.PasswordChanged += HandlePasswordChanged;
            }
        }

        private static void HandlePasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                // Предотвращаем рекурсивное обновление
                SetIsUpdating(passwordBox, true);
                SetBindPassword(passwordBox, passwordBox.Password);
                SetIsUpdating(passwordBox, false);
            }
        }
    }
}