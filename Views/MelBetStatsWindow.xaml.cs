using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;
using AutoBet.Controllers;
using AutoBet.Models;

namespace AutoBet.Views;

public sealed partial class MelBetStatsWindow : Window
{
    private MelBetController? _controller;
    private DispatcherTimer? _updateTimer;
    private bool _isRunning = false;
    
    // Словарь для хранения UI элементов статистики кубиков
    private Dictionary<string, Dictionary<string, Dictionary<int, (TextBlock Count, ProgressBar Progress)>>> _diceStatsUI;
    
    // Лог сообщений
    private StringBuilder _logBuilder = new StringBuilder();
    private const int MaxLogLines = 100;
    
    // Логирование в файл
    private string? _currentLogFilePath;
    private readonly string _logsDirectory;

    public MelBetStatsWindow()
    {
        // Инициализируем папку для логов
        _logsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AutoBet",
            "Logs"
        );
        Directory.CreateDirectory(_logsDirectory);
        InitializeComponent();
        
        // Применяем сохранённую тему
        ApplyTheme();
        
        _diceStatsUI = new Dictionary<string, Dictionary<string, Dictionary<int, (TextBlock, ProgressBar)>>>();
        
        // Настраиваем окно
        SetupCustomTitleBar();
        SetupWindow();
        
        // Создаём карточки статистики
        CreateDiceStatisticsCards();
        
        // Таймер обновления UI
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();
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
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

