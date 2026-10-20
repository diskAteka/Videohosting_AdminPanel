using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using VideohostingPanel.Resources.Elements;
using Application = System.Windows.Application;
using Point = System.Windows.Point;

namespace VideohostingPanel.Views.Windows
{
    public partial class ResetPasswordWindow : Window
    {
        private string _userPassword = string.Empty;
        private bool _isAnimating = false;

        // Данные пользователя (должны быть установлены перед показом окна)
        public string UserId { get; set; } = "USR-2024-7845";
        public string UserName { get; set; } = "Иван Петров";
        public string UserEmail { get; set; } = "ivan.petrov@example.com";

        // Новый пароль (доступен после успешного сброса)
        public string NewPassword { get; private set; } = string.Empty;

        public ResetPasswordWindow()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                PlayShowAnimation();
                UpdateUserData();
            };
        }

        /// <summary>
        /// Показать окно сброса пароля для конкретного пользователя.
        /// Возвращает новый пароль или null, если операция отменена.
        /// </summary>
        /// <param name="userId">ID пользователя</param>
        /// <param name="userName">Имя пользователя</param>
        /// <param name="userEmail">Email пользователя</param>
        /// <returns>Новый пароль или null при отмене</returns>
        public static string? ShowResetPasswordDialog(string userId, string userName, string userEmail)
        {
            var window = new ResetPasswordWindow
            {
                UserId = userId,
                UserName = userName,
                UserEmail = userEmail,
                Owner = Application.Current.MainWindow
            };

            if (window.ShowDialog() == true)
            {
                return window.NewPassword;
            }

            return null;
        }

        /// <summary>
        /// Обновление данных пользователя в UI
        /// </summary>
        private void UpdateUserData()
        {
            UserIdText.Text = UserId;
            UserNameText.Text = UserName;
            UserEmailText.Text = UserEmail;
        }

        /// <summary>
        /// Обработчик изменения пароля
        /// </summary>
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _userPassword = PasswordBox.Password;
        }

        /// <summary>
        /// Кнопка сброса пароля
        /// </summary>
        private void ResetPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            // Валидация пароля
            if (string.IsNullOrEmpty(_userPassword))
            {
                ShowError("Пожалуйста, введите новый пароль");
                FlashPasswordBorder();
                return;
            }

            if (_userPassword.Length < 6 || _userPassword.Length > 16)
            {
                ShowError("Пароль должен содержать от 6 до 16 символов");
                FlashPasswordBorder();
                return;
            }

            // Сохраняем новый пароль
            NewPassword = _userPassword;

            // Здесь должна быть логика сброса пароля
            // Например: UserService.ResetPassword(UserId, NewPassword);

            ModernMessageBox.Show($"Пароль для пользователя {UserName} успешно изменен!",
                            "Успех",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

            DialogResult = true;
            PlayCloseAnimation();
        }

        /// <summary>
        /// Анимация мигания обводки PasswordBox белым цветом (2 раза)
        /// </summary>
        private async void FlashPasswordBorder()
        {
            if (_isAnimating) return;
            _isAnimating = true;

            try
            {
                // Сохраняем оригинальную обводку
                var originalBorderBrush = PasswordBorder.BorderBrush;
                var originalBorderThickness = PasswordBorder.BorderThickness;

                // Создаем новую обводку белого цвета
                var whiteBrush = new SolidColorBrush(Colors.White);

                // Анимация мигания 2 раза
                for (int i = 0; i < 2; i++)
                {
                    // Включаем белую обводку
                    PasswordBorder.BorderBrush = whiteBrush;
                    PasswordBorder.BorderThickness = new Thickness(3);

                    // Небольшая задержка
                    await System.Threading.Tasks.Task.Delay(200);

                    // Возвращаем оригинальную обводку
                    PasswordBorder.BorderBrush = originalBorderBrush;
                    PasswordBorder.BorderThickness = originalBorderThickness;

                    // Задержка перед следующим миганием
                    await System.Threading.Tasks.Task.Delay(200);
                }

                // Добавляем анимацию встряхивания для дополнительного эффекта
                var shakeAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 5,
                    Duration = TimeSpan.FromMilliseconds(50),
                    AutoReverse = true,
                    RepeatBehavior = new RepeatBehavior(3)
                };

                var transform = new TranslateTransform();
                PasswordBorder.RenderTransform = transform;
                transform.BeginAnimation(TranslateTransform.XProperty, shakeAnimation);
            }
            finally
            {
                _isAnimating = false;
            }
        }

        /// <summary>
        /// Кнопка отмены
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            PlayCloseAnimation();
        }

        /// <summary>
        /// Показать ошибку с анимацией
        /// </summary>
        private void ShowError(string message)
        {
            // Здесь можно использовать кастомный MessageBox
            ModernMessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// Анимация появления окна
        /// </summary>
        private void PlayShowAnimation()
        {
            var animation = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            this.BeginAnimation(UIElement.OpacityProperty, animation);

            var scaleAnimation = new DoubleAnimation(0.9, 1, TimeSpan.FromSeconds(0.3))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var transform = new ScaleTransform(0.9, 0.9);
            RootBorder.RenderTransform = transform;
            RootBorder.RenderTransformOrigin = new Point(0.5, 0.5);

            transform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
        }

        /// <summary>
        /// Анимация закрытия окна
        /// </summary>
        private void PlayCloseAnimation()
        {
            var animation = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.2))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            animation.Completed += (s, e) => this.Close();
            this.BeginAnimation(UIElement.OpacityProperty, animation);
        }

        /// <summary>
        /// Перемещение окна
        /// </summary>
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        /// <summary>
        /// Закрытие окна
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            PlayCloseAnimation();
        }
    }
}