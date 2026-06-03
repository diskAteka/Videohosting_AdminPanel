using System;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace VideohostingPanel.Resources.Elements
{
    /// <summary>
    /// Кастомный MessageBox в современном стиле
    /// </summary>
    public partial class ModernMessageBox : Window
    {
        private MessageBoxResult _result = MessageBoxResult.None;
        private bool _isDragging = false;
        private Point _startPoint;

        public MessageBoxResult Result => _result;

        public ModernMessageBox()
        {
            InitializeComponent();
            Loaded += (s, e) => PlayShowAnimation();
        }

        /// <summary>
        /// Показать информационное сообщение
        /// </summary>
        public static MessageBoxResult Show(string message, string title = "Информация")
        {
            return Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Показать сообщение с выбором Да/Нет
        /// </summary>
        public static MessageBoxResult ShowConfirm(string message, string title = "Подтверждение")
        {
            return Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        }

        /// <summary>
        /// Показать сообщение об ошибке
        /// </summary>
        public static MessageBoxResult ShowError(string message, string title = "Ошибка")
        {
            return Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// Показать предупреждение
        /// </summary>
        public static MessageBoxResult ShowWarning(string message, string title = "Предупреждение")
        {
            return Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>
        /// Основной метод отображения MessageBox
        /// </summary>
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

        /// <summary>
        /// Установка иконки
        /// </summary>
        private void SetIcon(MessageBoxImage icon)
        {
            string imagePath = Path.Combine("Resources", "Images");

            switch (icon)
            {
                case MessageBoxImage.Information:
                    imagePath += "ic_info.png";
                    break;
                case MessageBoxImage.Question:
                    imagePath = "ic_question.png";
                    break;
                case MessageBoxImage.Error:
                    imagePath = "ic_error.png";
                    break;
                case MessageBoxImage.Warning:
                    imagePath = "ic_warning.png";
                    break;
            }

            try
            {
                var uri = new Uri($"pack://application:,,,/{imagePath}");
                var bitmap = new BitmapImage(uri);
                IconImage.Source = bitmap;
                IconImage.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                // Если изображение не найдено, показываем текстовую иконку как запасной вариант
                ShowFallbackIcon(icon);
            }
        }

        /// <summary>
        /// Запасной вариант с текстовыми иконками
        /// </summary>
        private void ShowFallbackIcon(MessageBoxImage icon)
        {
            // Создаем TextBlock вместо Image
            var fallbackText = new TextBlock
            {
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x00)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            switch (icon)
            {
                case MessageBoxImage.Information:
                    fallbackText.Text = "ⓘ";
                    break;
                case MessageBoxImage.Question:
                    fallbackText.Text = "?";
                    break;
                case MessageBoxImage.Error:
                    fallbackText.Text = "✕";
                    break;
                case MessageBoxImage.Warning:
                    fallbackText.Text = "⚠";
                    break;
            }

            // Заменяем Image на TextBlock в контейнере
            var parent = IconImage.Parent as Border;
            if (parent != null)
            {
                parent.Child = fallbackText;
            }
        }


        /// <summary>
        /// Установка текста сообщения
        /// </summary>
        private void SetMessage(string message)
        {
            MessageText.Text = message;
        }

        /// <summary>
        /// Установка кнопок
        /// </summary>
        private void SetButtons(MessageBoxButton buttons)
        {
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

        /// <summary>
        /// Создание кнопки
        /// </summary>
        private void CreateButton(string name, string content, MessageBoxResult result, bool isPrimary)
        {
            var button = new Button();
            button.Content = content;
            button.Margin = new Thickness(10, 0, 10, 0);
            button.Width = 150;
            button.Height = 50;
            button.FontWeight = FontWeights.Bold;
            button.FontSize = 14;
            button.Cursor = Cursors.Hand;
            button.BorderThickness = new Thickness(0);
            button.RenderTransformOrigin = new Point(0.5, 0.5);

            // Применяем градиент в зависимости от типа кнопки
            if (isPrimary)
            {
                button.Background = new LinearGradientBrush(
                    Color.FromRgb(0xFF, 0x00, 0x00),
                    Color.FromRgb(0x8A, 0x2B, 0xE2),
                    new Point(0, 0),
                    new Point(1, 0));
            }
            else
            {
                button.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
            }

            button.Foreground = new SolidColorBrush(Colors.White);

            // Стиль кнопки
            var style = new Style(typeof(Button));

            // Шаблон кнопки
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "border";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExpression(Control.BackgroundProperty));

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            border.AppendChild(contentPresenter);
            template.VisualTree = border;

            // Триггеры
            var hoverTrigger = new Trigger
            {
                Property = UIElement.IsMouseOverProperty,
                Value = true
            };

            if (isPrimary)
            {
                hoverTrigger.Setters.Add(new Setter(
                    Control.BackgroundProperty,
                    new LinearGradientBrush(
                        Color.FromRgb(0xFF, 0x33, 0x33),
                        Color.FromRgb(0x9B, 0x4D, 0xFF),
                        new Point(0, 0),
                        new Point(1, 0))));

                hoverTrigger.Setters.Add(new Setter(
                    UIElement.EffectProperty,
                    new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Color.FromRgb(0xFF, 0x00, 0x00),
                        BlurRadius = 20,
                        Opacity = 0.5
                    }));
            }
            else
            {
                hoverTrigger.Setters.Add(new Setter(
                    Control.BackgroundProperty,
                    new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A))));
            }

            template.Triggers.Add(hoverTrigger);
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            button.Style = style;

            // Добавляем анимацию масштабирования
            button.MouseEnter += (s, e) =>
            {
                var scaleTransform = new ScaleTransform(1, 1);
                button.RenderTransform = scaleTransform;

                var animation = new DoubleAnimation(1.02, TimeSpan.FromSeconds(0.2));
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
            };

            button.MouseLeave += (s, e) =>
            {
                var scaleTransform = button.RenderTransform as ScaleTransform;
                if (scaleTransform != null)
                {
                    var animation = new DoubleAnimation(1, TimeSpan.FromSeconds(0.2));
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
                }
            };

            button.Click += (s, e) =>
            {
                _result = result;
                PlayCloseAnimation();
            };

            ButtonPanel.Children.Add(button);
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
            this.RenderTransform = transform;
            this.RenderTransformOrigin = new Point(0.5, 0.5);

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
            _result = MessageBoxResult.None;
            PlayCloseAnimation();
        }
    }
}