        if (appWindow != null)
        {
            int width = 450;
            int height = 750;
            
            var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
            int x = displayArea.WorkArea.Width - width - 20;
            int y = 20;
            
            appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, width, height));
            
            // Устанавливаем окно всегда поверх всех окон
            var presenter = appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
            if (presenter != null)
            {
                presenter.IsAlwaysOnTop = true;
            }
        }
    }

    private void CreateDiceStatisticsCards()
    {
        var periods = new (string Name, int? Count)[]
        {
            ("Все броски", null),
            ("Последние 50", 50),
            ("Последние 25", 25),
            ("Последние 20", 20),
            ("Последние 15", 15),
            ("Последние 10", 10)
        };

        foreach (var (name, count) in periods)
        {
            var card = CreatePeriodCard(name, count);
            StatsContainer.Children.Add(card);
        }
    }

    private Border CreatePeriodCard(string periodName, int? periodCount)
    {
        // Современная карточка с градиентом
        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(255, 30, 30, 30)),
            CornerRadius = new CornerRadius(12),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16),
            Margin = new Thickness(0)
        };

        var stackPanel = new StackPanel { Spacing = 12 };
        card.Child = stackPanel;

        // Заголовок периода
        var title = new TextBlock
        {
            Text = periodName,
            FontSize = 15,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 200, 200)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        };
        stackPanel.Children.Add(title);

        // Контейнер для кубиков в строке
        var diceGrid = new Grid { ColumnSpacing = 12 };
        diceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        diceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        stackPanel.Children.Add(diceGrid);

        // Синий кубик слева
        var blueFrame = CreateDiceFrame("🔵", Color.FromArgb(255, 66, 165, 245), "blue");
        Grid.SetColumn(blueFrame, 0);
        diceGrid.Children.Add(blueFrame);

        // Красный кубик справа
        var redFrame = CreateDiceFrame("🔴", Color.FromArgb(255, 239, 83, 80), "red");
        Grid.SetColumn(redFrame, 1);
        diceGrid.Children.Add(redFrame);

        // Сохраняем ссылки на UI элементы
        string periodKey = periodCount?.ToString() ?? "all";
        _diceStatsUI[periodKey] = new Dictionary<string, Dictionary<int, (TextBlock, ProgressBar)>>
        {
            ["red"] = redFrame.Tag as Dictionary<int, (TextBlock, ProgressBar)>,
            ["blue"] = blueFrame.Tag as Dictionary<int, (TextBlock, ProgressBar)>
        };

        // Добавляем строку с рейтингом чисел (1-6)
        var sumRankingText = new TextBlock
        {
            Name = $"SumRanking_{periodKey}",
            Text = "🏆 Рейтинг чисел: Загрузка...",
            FontSize = 11,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            Foreground = new SolidColorBrush(Color.FromArgb(255, 180, 180, 180)),
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };
        stackPanel.Children.Add(sumRankingText);

        return card;
    }

    private Border CreateDiceFrame(string emoji, Color accentColor, string diceColor)
    {
        var frame = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(255, 40, 40, 40)),
            CornerRadius = new CornerRadius(8),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, accentColor.R, accentColor.G, accentColor.B)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10)
        };

        var stackPanel = new StackPanel { Spacing = 8 };
        frame.Child = stackPanel;

        // Эмодзи кубика (меньший размер)
        var emojiText = new TextBlock
        {
            Text = emoji,
            FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        };
        stackPanel.Children.Add(emojiText);

        // Сетка значений 1-6 (2 ряда по 3)
        var valuesGrid = new Grid { RowSpacing = 6, ColumnSpacing = 4 };
        for (int i = 0; i < 2; i++)
            valuesGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int i = 0; i < 3; i++)
            valuesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        
        stackPanel.Children.Add(valuesGrid);

        // Создаём виджеты для значений 1-6
        var valueWidgets = new Dictionary<int, (TextBlock, ProgressBar)>();
        for (int value = 1; value <= 6; value++)
        {
            int row = (value - 1) / 3;
            int col = (value - 1) % 3;

            var widget = CreateValueWidget(value, accentColor);
            Grid.SetRow(widget, row);
            Grid.SetColumn(widget, col);
            valuesGrid.Children.Add(widget);

            // Сохраняем ссылки
            var countLabel = ((widget.Child as StackPanel).Children[0] as Grid).Children[1] as TextBlock;
            var progressBar = (widget.Child as StackPanel).Children[1] as ProgressBar;
            valueWidgets[value] = (countLabel, progressBar);
        }

        // Сохраняем словарь виджетов в Tag
        frame.Tag = valueWidgets;

        return frame;
    }

    private Border CreateValueWidget(int value, Color color)
    {
        var container = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(255, 35, 35, 35)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(6, 5, 6, 5),
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B)),
            BorderThickness = new Thickness(1)
        };

        var stackPanel = new StackPanel { Spacing = 3 };
        container.Child = stackPanel;

        // Верхняя панель с номером и счётчиком
        var infoGrid = new Grid();
        infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        stackPanel.Children.Add(infoGrid);

        var valueLabel = new TextBlock
        {
            Text = value.ToString(),
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(color)
        };
        Grid.SetColumn(valueLabel, 0);
        infoGrid.Children.Add(valueLabel);

        var countLabel = new TextBlock
        {
            Text = "0",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 180, 180, 180)),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(countLabel, 1);
        infoGrid.Children.Add(countLabel);

        // Плавный прогресс-бар
        var progress = new ProgressBar
        {
            Height = 4,
            Foreground = new SolidColorBrush(color),
            Background = new SolidColorBrush(Color.FromArgb(255, 50, 50, 50)),
            CornerRadius = new CornerRadius(2),
            Value = 0,
            Maximum = 1.0  // Максимум 1.0 для дробных значений
        };
        stackPanel.Children.Add(progress);

        return container;
    }

    private void UpdateTimer_Tick(object? sender, object e)
    {
        if (_controller == null)
            return;

        UpdateStatistics();
    }

    private void UpdateStatistics()
    {
        if (_controller == null)
            return;

        var state = _controller.GameState;

        // Обновляем статус и анимацию иконки
        if (_isRunning)
        {
            StatusText.Text = "Работает";
            StatusIcon.Fill = new SolidColorBrush(Color.FromArgb(255, 76, 175, 80)); // Зелёный
            
            // Запускаем анимацию пульсации
            PulseAnimation.Begin();
        }
        else
        {
            StatusText.Text = "Готов к запуску";
            StatusIcon.Fill = new SolidColorBrush(Color.FromArgb(255, 153, 153, 153)); // Серый
            StatusIcon.Opacity = 0.3;
            
            // Останавливаем анимацию
            PulseAnimation.Stop();
        }

        // Обновляем статистику кубиков для всех периодов
        if (state.RollsHistory.Count > 0)
        {
            var periods = new (string Key, int Count)[]
            {
                ("all", state.RollsHistory.Count),
                ("50", Math.Min(50, state.RollsHistory.Count)),
                ("25", Math.Min(25, state.RollsHistory.Count)),
                ("20", Math.Min(20, state.RollsHistory.Count)),
                ("15", Math.Min(15, state.RollsHistory.Count)),
                ("10", Math.Min(10, state.RollsHistory.Count))
            };

            foreach (var (key, count) in periods)
            {
                if (!_diceStatsUI.ContainsKey(key))
                    continue;

                // Берём последние N бросков
                var rolls = state.RollsHistory.Skip(Math.Max(0, state.RollsHistory.Count - count)).ToList();

                // Подсчитываем статистику по отдельным кубикам
                var redStats = new int[7];
                var blueStats = new int[7];
                
                // Подсчитываем общую статистику по всем числам (1-6)
                var allNumbersStats = new Dictionary<int, int>();
                for (int i = 1; i <= 6; i++)
                    allNumbersStats[i] = 0;
                
                foreach (var (blue, red) in rolls)
                {
                    if (blue >= 1 && blue <= 6)
                    {
                        blueStats[blue]++;
                        allNumbersStats[blue]++;
                    }
                    if (red >= 1 && red <= 6)
                    {
                        redStats[red]++;
                        allNumbersStats[red]++;
                    }
                }

                // Находим максимумы для нормализации прогресс-баров
                int maxRed = redStats.Max();
                int maxBlue = blueStats.Max();

                // Обновляем UI красного кубика
                var redUI = _diceStatsUI[key]["red"];
                for (int i = 1; i <= 6; i++)
                {
                    var (countLabel, progressBar) = redUI[i];
                    int oldValue = int.TryParse(countLabel.Text, out int val) ? val : 0;
                    int newValue = redStats[i];
                    
                    countLabel.Text = newValue.ToString();
                    
                    // Подсветка при изменении
                    if (newValue > oldValue)
                    {
                        var border = FindParentBorder(countLabel);
                        if (border != null)
                        {
                            HighlightElement(border);
                        }
                    }
                    
                    // Плавная анимация прогресс-бара (значение от 0.0 до 1.0)
                    double targetValue = maxRed > 0 ? (double)redStats[i] / maxRed : 0;
                    AnimateProgressBar(progressBar, targetValue);
                }

                // Обновляем UI синего кубика
                var blueUI = _diceStatsUI[key]["blue"];
                for (int i = 1; i <= 6; i++)
                {
                    var (countLabel, progressBar) = blueUI[i];
                    int oldValue = int.TryParse(countLabel.Text, out int val) ? val : 0;
                    int newValue = blueStats[i];
                    
                    countLabel.Text = newValue.ToString();
                    
                    // Подсветка при изменении
                    if (newValue > oldValue)
                    {
                        var border = FindParentBorder(countLabel);
                        if (border != null)
                        {
                            HighlightElement(border);
                        }
                    }
                    
                    // Плавная анимация прогресс-бара (значение от 0.0 до 1.0)
                    double targetValue = maxBlue > 0 ? (double)blueStats[i] / maxBlue : 0;
                    AnimateProgressBar(progressBar, targetValue);
                }
                
                // Обновляем строку с рейтингом всех чисел (от 1 до 6)
                UpdateNumberRanking(key, allNumbersStats);
            }
        }
    }
    
    private void UpdateNumberRanking(string periodKey, Dictionary<int, int> numberStats)
    {
        try
        {
            // Находим TextBlock с рейтингом чисел
            var sumRankingName = $"SumRanking_{periodKey}";
            TextBlock? sumRankingText = null;
            
            // Ищем в StatsContainer
            foreach (var child in StatsContainer.Children)
            {
                if (child is Border card && card.Child is StackPanel panel)
                {
                    foreach (var element in panel.Children)
                    {
                        if (element is TextBlock tb && tb.Name == sumRankingName)
                        {
                            sumRankingText = tb;
                            break;
                        }
                    }
                }
                if (sumRankingText != null) break;
            }
            
            if (sumRankingText == null)
                return;
            
            // Сортируем числа по частоте (от большего к меньшему)
            var sortedNumbers = numberStats
                .OrderByDescending(kvp => kvp.Value)
                .ThenBy(kvp => kvp.Key)
                .Where(kvp => kvp.Value > 0)
                .Select((kvp, index) => 
                {
                    // Добавляем медали и позиции для топ-3
                    string prefix = index switch
                    {
                        0 => "🥇1-е:", // 1-е место - золото
                        1 => "🥈2-е:", // 2-е место - серебро
                        2 => "🥉3-е:", // 3-е место - бронза
                        _ => $"{index + 1}-е:"    // Остальные с номером
                    };
                    
                    // Правильное склонение слова "раз"
                    string times = GetRazDeclension(kvp.Value);
                    
                    return $"{prefix} число {kvp.Key} ({kvp.Value} {times})";
                })
                .ToList();
            
            if (sortedNumbers.Count > 0)
            {
                string periodName = periodKey == "all" ? "Все ходы" : $"{periodKey} ходов";
                sumRankingText.Text = $"🏆 Рейтинг [{periodName}]:\n{string.Join(" | ", sortedNumbers)}";
            }
            else
            {
                sumRankingText.Text = "🏆 Рейтинг: Нет данных";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateNumberRanking] Ошибка: {ex.Message}");
        }
    }
    
    private string GetRazDeclension(int count)
    {
        // Правильное склонение слова "раз"
        // 1 раз, 2 раза, 3 раза, 4 раза, 5 раз, 6 раз...
        // 21 раз, 22 раза, 23 раза, 24 раза, 25 раз...
        
        int lastDigit = count % 10;
        int lastTwoDigits = count % 100;
        
        // Исключения для 11-14
        if (lastTwoDigits >= 11 && lastTwoDigits <= 14)
            return "раз";
        
        // Основные правила
        if (lastDigit == 1)
            return "раз";
        else if (lastDigit >= 2 && lastDigit <= 4)
            return "раза";
        else
            return "раз";
    }
    
    private Border? FindParentBorder(DependencyObject child)
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is Border border)
                return border;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    private void AnimateProgressBar(ProgressBar progressBar, double targetValue)
    {
        // Напрямую устанавливаем значение без анимации для надёжности
        // (анимация в WinUI 3 иногда не работает с ProgressBar)
        try
        {
            System.Diagnostics.Debug.WriteLine($"[ProgressBar] Setting value to {targetValue}, Current: {progressBar.Value}, Max: {progressBar.Maximum}");
            progressBar.Value = targetValue;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProgressBar] Error: {ex.Message}");
        }
    }

    private void HighlightElement(Border border)
    {
        // Сохраняем оригинальный фон
        var originalBrush = border.Background as SolidColorBrush;
        var originalColor = originalBrush?.Color ?? Colors.Transparent;
        
        // Создаем анимацию подсветки (от яркого к оригинальному)
        var highlightColor = Color.FromArgb(60, 255, 255, 0); // Полупрозрачный желтый
        
        var colorAnimation = new ColorAnimation
        {
            From = highlightColor,
            To = originalColor,
            Duration = new Duration(TimeSpan.FromMilliseconds(600)),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        
        var brush = new SolidColorBrush(highlightColor);
        border.Background = brush;
        
        Storyboard.SetTarget(colorAnimation, brush);
        Storyboard.SetTargetProperty(colorAnimation, "Color");
        
        var storyboard = new Storyboard();
        storyboard.Children.Add(colorAnimation);
        storyboard.Begin();
    }

    private void StartStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isRunning)
        {
            StartController();
        }
        else
        {
            StopController();
        }
    }

    private void StartController()
    {
        try
        {
            // Создаём новый файл логов для сессии
            CreateNewLogFile();
            
            if (_controller == null)
            {
                _controller = new MelBetController();
                _controller.OnStateChanged += OnControllerStateChanged;
                _controller.OnLogMessage += OnControllerLogMessage;
                _controller.OnError += OnControllerError;
                _controller.OnGameStopped += OnGameStopped;
            }

            _controller.StartGame();

            _isRunning = true;

            // Обновляем кнопку - красная "Остановить"
            StartStopText.Text = "Остановить";
            StartStopButton.Background = new SolidColorBrush(Color.FromArgb(255, 239, 83, 80)); // Красный
            
            // Обновляем ресурсы для hover/pressed
            StartStopButton.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(Color.FromArgb(255, 244, 100, 96));
            StartStopButton.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(Color.FromArgb(255, 220, 60, 60));
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Ошибка запуска", ex.Message);
        }
    }

    private void StopController()
    {
        if (_controller != null)
        {
            _controller.StopGame();
        }

        _isRunning = false;

        // Обновляем кнопку - зелёная "Начать"
        StartStopText.Text = "Начать";
        StartStopButton.Background = new SolidColorBrush(Color.FromArgb(255, 76, 175, 80)); // Зелёный
        
        // Обновляем ресурсы для hover/pressed
        StartStopButton.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(Color.FromArgb(255, 92, 191, 96));
        StartStopButton.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(Color.FromArgb(255, 68, 157, 72));
    }

    private void OnControllerStateChanged(MelBetGameState state)
    {
        // Обновление происходит через таймер
        DispatcherQueue.TryEnqueue(() =>
        {
            AddLog($"📊 Состояние обновлено: Раунд {state.TotalRounds}, Баланс {state.Balance}, Ставка {state.CurrentBet}");
            
            // Если есть новый результат
            if (state.LastBlueValue > 0 && state.LastRedValue > 0)
            {
                AddLog($"🎲 Результат: Синий={state.LastBlueValue}, Красный={state.LastRedValue}");
            }
        });
    }

    private void OnControllerLogMessage(string message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            AddLog($"ℹ️ {message}");
        });
    }

    private void OnControllerError(string error, Exception? exception)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            string fullMessage = exception != null ? $"{error}: {exception.Message}" : error;
            AddLog($"❌ ОШИБКА: {fullMessage}");
            ShowErrorDialog("Ошибка контроллера", fullMessage);
        });
    }

    private void OnGameStopped(string reason)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _isRunning = false;
            
            AddLog($"⏹️ Игра остановлена: {reason}");
            
            // Возвращаем кнопку в исходное состояние
            StartStopText.Text = "Начать";
            StartStopButton.Background = new SolidColorBrush(Color.FromArgb(255, 76, 175, 80));
            StartStopButton.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(Color.FromArgb(255, 92, 191, 96));
            StartStopButton.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(Color.FromArgb(255, 68, 157, 72));

            if (!string.IsNullOrEmpty(reason))
            {
                ShowErrorDialog("Игра остановлена", reason);
            }
        });
    }

    private void AddLog(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string logLine = $"[{timestamp}] {message}";
        
        // Добавляем новую строку
        _logBuilder.AppendLine(logLine);
        
        // Записываем в файл
        WriteToLogFile(logLine);
        
        // Ограничиваем количество строк в UI
        var lines = _logBuilder.ToString().Split('\n');
        if (lines.Length > MaxLogLines)
        {
            _logBuilder.Clear();
            for (int i = lines.Length - MaxLogLines; i < lines.Length; i++)
            {
                _logBuilder.AppendLine(lines[i]);
            }
        }
        
        // Обновляем UI
        LogTextBox.Text = _logBuilder.ToString();
        
        // Плавная прокрутка вниз
        AnimateScrollToBottom();
    }
    
    private void CreateNewLogFile()
    {
        try
        {
            // Формат: MelBet_Session_2025-11-10_14-30-45.txt
            string fileName = $"MelBet_Session_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
            _currentLogFilePath = Path.Combine(_logsDirectory, fileName);
            
            // Создаём файл с заголовком
            string header = $"=== MelBet Session Log ===\n" +
                          $"Дата начала: {DateTime.Now:dd.MM.yyyy HH:mm:ss}\n" +
                          $"{'=',-40}\n\n";
            File.WriteAllText(_currentLogFilePath, header);
            
            AddLog($"📁 Лог-файл создан: {fileName}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MelBetStatsWindow] Ошибка создания лог-файла: {ex.Message}");
        }
    }
    
    private void WriteToLogFile(string logLine)
    {
        try
        {
            if (!string.IsNullOrEmpty(_currentLogFilePath))
            {
                File.AppendAllText(_currentLogFilePath, logLine + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MelBetStatsWindow] Ошибка записи в лог-файл: {ex.Message}");
        }
    }
    
    private void AnimateScrollToBottom()
    {
        // Даём UI время обновиться, затем плавно прокручиваем
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            // ChangeView с disableAnimation=false для плавной прокрутки
            LogScrollViewer.ChangeView(null, LogScrollViewer.ScrollableHeight, null, false);
        });
    }

    private async void ShowErrorDialog(string title, string message)
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

    private async void CopyLogsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var logText = LogTextBox.Text;
            
            if (string.IsNullOrWhiteSpace(logText))
            {
                // Показываем уведомление, что логи пусты
                var emptyDialog = new ContentDialog
                {
                    Title = "Нет логов",
                    Content = "Логи пока пусты. Запустите игру, чтобы увидеть логи.",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await emptyDialog.ShowAsync();
                return;
            }
            
            // Копируем в буфер обмена
            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(logText);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            
            // Визуальная обратная связь - меняем иконку на галочку
            var button = sender as Button;
            if (button?.Content is FontIcon icon)
            {
                var originalGlyph = icon.Glyph;
                icon.Glyph = "\uE73E"; // Галочка
                icon.Foreground = new SolidColorBrush(Color.FromArgb(255, 76, 175, 80)); // Зелёный
                
                // Возвращаем обратно через 1.5 секунды
                await System.Threading.Tasks.Task.Delay(1500);
                icon.Glyph = originalGlyph;
                icon.Foreground = new SolidColorBrush(Microsoft.UI.Colors.DeepSkyBlue);
            }
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Ошибка копирования", $"Не удалось скопировать логи: {ex.Message}");
        }
    }

    private void OpenLogsFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Открываем папку с логами в проводнике
            if (Directory.Exists(_logsDirectory))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _logsDirectory,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            else
            {
                ShowErrorDialog("Папка не найдена", "Папка с логами ещё не создана. Запустите игру, чтобы создать первый лог.");
            }
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Ошибка открытия папки", $"Не удалось открыть папку с логами: {ex.Message}");
        }
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        _updateTimer?.Stop();
        _controller?.StopGame();
    }

    private void ApplyTheme()
    {
        var savedTheme = Services.SettingsService.LoadTheme();
        if (this.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = savedTheme;
        }
    }
}
