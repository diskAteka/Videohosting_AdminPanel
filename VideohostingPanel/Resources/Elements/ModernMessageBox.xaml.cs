using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace VideohostingPanel.Resources.Elements
{
    public partial class ModernMessageBox : Window
    {
        private MessageBoxResult _result = MessageBoxResult.None;

        public MessageBoxResult Result => _result;

        public ModernMessageBox()
        {
            InitializeComponent();
            Loaded += (s, e) => PlayShowAnimation();
        }

        public static MessageBoxResult Show(string message, string title = "Информация")
        {
            return Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static MessageBoxResult ShowConfirm(string message, string title = "Подтверждение")
        {
            return Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        }

        public static MessageBoxResult ShowError(string message, string title = "Ошибка")
        {
            return Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static MessageBoxResult ShowWarning(string message, string title = "Предупреждение")
        {
            return Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public static MessageBoxResult Show(string message, string title,
            MessageBoxButton buttons, MessageBoxImage icon)
        {
            var msgBox = new ModernMessageBox();
            msgBox.Title = title;
            msgBox.SetMessage(message);
            msgBox.SetIcon(icon);
            msgBox.SetButtons(buttons);
            msgBox.ShowDialog();
            return msgBox.Result;
        }

        private void SetIcon(MessageBoxImage icon)
        {
            string imagePath = "Resources/Images/";

            switch (icon)
            {
                case MessageBoxImage.Information:
                    imagePath += "ic_info.png";
                    break;
                case MessageBoxImage.Question:
                    imagePath += "ic_question.png";
                    break;
                case MessageBoxImage.Error:
                    imagePath += "ic_error.png";
                    break;
                case MessageBoxImage.Warning:
                    imagePath += "ic_warning.png";
                    break;
            }

            try
            {
                var uri = new Uri($"pack://application:,,,/{imagePath}");
                var bitmap = new BitmapImage(uri);
                IconImage.Source = bitmap;
                IconImage.Visibility = Visibility.Visible;
            }
            catch (Exception)
            {
                ShowFallbackIcon(icon);
            }
        }

        private void ShowFallbackIcon(MessageBoxImage icon)
        {
            var fallbackText = new TextBlock
            {
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x00)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };

            fallbackText.Text = icon switch
            {
                MessageBoxImage.Information => "\u24D8", // ⓘ
                MessageBoxImage.Question => "?",
                MessageBoxImage.Error => "\u2715",       // ✕
                MessageBoxImage.Warning => "\u26A0",     // ⚠
                _ => "?"
            };

            if (IconImage.Parent is Border parent)
            {
                parent.Child = fallbackText;
            }
        }

        private void SetMessage(string message)
        {
            MessageText.Text = message;
        }

        private void SetButtons(MessageBoxButton buttons)
        {
            ButtonPanel.Children.Clear();

            switch (buttons)
            {
                case MessageBoxButton.OK:
                    CreateButton("OK", "ПРИНЯТЬ", MessageBoxResult.OK, true);
                    break;

                case MessageBoxButton.OKCancel:
                    CreateButton("OK", "ПРИНЯТЬ", MessageBoxResult.OK, true);
                    CreateButton("Cancel", "ОТМЕНА", MessageBoxResult.Cancel, false);
                    break;

                case MessageBoxButton.YesNo:
                    CreateButton("Yes", "ДА", MessageBoxResult.Yes, true);
                    CreateButton("No", "НЕТ", MessageBoxResult.No, false);
                    break;

                case MessageBoxButton.YesNoCancel:
                    CreateButton("Yes", "ДА", MessageBoxResult.Yes, true);
                    CreateButton("No", "НЕТ", MessageBoxResult.No, false);
                    CreateButton("Cancel", "ОТМЕНА", MessageBoxResult.Cancel, false);
                    break;
            }
        }

        private void CreateButton(string name, string content, MessageBoxResult result, bool isPrimary)
        {
            var button = new Button
            {
                Content = content,
                Style = isPrimary
                    ? (Style)FindResource("PrimaryActionButtonStyle")
                    : (Style)FindResource("SecondaryActionButtonStyle")
            };

            // Анимация масштабирования при наведении
            button.MouseEnter += OnButtonMouseEnter;
            button.MouseLeave += OnButtonMouseLeave;
            button.Click += (s, e) =>
            {
                _result = result;
                PlayCloseAnimation();
            };

            ButtonPanel.Children.Add(button);
        }

        private void OnButtonMouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Button button)
            {
                AnimateScale(button, 1.02);
            }
        }

        private void OnButtonMouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Button button)
            {
                AnimateScale(button, 1.0);
            }
        }

        private void AnimateScale(Button button, double toValue)
        {
            var scaleTransform = button.RenderTransform as ScaleTransform;
            if (scaleTransform == null)
            {
                scaleTransform = new ScaleTransform(1.0, 1.0);
                button.RenderTransform = scaleTransform;
            }

            var duration = TimeSpan.FromSeconds(0.2);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(toValue, duration));
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(toValue, duration));
        }

        private void PlayShowAnimation()
        {
            var opacityAnimation = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(OpacityProperty, opacityAnimation);

            var scaleTransform = new ScaleTransform(0.9, 0.9);
            RenderTransform = scaleTransform;
            RenderTransformOrigin = new Point(0.5, 0.5);

            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(1, TimeSpan.FromSeconds(0.3))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(1, TimeSpan.FromSeconds(0.3))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
        }

        private void PlayCloseAnimation()
        {
            var animation = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.2))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            animation.Completed += (s, e) => Close();
            BeginAnimation(OpacityProperty, animation);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _result = MessageBoxResult.None;
            PlayCloseAnimation();
        }
    }
}