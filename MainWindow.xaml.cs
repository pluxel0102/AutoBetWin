using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.IO;
using AutoBet.Services;

namespace AutoBet;

public sealed partial class MainWindow : Window
{
    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop), 
        "AutoBet_Log.txt");

    private static void Log(string message)
    {
        try
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            File.AppendAllText(LogFile, logMessage + Environment.NewLine);
            Console.WriteLine(logMessage);
            System.Diagnostics.Debug.WriteLine(logMessage);
        }
        catch { }
    }
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    
    private const int SW_RESTORE = 9;

    private Storyboard? _settingsPanelOpenStoryboard;
    private Storyboard? _settingsPanelCloseStoryboard;
    private Storyboard? _overlayFadeInStoryboard;
    private Storyboard? _overlayFadeOutStoryboard;
    private Storyboard? _contentAppearStoryboard;
    private string _currentMode = "BetBoom";
    private bool _isLoadingSettings = false;
    private bool _isClosing = false;
    private DispatcherTimer? _proxySaveTimer;
    private DispatcherTimer? _updateCheckTimer;

    public MainWindow()
    {
        this.InitializeComponent();
        
        Log("========================================");
        Log("AutoBet запущен!");
        Log("========================================");
        Log($"Лог файл: {LogFile}");
        
        System.Diagnostics.Debug.WriteLine("[MainWindow] Конструктор начат");
        
        _isLoadingSettings = true;
        
        // Загрузка сохранённой темы
        var savedTheme = SettingsService.LoadTheme();
        System.Diagnostics.Debug.WriteLine($"[MainWindow] Загруженная тема: {savedTheme}");
        ApplyTheme(savedTheme);
        
        // Установка состояния переключателя темы в соответствии с загруженной темой
        ThemeToggle.IsOn = savedTheme == ElementTheme.Dark;
        System.Diagnostics.Debug.WriteLine($"[MainWindow] ThemeToggle.IsOn установлен в: {ThemeToggle.IsOn}");
        
        _isLoadingSettings = false;
        System.Diagnostics.Debug.WriteLine("[MainWindow] Флаг _isLoadingSettings снят");
        
        // Настройка кастомного заголовка
        SetupCustomTitleBar();
        
        // Максимизируем окно
        var presenter = this.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
        if (presenter != null)
        {
            presenter.Maximize();
        }
        
        System.Diagnostics.Debug.WriteLine("[MainWindow] Окно максимизировано");
        
        // Инициализация иконки трея
        InitializeTrayIcon();
        
        // Включаем поддержку высоких DPI и высокочастотных мониторов
        ConfigureHighDpiSupport();

        // Инициализация анимаций
        InitializeAnimations();

        // Запуск анимации появления контента после активации
        this.Activated += MainWindow_Activated;
        
        // Запуск таймера автопроверки обновлений (каждый час)
        InitializeUpdateChecker();
        
        // Запускаем анимацию с задержкой для корректной отрисовки после максимизации
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            timer.Tick += (sender, args) =>
            {
                timer.Stop();
                if (_isFirstActivation)
                {
                    _isFirstActivation = false;
                    _contentAppearStoryboard?.Begin();
                    LoadOpenRouterSettings();
                    
                    // Первая проверка обновлений через 5 секунд после запуска
                    var firstCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                    firstCheckTimer.Tick += async (s, e) =>
                    {
                        firstCheckTimer.Stop();
                        await CheckForUpdatesAsync(silent: true);
                    };
                    firstCheckTimer.Start();
                }
            };
            timer.Start();
        });
    }

    private bool _isFirstActivation = true;

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (_isFirstActivation && args.WindowActivationState != WindowActivationState.Deactivated)
        {
            _isFirstActivation = false;
            _contentAppearStoryboard?.Begin();
            
            // Загружаем настройки OpenRouter после полной инициализации UI
            LoadOpenRouterSettings();
        }
    }

    private void InitializeAnimations()
    {
        // Анимация появления контента
        _contentAppearStoryboard = new Storyboard();
        
        var fadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(400)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(fadeIn, MainContentCard);
        Storyboard.SetTargetProperty(fadeIn, "Opacity");
        
        var slideUp = new DoubleAnimation
        {
            From = 20,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(400)),
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
        };
        Storyboard.SetTarget(slideUp, MainContentCard);
        Storyboard.SetTargetProperty(slideUp, "(UIElement.RenderTransform).(TranslateTransform.Y)");
        
        _contentAppearStoryboard.Children.Add(fadeIn);
        _contentAppearStoryboard.Children.Add(slideUp);

        // Анимация открытия панели настроек
        _settingsPanelOpenStoryboard = new Storyboard();
        
        var slideInAnimation = new DoubleAnimation
        {
            From = 350,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(350)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(slideInAnimation, SettingsPanel);
        Storyboard.SetTargetProperty(slideInAnimation, "(UIElement.RenderTransform).(TranslateTransform.X)");
        _settingsPanelOpenStoryboard.Children.Add(slideInAnimation);

        // Анимация закрытия панели настроек
        _settingsPanelCloseStoryboard = new Storyboard();
        
        var slideOutAnimation = new DoubleAnimation
        {
            From = 0,
            To = 350,
            Duration = new Duration(TimeSpan.FromMilliseconds(300)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(slideOutAnimation, SettingsPanel);
        Storyboard.SetTargetProperty(slideOutAnimation, "(UIElement.RenderTransform).(TranslateTransform.X)");
        _settingsPanelCloseStoryboard.Children.Add(slideOutAnimation);
        _settingsPanelCloseStoryboard.Completed += (s, e) => SettingsOverlay.Visibility = Visibility.Collapsed;

        // Анимация появления оверлея
        _overlayFadeInStoryboard = new Storyboard();
        
        var overlayFadeIn = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(250)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(overlayFadeIn, SettingsOverlay);
        Storyboard.SetTargetProperty(overlayFadeIn, "Opacity");
        _overlayFadeInStoryboard.Children.Add(overlayFadeIn);

        // Анимация исчезновения оверлея
        _overlayFadeOutStoryboard = new Storyboard();
        
        var overlayFadeOut = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(overlayFadeOut, SettingsOverlay);
        Storyboard.SetTargetProperty(overlayFadeOut, "Opacity");
        _overlayFadeOutStoryboard.Children.Add(overlayFadeOut);
    }

    private void LoadOpenRouterSettings()
    {
        _isLoadingSettings = true;  // Устанавливаем флаг
        
        try
        {
            // Загрузка API ключа
            string apiKey = SettingsService.LoadApiKey();
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                ApiKeyTextBox.Text = apiKey;
            }

            // Загрузка модели распознавания
            string recognitionModel = SettingsService.LoadRecognitionModel();
            if (recognitionModel == "openai/gpt-5-chat" || string.IsNullOrWhiteSpace(recognitionModel))
            {
                RecognitionGPT5Radio.IsChecked = true;
            }
            else
            {
                RecognitionGeminiRadio.IsChecked = true;
            }

            // Загрузка модели анализа
            string analysisModel = SettingsService.LoadAnalysisModel();
            if (analysisModel == "deepseek/deepseek-v3.2-exp" || string.IsNullOrWhiteSpace(analysisModel))
            {
                AnalysisDeepSeekRadio.IsChecked = true;
            }
            else
            {
                AnalysisClaudeRadio.IsChecked = true;
            }
            
            // Загрузка настроек прокси
            var proxySettings = SettingsService.LoadProxySettings();
            ProxyToggle.IsOn = proxySettings.Enabled;
            ProxyHostTextBox.Text = proxySettings.Host;
            ProxyPortTextBox.Text = proxySettings.Port > 0 ? proxySettings.Port.ToString() : string.Empty;
            ProxyUsernameTextBox.Text = proxySettings.Username;
            ProxyPasswordBox.Password = proxySettings.Password;
            
            // Установка типа прокси
            ProxyTypeComboBox.SelectedIndex = proxySettings.Type == Models.ProxyType.Http ? 0 : 1;
            
            // Показываем/скрываем панель настроек прокси
            ProxySettingsPanel.Visibility = proxySettings.Enabled ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            _isLoadingSettings = false;  // Снимаем флаг
        }
    }

    private void OnBetBoomClick(object sender, RoutedEventArgs e)
    {
        if (_currentMode != "BetBoom")
        {
            _currentMode = "BetBoom";
            AnimateModeSwitch(0, "BetBoom");
            
            // Скрываем панель кнопок MelBet
            MelBetButtonsPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void OnMelBetClick(object sender, RoutedEventArgs e)
    {
        if (_currentMode != "МелБет")
        {
            _currentMode = "МелБет";
            AnimateModeSwitch(1, "МелБет");
            
            // Показываем кнопки для MelBet
            MelBetButtonsPanel.Visibility = Visibility.Visible;
        }
    }

    private void AnimateModeSwitch(int columnIndex, string modeName)
    {
        // Получаем ширину кнопки
        double buttonWidth = BetBoomButton.ActualWidth;
        double targetX = columnIndex * buttonWidth;

        // Создаём плавную анимацию "капли"
        var storyboard = new Storyboard();

        // Анимация перемещения с эффектом упругости
        var moveAnimation = new DoubleAnimation
        {
            To = targetX,
            Duration = new Duration(TimeSpan.FromMilliseconds(400)),
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.25 }
        };
        Storyboard.SetTarget(moveAnimation, IndicatorTransform);
        Storyboard.SetTargetProperty(moveAnimation, "TranslateX");

        // Анимация растяжения при движении (эффект капли) - уменьшено до 1.04
        var scaleXAnimation = new DoubleAnimationUsingKeyFrames();
        scaleXAnimation.KeyFrames.Add(new EasingDoubleKeyFrame 
        { 
            KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0)), 
            Value = 1 
        });
        scaleXAnimation.KeyFrames.Add(new EasingDoubleKeyFrame 
        { 
            KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100)), 
            Value = 1.04,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        scaleXAnimation.KeyFrames.Add(new EasingDoubleKeyFrame 
        { 
            KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(400)), 
            Value = 1,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        Storyboard.SetTarget(scaleXAnimation, IndicatorTransform);
        Storyboard.SetTargetProperty(scaleXAnimation, "ScaleX");

        // Небольшое сжатие по вертикали для эффекта капли
        var scaleYAnimation = new DoubleAnimationUsingKeyFrames();
        scaleYAnimation.KeyFrames.Add(new EasingDoubleKeyFrame 
        { 
            KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0)), 
            Value = 1 
        });
        scaleYAnimation.KeyFrames.Add(new EasingDoubleKeyFrame 
        { 
            KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100)), 
            Value = 0.97,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        scaleYAnimation.KeyFrames.Add(new EasingDoubleKeyFrame 
        { 
            KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(400)), 
            Value = 1,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        Storyboard.SetTarget(scaleYAnimation, IndicatorTransform);
        Storyboard.SetTargetProperty(scaleYAnimation, "ScaleY");

        storyboard.Children.Add(moveAnimation);
        storyboard.Children.Add(scaleXAnimation);
        storyboard.Children.Add(scaleYAnimation);
        storyboard.Begin();

        // Обновление цвета текста кнопок
        UpdateButtonColors(columnIndex);

        // Обновление текста режима с анимацией
        UpdateModeText(modeName);
    }

    private void UpdateButtonColors(int activeIndex)
    {
        if (activeIndex == 0)
        {
            // BetBoom активен - белый текст на синем фоне
            BetBoomButton.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
            MelBetButton.Foreground = (SolidColorBrush)Application.Current.Resources["TextSecondaryBrush"];
        }
        else
        {
            // MelBet активен - белый текст на синем фоне
            BetBoomButton.Foreground = (SolidColorBrush)Application.Current.Resources["TextSecondaryBrush"];
            MelBetButton.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
        }
    }

    private void AnimateTextColor(Button button, bool isActive, Storyboard storyboard)
    {
        // Метод больше не используется
    }

    private void UpdateModeText(string mode)
    {
        var fadeOut = new Storyboard();
        var fadeOutAnim = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(150))
        };
        Storyboard.SetTarget(fadeOutAnim, CurrentModeText);
        Storyboard.SetTargetProperty(fadeOutAnim, "Opacity");
        fadeOut.Children.Add(fadeOutAnim);

        fadeOut.Completed += (s, args) =>
        {
            CurrentModeText.Text = $"Выбран режим: {mode}";

            var fadeIn = new Storyboard();
            var fadeInAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(150))
            };
            Storyboard.SetTarget(fadeInAnim, CurrentModeText);
            Storyboard.SetTargetProperty(fadeInAnim, "Opacity");
            fadeIn.Children.Add(fadeInAnim);
            fadeIn.Begin();
        };

        fadeOut.Begin();
    }

    private void OnGameModeChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton radioButton && radioButton.IsChecked == true)
        {
            string mode = radioButton.Content.ToString() ?? "Unknown";

            // Анимация изменения текста
            var fadeOut = new Storyboard();
            var fadeOutAnim = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(150))
            };
            Storyboard.SetTarget(fadeOutAnim, CurrentModeText);
            Storyboard.SetTargetProperty(fadeOutAnim, "Opacity");
            fadeOut.Children.Add(fadeOutAnim);

            fadeOut.Completed += (s, args) =>
            {
                CurrentModeText.Text = $"Выбран режим: {mode}";

                var fadeIn = new Storyboard();
                var fadeInAnim = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromMilliseconds(150))
                };
                Storyboard.SetTarget(fadeInAnim, CurrentModeText);
                Storyboard.SetTargetProperty(fadeInAnim, "Opacity");
                fadeIn.Children.Add(fadeInAnim);
                fadeIn.Begin();
            };

            fadeOut.Begin();
        }
    }

    private void OnSettingsButtonClick(object sender, RoutedEventArgs e)
    {
        SettingsOverlay.Visibility = Visibility.Visible;
        _overlayFadeInStoryboard?.Begin();
        _settingsPanelOpenStoryboard?.Begin();
    }

    // ============================================
    // Система проверки обновлений
    // ============================================
    
    /// <summary>
    /// Инициализация таймера автопроверки обновлений
    /// </summary>
    private void InitializeUpdateChecker()
    {
        _updateCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromHours(1) // Проверка каждый час
        };
        _updateCheckTimer.Tick += async (s, e) => await CheckForUpdatesAsync(silent: true);
        _updateCheckTimer.Start();
        
        Log("Таймер автопроверки обновлений запущен (каждый час)");
    }
    
    /// <summary>
    /// Обработчик кнопки "Проверить обновления"
    /// </summary>
    private async void OnCheckUpdatesButtonClick(object sender, RoutedEventArgs e)
    {
        await CheckForUpdatesAsync(silent: false);
    }
    
    /// <summary>
    /// Проверка обновлений
    /// </summary>
    /// <param name="silent">Тихая проверка (не показывать диалог если обновлений нет)</param>
    private async System.Threading.Tasks.Task CheckForUpdatesAsync(bool silent)
    {
        try
        {
            Log($"Проверка обновлений... (silent={silent})");
            
            // Показываем индикатор загрузки на кнопке
            CheckUpdatesButton.IsEnabled = false;
            
            var updateInfo = await UpdateService.CheckForUpdatesAsync();
            
            CheckUpdatesButton.IsEnabled = true;
            
            if (updateInfo == null)
            {
                if (!silent)
                {
                    await ShowInfoDialog("Информация", "Пока нет релизов на GitHub. Это нормально для новых проектов.");
                }
                Log("На GitHub пока нет релизов");
                return;
            }
            
            if (updateInfo.IsUpdateAvailable)
            {
                Log($"Найдено обновление: {updateInfo.Version}");
                
                // Показываем окно с информацией об обновлении
                var updateWindow = new Views.UpdateWindow(updateInfo);
                updateWindow.Activate();
            }
            else
            {
                if (!silent)
                {
                    var currentVersion = UpdateService.GetCurrentVersion();
                    await ShowInfoDialog("Обновлений нет", $"Вы используете актуальную версию {currentVersion}");
                }
                Log("Обновлений не найдено");
            }
        }
        catch (Exception ex)
        {
            Log($"Исключение при проверке обновлений: {ex.Message}");
            if (!silent)
            {
                await ShowInfoDialog("Ошибка", $"Ошибка проверки обновлений: {ex.Message}");
            }
        }
    }

    private void OnCloseSettingsClick(object sender, RoutedEventArgs e)
    {
        _overlayFadeOutStoryboard?.Begin();
        _settingsPanelCloseStoryboard?.Begin();
    }

    private void OnSettingsOverlayTapped(object sender, TappedRoutedEventArgs e)
    {
        // Закрываем панель при клике на затемнённую область
        _overlayFadeOutStoryboard?.Begin();
        _settingsPanelCloseStoryboard?.Begin();
    }

    private void OnSettingsPanelTapped(object sender, TappedRoutedEventArgs e)
    {
        // Предотвращаем закрытие панели при клике внутри неё
        e.Handled = true;
    }

    private void OnThemeToggled(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[MainWindow] OnThemeToggled вызван, _isLoadingSettings = {_isLoadingSettings}");
        
        if (_isLoadingSettings) return;  // Не сохраняем при загрузке настроек
        
        var toggleSwitch = sender as ToggleSwitch;
        if (toggleSwitch != null)
        {
            ElementTheme theme = toggleSwitch.IsOn ? ElementTheme.Dark : ElementTheme.Light;
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Сохраняем тему: {theme}");
            
            // Сохранение выбранной темы
            SettingsService.SaveTheme(theme);
            
            // Плавный переход темы
            var fadeOut = new Storyboard();
            var fadeOutAnim = new DoubleAnimation
            {
                From = 1,
                To = 0.7,
                Duration = new Duration(TimeSpan.FromMilliseconds(150))
            };
            Storyboard.SetTarget(fadeOutAnim, this.Content as UIElement);
            Storyboard.SetTargetProperty(fadeOutAnim, "Opacity");
            fadeOut.Children.Add(fadeOutAnim);
            
            fadeOut.Completed += (s, args) =>
            {
                ApplyTheme(theme);
                
                var fadeIn = new Storyboard();
                var fadeInAnim = new DoubleAnimation
                {
                    From = 0.7,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromMilliseconds(150))
                };
                Storyboard.SetTarget(fadeInAnim, this.Content as UIElement);
                Storyboard.SetTargetProperty(fadeInAnim, "Opacity");
                fadeIn.Children.Add(fadeInAnim);
                fadeIn.Begin();
            };
            
            fadeOut.Begin();
        }
    }

    private void ApplyTheme(ElementTheme theme)
    {
        if (Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = theme;
        }
    }

    private void SetupCustomTitleBar()
    {
        // Включаем расширение контента в область заголовка
        ExtendsContentIntoTitleBar = true;
        
        // Устанавливаем кастомную область заголовка
        SetTitleBar(AppTitleBar);
    }

    private void ConfigureHighDpiSupport()
    {
        try
        {
            // WinUI 3 автоматически поддерживает Per-Monitor DPI V2 благодаря app.manifest
            // Шрифт устанавливается глобально через App.xaml
            System.Diagnostics.Debug.WriteLine("[MainWindow] High DPI поддержка активна (Per-Monitor V2)");
            System.Diagnostics.Debug.WriteLine("[MainWindow] Шрифт: Segoe UI Variable Display");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Ошибка: {ex.Message}");
        }
    }

    private void InitializeTrayIcon()
    {
        try
        {
            Log("[InitializeTrayIcon] Начало инициализации иконки трея...");
            
            // Получаем путь к директории приложения
            var appDirectory = System.AppDomain.CurrentDomain.BaseDirectory;
            var iconPath = System.IO.Path.Combine(appDirectory, "Assets", "StoreLogo.png");
            Log($"[InitializeTrayIcon] Путь к иконке: {iconPath}");
            
            if (System.IO.File.Exists(iconPath))
            {
                Log("[InitializeTrayIcon] Файл иконки найден, вызов ForceCreate...");
                TrayIcon.ForceCreate(false);
                Log("[InitializeTrayIcon] ✓ Иконка трея инициализирована успешно!");
            }
            else
            {
                Log($"[InitializeTrayIcon] ✗ Иконка не найдена: {iconPath}");
                Log("[InitializeTrayIcon] Попытка создать иконку всё равно...");
                TrayIcon.ForceCreate(false);
            }
        }
        catch (Exception ex)
        {
            Log($"[InitializeTrayIcon] ✗ ОШИБКА: {ex.Message}");
            Log($"[InitializeTrayIcon] Stack: {ex.StackTrace}");
        }
    }

    // OpenRouter API обработчики
    private void OnApiKeyChanged(object sender, TextChangedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[MainWindow] OnApiKeyChanged вызван, _isLoadingSettings = {_isLoadingSettings}");
        
        if (_isLoadingSettings) return;  // Не сохраняем при загрузке
        
        if (sender is TextBox textBox)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Сохраняем API ключ");
            SettingsService.SaveApiKey(textBox.Text);
        }
    }

    private void OnRecognitionModelChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings) return;  // Не сохраняем при загрузке
        
        if (sender is RadioButton radio && radio.IsChecked == true)
        {
            string modelId = radio.Name == "RecognitionGPT5Radio" 
                ? "openai/gpt-5-chat" 
                : "google/gemini-2.5-flash-lite-preview-09-2025";
            SettingsService.SaveRecognitionModel(modelId);
        }
    }

    private void OnAnalysisModelChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings) return;  // Не сохраняем при загрузке
        
        if (sender is RadioButton radio && radio.IsChecked == true)
        {
            string modelId = radio.Name == "AnalysisDeepSeekRadio" 
                ? "deepseek/deepseek-v3.2-exp" 
                : "anthropic/claude-opus-4.1";
            SettingsService.SaveAnalysisModel(modelId);
        }
    }
    
    // Обработчики настроек прокси
    private void OnProxyToggled(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings) return;
        
        // Показываем/скрываем панель настроек
        ProxySettingsPanel.Visibility = ProxyToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
        
        System.Diagnostics.Debug.WriteLine($"[MainWindow] Прокси переключатель: {ProxyToggle.IsOn}");
        
        // Сохраняем настройки
        SaveProxySettings();
    }
    
    private void OnProxySettingsChanged(object sender, object e)
    {
        if (_isLoadingSettings)
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] Пропускаем сохранение - идет загрузка настроек");
            return;
        }
        
        System.Diagnostics.Debug.WriteLine($"[MainWindow] OnProxySettingsChanged вызван от {sender?.GetType().Name}");
        
        // Используем debounce - сохраняем через 500мс после последнего изменения
        if (_proxySaveTimer == null)
        {
            _proxySaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _proxySaveTimer.Tick += (s, args) =>
            {
                _proxySaveTimer?.Stop();
                SaveProxySettings();
            };
        }
        
        _proxySaveTimer.Stop();
        _proxySaveTimer.Start();
    }
    
    private void SaveProxySettings()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] SaveProxySettings() вызван");
            
            var proxySettings = new Models.ProxySettings
            {
                Enabled = ProxyToggle.IsOn,
                Host = ProxyHostTextBox.Text?.Trim() ?? string.Empty,
                Username = ProxyUsernameTextBox.Text?.Trim() ?? string.Empty,
                Password = ProxyPasswordBox.Password ?? string.Empty
            };
            
            // Парсим порт
            if (int.TryParse(ProxyPortTextBox.Text, out int port))
            {
                proxySettings.Port = port;
            }
            
            // Определяем тип прокси
            if (ProxyTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string tag = selectedItem.Tag?.ToString() ?? "Http";
                proxySettings.Type = tag == "Socks5" ? Models.ProxyType.Socks5 : Models.ProxyType.Http;
            }
            
            SettingsService.SaveProxySettings(proxySettings);
            System.Diagnostics.Debug.WriteLine($"[MainWindow] ✓ Прокси настройки сохранены: {proxySettings.Host}:{proxySettings.Port}, Enabled={proxySettings.Enabled}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] ✗ Ошибка сохранения прокси: {ex.Message}");
        }
    }

    private async void OnTestProxyClick(object sender, RoutedEventArgs e)
    {
        try
        {
            // Показываем индикатор загрузки
            TestProxyButton.IsEnabled = false;
            TestProxyProgressRing.Visibility = Visibility.Visible;
            TestProxyButtonText.Text = "Тестирование...";

            // Создаем настройки прокси из UI
            var proxySettings = new Models.ProxySettings
            {
                Enabled = ProxyToggle.IsOn,
                Host = ProxyHostTextBox.Text?.Trim() ?? string.Empty
            };

            // Парсим порт
            if (int.TryParse(ProxyPortTextBox.Text?.Trim(), out int port))
            {
                proxySettings.Port = port;
            }

            // Получаем логин и пароль
            proxySettings.Username = ProxyUsernameTextBox.Text?.Trim() ?? string.Empty;
            proxySettings.Password = ProxyPasswordBox.Password ?? string.Empty;

            // Определяем тип прокси
            if (ProxyTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string tag = selectedItem.Tag?.ToString() ?? "Http";
                proxySettings.Type = tag == "Socks5" ? Models.ProxyType.Socks5 : Models.ProxyType.Http;
            }

            // Валидируем настройки
            var (isValid, errorMessage) = proxySettings.Validate();
            if (!isValid)
            {
                var errorTitleText = new TextBlock
                {
                    Text = "Ошибка настроек прокси",
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed),
                    FontSize = 20,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                };

                var errorDialog = new ContentDialog
                {
                    Title = errorTitleText,
                    Content = errorMessage,
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }

            // Сохраняем настройки перед тестированием
            SettingsService.SaveProxySettings(proxySettings);
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Настройки прокси сохранены перед тестированием: {proxySettings.Host}:{proxySettings.Port}");

            // Тестируем прокси
            var result = await Services.OpenRouterService.TestProxy(proxySettings);

            // Показываем результат
            var resultTitleText = new TextBlock
            {
                Text = result.Success ? "Прокси работает" : "Ошибка подключения",
                Foreground = new SolidColorBrush(result.Success ? 
                    Microsoft.UI.Colors.LimeGreen : 
                    Microsoft.UI.Colors.OrangeRed),
                FontSize = 20,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };

            var resultDialog = new ContentDialog
            {
                Title = resultTitleText,
                Content = result.Message,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };
            await resultDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            var exceptionTitleText = new TextBlock
            {
                Text = "Ошибка тестирования",
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed),
                FontSize = 20,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };

            var exceptionDialog = new ContentDialog
            {
                Title = exceptionTitleText,
                Content = $"Произошла ошибка при тестировании прокси:\n{ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };
            await exceptionDialog.ShowAsync();
        }
        finally
        {
            // Скрываем индикатор загрузки
            TestProxyButton.IsEnabled = true;
            TestProxyProgressRing.Visibility = Visibility.Collapsed;
            TestProxyButtonText.Text = "🔌 Протестировать прокси";
        }
    }

    private async void OnTestApiClick(object sender, RoutedEventArgs e)
    {
        // Проверка наличия API ключа - читаем напрямую из TextBox
        string apiKey = ApiKeyTextBox.Text?.Trim() ?? string.Empty;
        
        // Если в TextBox пусто, пробуем загрузить из настроек
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = SettingsService.LoadApiKey();
        }
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var dialog = new ContentDialog
            {
                Title = "Ошибка",
                Content = "Пожалуйста, вставьте API ключ OpenRouter!",
                CloseButtonText = "ОК",
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
            return;
        }

        // Показываем индикатор загрузки
        TestApiButtonText.Visibility = Visibility.Collapsed;
        TestApiProgressRing.Visibility = Visibility.Visible;
        TestApiProgressRing.IsActive = true;
        TestApiButton.IsEnabled = false;

        try
        {
            // Получаем выбранные модели напрямую из RadioButton
            string recognitionModel = RecognitionGPT5Radio.IsChecked == true 
                ? "openai/gpt-5-chat" 
                : "google/gemini-2.5-flash-lite-preview-09-2025";
            
            string analysisModel = AnalysisDeepSeekRadio.IsChecked == true 
                ? "deepseek/deepseek-v3.2-exp" 
                : "anthropic/claude-opus-4.1";
            
            // Сохраняем текущий выбор (на всякий случай)
            SettingsService.SaveRecognitionModel(recognitionModel);
            SettingsService.SaveAnalysisModel(analysisModel);

            // Тестируем API
            var result = await OpenRouterService.TestApiKey(apiKey, recognitionModel, analysisModel);

            // Скрываем индикатор загрузки
            TestApiProgressRing.IsActive = false;
            TestApiProgressRing.Visibility = Visibility.Collapsed;
            TestApiButtonText.Visibility = Visibility.Visible;
            TestApiButton.IsEnabled = true;

            // Показываем результат
            var titleText = new TextBlock
            {
                Text = result.Success ? "Успешно" : "Ошибка",
                Foreground = new SolidColorBrush(result.Success ? 
                    Microsoft.UI.Colors.LimeGreen : 
                    Microsoft.UI.Colors.OrangeRed),
                FontSize = 20,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };

            var resultDialog = new ContentDialog
            {
                Title = titleText,
                Content = result.Message,
                CloseButtonText = "ОК",
                XamlRoot = this.Content.XamlRoot
            };
            await resultDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            // Скрываем индикатор загрузки в случае ошибки
            TestApiProgressRing.IsActive = false;
            TestApiProgressRing.Visibility = Visibility.Collapsed;
            TestApiButtonText.Visibility = Visibility.Visible;
            TestApiButton.IsEnabled = true;

            var errorDialog = new ContentDialog
            {
                Title = "Ошибка",
                Content = $"Произошла ошибка при тестировании API:\n{ex.Message}",
                CloseButtonText = "ОК",
                XamlRoot = this.Content.XamlRoot
            };

            await errorDialog.ShowAsync();
        }
    }

    private async void ConfigureROIButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] Открытие окна настройки ROI");
            
            // Сразу открываем окно настройки ROI
            var roiWindow = new Views.ROIConfigWindow();
            roiWindow.Activate();
            
            System.Diagnostics.Debug.WriteLine("[MainWindow] Окно ROI открыто, начало захвата экрана");
            
            // Минимизируем оба окна для захвата скриншота
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            ShowWindow(hwnd, 6); // SW_MINIMIZE = 6
            
            var roiHwnd = WinRT.Interop.WindowNative.GetWindowHandle(roiWindow);
            ShowWindow(roiHwnd, 6); // SW_MINIMIZE = 6
            
            // Делаем задержку, чтобы окна успели минимизироваться
            await System.Threading.Tasks.Task.Delay(500);
            
            // Захватываем экран
            var screenshot = await ScreenCaptureService.CaptureFullScreen();
            
            // Восстанавливаем окно ROI
            ShowWindow(roiHwnd, SW_RESTORE);
            
            if (screenshot != null)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Скриншот захвачен, размер: {screenshot.Length} байт");
                
                // Передаём скриншот в окно настройки ROI
                roiWindow.SetScreenshot(screenshot);
                
                System.Diagnostics.Debug.WriteLine("[MainWindow] Скриншот установлен в окно ROI");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] Ошибка захвата скриншота, окно открыто без скриншота");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Ошибка при настройке ROI: {ex.Message}");
            
            // В случае ошибки всё равно пытаемся открыть окно
            try
            {
                var roiWindow = new Views.ROIConfigWindow();
                roiWindow.Activate();
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] Не удалось открыть окно ROI");
            }
        }
    }

    private void MelBetSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] Открытие окна настроек МелБет");
            
            var settingsWindow = new Views.MelBetSettingsWindow();
            settingsWindow.Activate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Ошибка при открытии настроек МелБет: {ex.Message}");
        }
    }

    private async void LaunchMelBetButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] Открытие окна статистики МелБет");
            
            // Открываем окно статистики
            var statsWindow = new Views.MelBetStatsWindow();
            statsWindow.Activate();
            
            System.Diagnostics.Debug.WriteLine("[MainWindow] Окно статистики МелБет открыто");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Ошибка при открытии окна статистики МелБет: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Stack trace: {ex.StackTrace}");
            
            // Показываем ошибку пользователю
            var dialog = new ContentDialog
            {
                Title = "Ошибка открытия окна статистики",
                Content = $"Не удалось открыть окно статистики МелБет:\n\n{ex.Message}\n\n{ex.InnerException?.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    // === ОБРАБОТЧИКИ ТРЕЯ ===
    // TODO: Реализовать позже с H.NotifyIcon

    private void ShowMainWindow()
    {
        ((FrameworkElement)this.Content).Visibility = Visibility.Visible;
        this.Activate();
    }

    private async void TrayConfigureROI_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Log("====================================");
            Log("[Tray] Начало захвата экрана для настройки ROI");
            Log("====================================");
            
            // Делаем небольшую задержку, чтобы меню трея успело закрыться
            Log("[Tray] Ожидание 300ms...");
            await System.Threading.Tasks.Task.Delay(300);
            
            // Захватываем экран
            Log("[Tray] Захват экрана...");
            var screenshot = await ScreenCaptureService.CaptureFullScreen();
            
            if (screenshot != null)
            {
                Log($"[Tray] ✓ Скриншот захвачен успешно! Размер: {screenshot.Length} байт");
                
                // Открываем окно настройки ROI и передаём ему скриншот
                Log("[Tray] Создание окна ROIConfigWindow...");
                var roiWindow = new Views.ROIConfigWindow();
                
                Log("[Tray] Установка скриншота...");
                roiWindow.SetScreenshot(screenshot);
                
                Log("[Tray] Активация окна...");
                roiWindow.Activate();
                
                Log("[Tray] ✓ Окно настройки ROI открыто со скриншотом!");
            }
            else
            {
                Log("[Tray] ✗ ОШИБКА: Скриншот = null");
                
                // Открываем окно без скриншота, пользователь сможет сделать его сам
                Log("[Tray] Открытие окна без скриншота...");
                var roiWindow = new Views.ROIConfigWindow();
                roiWindow.Activate();
                Log("[Tray] Окно открыто");
            }
        }
        catch (Exception ex)
        {
            Log($"[Tray] ✗✗✗ ИСКЛЮЧЕНИЕ ✗✗✗");
            Log($"[Tray] Сообщение: {ex.Message}");
            Log($"[Tray] Тип: {ex.GetType().Name}");
            Log($"[Tray] Stack trace:");
            Log(ex.StackTrace ?? "");
            
            // В случае ошибки всё равно открываем окно
            try
            {
                Log("[Tray] Попытка открыть окно после ошибки...");
                var roiWindow = new Views.ROIConfigWindow();
                roiWindow.Activate();
                Log("[Tray] Окно открыто");
            }
            catch (Exception ex2)
            {
                Log($"[Tray] Не удалось открыть окно: {ex2.Message}");
            }
        }
    }

    private void TrayShowWindow_Click(object sender, RoutedEventArgs e)
    {
        Log("[TrayShowWindow_Click] Событие сработало!");
        this.Activate();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        ShowWindow(hwnd, SW_RESTORE);
        Log("[TrayShowWindow_Click] Главное окно показано");
    }

    private void TraySettings_Click(object sender, RoutedEventArgs e)
    {
        Log("[TraySettings_Click] Событие сработало!");
        this.Activate();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        ShowWindow(hwnd, SW_RESTORE);
        OnSettingsButtonClick(sender, e);
        Log("[TraySettings_Click] Открыты настройки");
    }

    private void TrayExit_Click(object sender, RoutedEventArgs e)
    {
        Log("[TrayExit_Click] Событие сработало!");
        _isClosing = true;
        TrayIcon?.Dispose();
        this.Close();
        Log("[TrayExit_Click] Приложение закрыто");
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        if (!_isClosing)
        {
            _isClosing = true;
            TrayIcon?.Dispose();
        }
    }

    // Hover-эффекты для кнопок
    private void OnButtonPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.Scale = new System.Numerics.Vector3(1.05f, 1.05f, 1);
            button.Translation = new System.Numerics.Vector3(0, -2, 0);
        }
    }

    private void OnButtonPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.Scale = new System.Numerics.Vector3(1, 1, 1);
            button.Translation = new System.Numerics.Vector3(0, 0, 0);
        }
    }

    private void OnButtonPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.Scale = new System.Numerics.Vector3(0.98f, 0.98f, 1);
        }
    }

    private void OnButtonPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.Scale = new System.Numerics.Vector3(1.05f, 1.05f, 1);
        }
    }
    
    /// <summary>
    /// Показать информационный диалог
    /// </summary>
    private async System.Threading.Tasks.Task ShowInfoDialog(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.Content.XamlRoot
        };
        
        await dialog.ShowAsync();
    }
}
