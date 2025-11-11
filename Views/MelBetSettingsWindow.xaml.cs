using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using AutoBet.Services;
        
namespace AutoBet.Views
{
    public sealed partial class MelBetSettingsWindow : Window
    {
        private DispatcherTimer? _saveTimer;
        private DispatcherTimer? _notificationTimer;
        private Storyboard? _notificationFadeInStoryboard;
        private Storyboard? _notificationFadeOutStoryboard;

        public MelBetSettingsWindow()
        {
            this.InitializeComponent();
            
            // Применяем сохранённую тему
            ApplyTheme();
            
            // Настраиваем кастомный заголовок
            SetupCustomTitleBar();
            
            SetupWindow();
            SetupAnimations();
            LoadSettings();
            
            // Инициализация таймера для автосохранения
            _saveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000)
            };
            _saveTimer.Tick += SaveTimer_Tick;

            // Инициализация таймера для скрытия уведомления
            _notificationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(3000)
            };
            _notificationTimer.Tick += NotificationTimer_Tick;
        }

        private void SetupCustomTitleBar()
        {
            // Включаем кастомный заголовок
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);
            
            // Скрываем системные кнопки
            var titleBar = this.AppWindow.TitleBar;
            if (titleBar != null)
            {
                titleBar.IconShowOptions = Microsoft.UI.Windowing.IconShowOptions.HideIconAndSystemMenu;
            }
        }

        private void SetupWindow()
        {
            // Устанавливаем размер окна
            var size = new Windows.Graphics.SizeInt32(600, 500);
            this.AppWindow.Resize(size);
            
            // Настройка внешнего вида окна
            var presenter = this.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
            if (presenter != null)
            {
                presenter.IsResizable = true;
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
            }

            // Центрируем окно
            CenterWindow();
        }

        private void SetupAnimations()
        {
            // Анимация появления уведомления (fade in)
            _notificationFadeInStoryboard = new Storyboard();
            
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(fadeIn, SaveNotificationBar);
            Storyboard.SetTargetProperty(fadeIn, "Opacity");
            _notificationFadeInStoryboard.Children.Add(fadeIn);

            // Анимация исчезновения уведомления (fade out)
            _notificationFadeOutStoryboard = new Storyboard();
            
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(fadeOut, SaveNotificationBar);
            Storyboard.SetTargetProperty(fadeOut, "Opacity");
            _notificationFadeOutStoryboard.Children.Add(fadeOut);

            _notificationFadeOutStoryboard.Completed += (s, e) =>
            {
                SaveNotificationBar.IsOpen = false;
            };
        }

        private void CenterWindow()
        {
            var displayArea = Microsoft.UI.Windowing.DisplayArea.Primary;
            var workArea = displayArea.WorkArea;
            
            var windowWidth = 600;
            var windowHeight = 500;
            
            var x = (workArea.Width - windowWidth) / 2;
            var y = (workArea.Height - windowHeight) / 2;
            
            this.AppWindow.Move(new Windows.Graphics.PointInt32(x, y));
        }

        private void LoadSettings()
        {
            try
            {
                // Загружаем сохранённую начальную ставку
                int savedBaseBet = SettingsService.LoadMelBetBaseBet();
                
                // Находим и выбираем соответствующий элемент в ComboBox
                foreach (ComboBoxItem item in BaseBetComboBox.Items)
                {
                    if (item.Tag is string tag && int.Parse(tag) == savedBaseBet)
                    {
                        BaseBetComboBox.SelectedItem = item;
                        break;
                    }
                }
                
                // Если не нашли, выбираем первый (10)
                if (BaseBetComboBox.SelectedItem == null && BaseBetComboBox.Items.Count > 0)
                {
                    BaseBetComboBox.SelectedIndex = 0;
                }
                
                System.Diagnostics.Debug.WriteLine($"[MelBetSettingsWindow] Загружена начальная ставка: {savedBaseBet}");

                // Загружаем сохранённый предпочитаемый цвет
                string savedColor = SettingsService.LoadMelBetPreferredColor();
                
                // Находим и выбираем соответствующий элемент в ComboBox цветов
                foreach (ComboBoxItem item in PreferredColorComboBox.Items)
                {
                    if (item.Tag is string tag && tag == savedColor)
                    {
                        PreferredColorComboBox.SelectedItem = item;
                        break;
                    }
                }
                
                // Если не нашли, выбираем первый (Синий)
                if (PreferredColorComboBox.SelectedItem == null && PreferredColorComboBox.Items.Count > 0)
                {
                    PreferredColorComboBox.SelectedIndex = 0;
                }
                
                System.Diagnostics.Debug.WriteLine($"[MelBetSettingsWindow] Загружен предпочитаемый цвет: {savedColor}");

                // Загружаем сохранённую настройку смены цвета
                int savedColorSwitch = SettingsService.LoadMelBetColorSwitchAfterLosses();
                
                // Находим и выбираем соответствующий элемент в ComboBox смены цвета
                foreach (ComboBoxItem item in ColorSwitchComboBox.Items)
                {
                    if (item.Tag is string tag && int.TryParse(tag, out int value) && value == savedColorSwitch)
                    {
                        ColorSwitchComboBox.SelectedItem = item;
                        break;
                    }
                }
                
                // Если не нашли, выбираем второй (Через 2 проигрыша)
                if (ColorSwitchComboBox.SelectedItem == null && ColorSwitchComboBox.Items.Count > 1)
                {
                    ColorSwitchComboBox.SelectedIndex = 1;
                }
                
                System.Diagnostics.Debug.WriteLine($"[MelBetSettingsWindow] Загружена настройка смены цвета: {savedColorSwitch}");

                // Загружаем сохранённую стратегию
                var savedStrategy = SettingsService.LoadMelBetStrategy();
                
                // Находим и выбираем соответствующий элемент в ComboBox стратегии
                foreach (ComboBoxItem item in StrategyComboBox.Items)
                {
                    if (item.Tag is string tag && tag == savedStrategy.ToString())
                    {
                        StrategyComboBox.SelectedItem = item;
                        break;
                    }
                }
                
                // Если не нашли, выбираем первый (Мартингейл)
                if (StrategyComboBox.SelectedItem == null && StrategyComboBox.Items.Count > 0)
                {
                    StrategyComboBox.SelectedIndex = 0;
                }
                
                System.Diagnostics.Debug.WriteLine($"[MelBetSettingsWindow] Загружена стратегия: {savedStrategy}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MelBetSettingsWindow] Ошибка загрузки настроек: {ex.Message}");
            }
        }

        private void BaseBetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Перезапускаем таймер при каждом изменении
            _saveTimer?.Stop();
            _saveTimer?.Start();
        }

        private void PreferredColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Перезапускаем таймер при каждом изменении
            _saveTimer?.Stop();
            _saveTimer?.Start();
        }

        private void ColorSwitchComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Перезапускаем таймер при каждом изменении
            _saveTimer?.Stop();
            _saveTimer?.Start();
        }

        private void StrategyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Перезапускаем таймер при каждом изменении
            _saveTimer?.Stop();
            _saveTimer?.Start();
        }

        private void SaveTimer_Tick(object? sender, object e)
        {
            _saveTimer?.Stop();
            
            try
            {
                // Сохраняем начальную ставку
                if (BaseBetComboBox.SelectedItem is ComboBoxItem selectedBetItem && 
                    selectedBetItem.Tag is string betTag)
                {
                    int baseBet = int.Parse(betTag);
                    SettingsService.SaveMelBetBaseBet(baseBet);
                    System.Diagnostics.Debug.WriteLine($"[MelBetSettingsWindow] Начальная ставка сохранена: {baseBet}");
                }

                // Сохраняем предпочитаемый цвет
                if (PreferredColorComboBox.SelectedItem is ComboBoxItem selectedColorItem && 
                    selectedColorItem.Tag is string colorTag)
                {
                    SettingsService.SaveMelBetPreferredColor(colorTag);
                    System.Diagnostics.Debug.WriteLine($"[MelBetSettingsWindow] Предпочитаемый цвет сохранён: {colorTag}");
                }

                // Сохраняем настройку смены цвета
                if (ColorSwitchComboBox.SelectedItem is ComboBoxItem selectedSwitchItem && 
                    selectedSwitchItem.Tag is string switchTag && 
                    int.TryParse(switchTag, out int lossesCount))
                {
                    SettingsService.SaveMelBetColorSwitchAfterLosses(lossesCount);
                    System.Diagnostics.Debug.WriteLine($"[MelBetSettingsWindow] Настройка смены цвета сохранена: {lossesCount}");
                }

                // Сохраняем стратегию
                if (StrategyComboBox.SelectedItem is ComboBoxItem selectedStrategyItem && 
                    selectedStrategyItem.Tag is string strategyTag &&
                    Enum.TryParse<AutoBet.Models.BetStrategy>(strategyTag, out var strategy))
                {
                    SettingsService.SaveMelBetStrategy(strategy);
                    System.Diagnostics.Debug.WriteLine($"[MelBetSettingsWindow] Стратегия сохранена: {strategy}");
                }

                // Показываем уведомление о сохранении
                ShowSaveNotification();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MelBetSettingsWindow] Ошибка сохранения: {ex.Message}");
            }
        }

        private void ShowSaveNotification()
        {
            // Показываем уведомление с анимацией
            SaveNotificationBar.IsOpen = true;
            _notificationFadeInStoryboard?.Begin();
            
            // Перезапускаем таймер для автоматического скрытия через 3 секунды
            _notificationTimer?.Stop();
            _notificationTimer?.Start();
        }

        private void NotificationTimer_Tick(object? sender, object e)
        {
            _notificationTimer?.Stop();
            
            // Скрываем уведомление с анимацией
            _notificationFadeOutStoryboard?.Begin();
        }



        private void ApplyTheme()
        {
            var savedTheme = SettingsService.LoadTheme();
            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = savedTheme;
            }
        }
    }
}
