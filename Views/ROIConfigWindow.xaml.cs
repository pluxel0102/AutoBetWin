using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI;
using AutoBet.Services;

namespace AutoBet.Views;

public sealed partial class ROIConfigWindow : Window
{
    private readonly ObservableCollection<ROIRegion> _regions = new();
    private Point _selectionStart;
    private Rectangle? _currentSelectionRect;
    private bool _isSelecting = false;

    private readonly string[] _regionNames = new[]
    {
        "🎲 Область кубиков (результат игры)",
        "🔵 Кнопка Blue (синяя ставка)",
        "🔴 Кнопка Red (красная ставка)",
        "💸 Кнопка ставки 10",
        "💸 Кнопка ставки 50",
        "💸 Кнопка ставки 100",
        "💸 Кнопка ставки 500",
        "💸 Кнопка ставки 1000",
        "💸 Кнопка ставки 2000",
        "💸 Кнопка ставки 5000",
        "💸 Кнопка ставки 10000",
        "💸 Кнопка ставки 20000",
        "✖️ Кнопка X2 (удвоение ставки)",
        "🚫 Кнопка 'Не дубль' (200,000)",
        "➡️ Область скролла вправо (4 свайпа)",
        "⬅️ Область скролла влево (4 свайпа)"
    };

    // Подробные инструкции для каждого шага
    private readonly (string instruction, string icon)[] _stepInstructions = new[]
    {
        ("Выделите ОБЛАСТЬ КУБИКОВ - где отображается результат игры (оба кубика)", "🎲"),
        ("Выделите СИНЮЮ КНОПКУ СТАВКИ - синяя кнопка для ставки на синий цвет", "🔵"),
        ("Выделите КРАСНУЮ КНОПКУ СТАВКИ - красная кнопка для ставки на красный цвет", "🔴"),
        ("Выделите КНОПКУ СТАВКИ 10 - кнопка с номиналом 10", "💸"),
        ("Выделите КНОПКУ СТАВКИ 50 - кнопка с номиналом 50", "💸"),
        ("Выделите КНОПКУ СТАВКИ 100 - кнопка с номиналом 100", "💸"),
        ("Выделите КНОПКУ СТАВКИ 500 - кнопка с номиналом 500", "💸"),
        ("Выделите КНОПКУ СТАВКИ 1000 - кнопка с номиналом 1000", "💸"),
        ("Выделите КНОПКУ СТАВКИ 2000 - кнопка с номиналом 2000", "💸"),
        ("Выделите КНОПКУ СТАВКИ 5000 - кнопка с номиналом 5000", "💸"),
        ("Выделите КНОПКУ СТАВКИ 10000 - кнопка с номиналом 10000", "💸"),
        ("Выделите КНОПКУ СТАВКИ 20000 - кнопка с номиналом 20000", "💸"),
        ("Выделите КНОПКУ X2 - кнопка удвоения ставки (множитель)", "✖️"),
        ("Выделите КНОПКУ 'НЕ ДУБЛЬ' - ставка 200,000 на то, что не выпадет дубль", "🚫"),
        ("Выделите ОБЛАСТЬ ДЛЯ СКРОЛЛА ВПРАВО - область для 4 свайпов вправо по списку ставок", "➡️"),
        ("Выделите ОБЛАСТЬ ДЛЯ СКРОЛЛА ВЛЕВО - область для 4 свайпов влево (возврат)", "⬅️")
    };

    public ROIConfigWindow()
    {
        this.InitializeComponent();
        
        // Настраиваем кастомный заголовок
        SetupCustomTitleBar();
        
        // Максимизируем окно
        var presenter = this.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
        if (presenter != null)
        {
            presenter.Maximize();
        }
        
        Console.WriteLine($"[ROIConfigWindow] Окно максимизировано");
        
        ROIListRepeater.ItemsSource = _regions;
        
        // Загружаем сохранённые ROI если есть
        LoadSavedROI();
        
        DispatcherQueue.TryEnqueue(() => AnimateEntrance());
    }

    private void SetupCustomTitleBar()
    {
        // Настраиваем presenter
        var presenter = this.AppWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
        if (presenter != null)
        {
            presenter.SetBorderAndTitleBar(false, false);
        }
        
        // Включаем кастомный заголовок
        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(AppTitleBar);
    }

    /// <summary>
    /// Устанавливает скриншот для отображения в окне
    /// </summary>
    public async void SetScreenshot(byte[] screenshotData)
    {
        try
        {
            Console.WriteLine("[ROIConfigWindow] === SetScreenshot начат ===");
            
            if (screenshotData == null || screenshotData.Length == 0)
            {
                Console.WriteLine("[ROIConfigWindow] ✗ Пустые данные скриншота!");
                return;
            }

            Console.WriteLine($"[ROIConfigWindow] Данные скриншота: {screenshotData.Length} байт");
            
            // Конвертируем byte[] в BitmapImage
            Console.WriteLine("[ROIConfigWindow] Создание BitmapImage...");
            var bitmapImage = new BitmapImage();
            
            Console.WriteLine("[ROIConfigWindow] Создание InMemoryRandomAccessStream...");
            using (var stream = new InMemoryRandomAccessStream())
            {
                Console.WriteLine("[ROIConfigWindow] Запись в поток...");
                await stream.WriteAsync(screenshotData.AsBuffer());
                stream.Seek(0);
                
                Console.WriteLine("[ROIConfigWindow] Установка источника изображения...");
                await bitmapImage.SetSourceAsync(stream);
            }
            
            Console.WriteLine($"[ROIConfigWindow] Изображение загружено: {bitmapImage.PixelWidth}x{bitmapImage.PixelHeight}");
            
            // Показываем захваченное изображение
            Console.WriteLine("[ROIConfigWindow] Скрытие PlaceholderPanel...");
            PlaceholderPanel.Visibility = Visibility.Collapsed;
            
            Console.WriteLine("[ROIConfigWindow] Показ ScreenshotScrollViewer...");
            ScreenshotScrollViewer.Visibility = Visibility.Visible;
            
            Console.WriteLine("[ROIConfigWindow] Установка источника изображения в ScreenshotImage...");
            ScreenshotImage.Source = bitmapImage;
            
            // Устанавливаем размер Canvas под изображение
            ROICanvas.Width = bitmapImage.PixelWidth;
            ROICanvas.Height = bitmapImage.PixelHeight;
            ScreenshotImage.Width = bitmapImage.PixelWidth;
            ScreenshotImage.Height = bitmapImage.PixelHeight;
            
            // Автоматически масштабируем скриншот для вмещения в окно
            await System.Threading.Tasks.Task.Delay(100); // Даем время для отрисовки
            FitScreenshotToView();
            
            // Показываем первую инструкцию
            UpdateCurrentInstruction();
            
            Console.WriteLine("[ROIConfigWindow] Показ диалога...");
            await ShowInfoDialog("📸 Захват экрана", "Скриншот загружен! Следуйте пошаговым инструкциям выше для выделения областей.");
            
            Console.WriteLine($"[ROIConfigWindow] ✓ Скриншот успешно установлен!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ROIConfigWindow] ✗✗✗ ОШИБКА SetScreenshot ✗✗✗");
            Console.WriteLine($"[ROIConfigWindow] Сообщение: {ex.Message}");
            Console.WriteLine($"[ROIConfigWindow] Тип: {ex.GetType().Name}");
            Console.WriteLine($"[ROIConfigWindow] Stack trace:");
            Console.WriteLine(ex.StackTrace);
            
            await ShowInfoDialog("❌ Ошибка", $"Не удалось загрузить скриншот: {ex.Message}");
        }
    }

    private void LoadSavedROI()
    {
        var savedROI = ScreenCaptureService.LoadROIConfiguration();
        if (savedROI != null && savedROI.Length == 16)
        {
            foreach (var roi in savedROI)
            {
                _regions.Add(new ROIRegion
                {
                    Name = roi.Name,
                    X = roi.X,
                    Y = roi.Y,
                    Width = roi.Width,
                    Height = roi.Height
                });
            }
            UpdateProgress();
            
            // Показываем информацию о загруженных областях
            System.Diagnostics.Debug.WriteLine($"[ROIConfigWindow] Загружено {_regions.Count} сохранённых областей");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[ROIConfigWindow] Сохранённые области не найдены, начинаем с нуля");
        }
    }

    private async void AnimateEntrance()
    {
        PreviewPanel.Opacity = 1;
        PreviewPanel.Scale = new System.Numerics.Vector3(1, 1, 1);
        await System.Threading.Tasks.Task.Delay(100);
        StatsPanel.Opacity = 1;
        StatsPanel.Scale = new System.Numerics.Vector3(1, 1, 1);
        await System.Threading.Tasks.Task.Delay(100);
        ListPanel.Opacity = 1;
        ListPanel.Scale = new System.Numerics.Vector3(1, 1, 1);
        await System.Threading.Tasks.Task.Delay(100);
        InfoPanel.Opacity = 1;
        InfoPanel.Scale = new System.Numerics.Vector3(1, 1, 1);
        await System.Threading.Tasks.Task.Delay(100);
        BottomPanel.Opacity = 1;
        BottomPanel.Translation = new System.Numerics.Vector3(0, 0, 0);
    }

    private async void CaptureScreenButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Показываем индикатор загрузки
            CaptureScreenButton.IsEnabled = false;
            
            // Минимизируем окно
            ((FrameworkElement)this.Content).Opacity = 0;
            
            // Ждём 500ms чтобы окно успело скрыться
            await System.Threading.Tasks.Task.Delay(500);
            
            // Захватываем весь экран
            var screenshot = await ScreenCaptureService.CaptureFullScreen();
            
            // Показываем окно обратно
            ((FrameworkElement)this.Content).Opacity = 1;
            this.Activate();
            CaptureScreenButton.IsEnabled = true;
            
            if (screenshot != null)
            {
                // Конвертируем byte[] в BitmapImage
                var bitmapImage = new BitmapImage();
                using (var stream = new InMemoryRandomAccessStream())
                {
                    await stream.WriteAsync(screenshot.AsBuffer());
                    stream.Seek(0);
                    await bitmapImage.SetSourceAsync(stream);
                }
                
                // Показываем захваченное изображение
                PlaceholderPanel.Visibility = Visibility.Collapsed;
                ScreenshotScrollViewer.Visibility = Visibility.Visible;
                ScreenshotImage.Source = bitmapImage;
                
                // Устанавливаем размер Canvas под изображение
                ROICanvas.Width = bitmapImage.PixelWidth;
                ROICanvas.Height = bitmapImage.PixelHeight;
                ScreenshotImage.Width = bitmapImage.PixelWidth;
                ScreenshotImage.Height = bitmapImage.PixelHeight;
                
                // Автоматически масштабируем скриншот для вмещения в окно
                await System.Threading.Tasks.Task.Delay(100); // Даем время для отрисовки
                FitScreenshotToView();
                
                // Показываем первую инструкцию
                UpdateCurrentInstruction();
                
                await ShowInfoDialog("📸 Захват экрана", "Скриншот сделан! Следуйте пошаговым инструкциям выше для выделения областей.");
                System.Diagnostics.Debug.WriteLine($"[ROIConfig] Скриншот захвачен: {bitmapImage.PixelWidth}x{bitmapImage.PixelHeight}");
            }
            else
            {
                await ShowInfoDialog("❌ Ошибка", "Не удалось захватить экран. Попробуйте ещё раз.");
                System.Diagnostics.Debug.WriteLine("[ROIConfig] Ошибка захвата скриншота");
            }
        }
        catch (Exception ex)
        {
            CaptureScreenButton.IsEnabled = true;
            await ShowInfoDialog("❌ Ошибка", $"Ошибка захвата экрана: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ROIConfig] Исключение при захвате: {ex}");
        }
    }

    private void ROICanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_regions.Count >= 16)
        {
            _ = ShowInfoDialog("⚠️ Внимание", "Все 16 областей уже выделены.");
            return;
        }

        _isSelecting = true;
        _selectionStart = e.GetCurrentPoint(ROICanvas).Position;
        _currentSelectionRect = new Rectangle
        {
            Stroke = new SolidColorBrush(Microsoft.UI.Colors.DeepSkyBlue),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(40, 0, 191, 255)),
            StrokeDashArray = new Microsoft.UI.Xaml.Media.DoubleCollection { 5, 3 }
        };

        Canvas.SetLeft(_currentSelectionRect, _selectionStart.X);
        Canvas.SetTop(_currentSelectionRect, _selectionStart.Y);
        ROICanvas.Children.Add(_currentSelectionRect);
        ROICanvas.CapturePointer(e.Pointer);
    }

    private void ROICanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isSelecting || _currentSelectionRect == null) return;

        var currentPoint = e.GetCurrentPoint(ROICanvas).Position;
        var x = Math.Min(_selectionStart.X, currentPoint.X);
        var y = Math.Min(_selectionStart.Y, currentPoint.Y);
        var width = Math.Abs(currentPoint.X - _selectionStart.X);
        var height = Math.Abs(currentPoint.Y - _selectionStart.Y);

        Canvas.SetLeft(_currentSelectionRect, x);
        Canvas.SetTop(_currentSelectionRect, y);
        _currentSelectionRect.Width = width;
        _currentSelectionRect.Height = height;
    }

    private void ROICanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isSelecting || _currentSelectionRect == null) return;

        _isSelecting = false;
        ROICanvas.ReleasePointerCapture(e.Pointer);

        var currentPoint = e.GetCurrentPoint(ROICanvas).Position;
        var width = Math.Abs(currentPoint.X - _selectionStart.X);
        var height = Math.Abs(currentPoint.Y - _selectionStart.Y);

        if (width < 20 || height < 20)
        {
            ROICanvas.Children.Remove(_currentSelectionRect);
            _currentSelectionRect = null;
            return;
        }

        var x = (int)Math.Min(_selectionStart.X, currentPoint.X);
        var y = (int)Math.Min(_selectionStart.Y, currentPoint.Y);

        var region = new ROIRegion
        {
            Name = _regionNames[_regions.Count],
            X = x,
            Y = y,
            Width = (int)width,
            Height = (int)height
        };

        _regions.Add(region);
        _currentSelectionRect.Stroke = new SolidColorBrush(Microsoft.UI.Colors.LimeGreen);
        _currentSelectionRect.Fill = new SolidColorBrush(Color.FromArgb(30, 50, 205, 50));
        _currentSelectionRect.StrokeDashArray = null;

        var label = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(200, 0, 120, 215)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4, 8, 4),
            Child = new TextBlock
            {
                Text = $"{_regions.Count}",
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White)
            }
        };

        Canvas.SetLeft(label, x);
        Canvas.SetTop(label, y - 30);
        ROICanvas.Children.Add(label);

        _currentSelectionRect = null;
        UpdateProgress();
    }

    private void UpdateProgress()
    {
        ROICountText.Text = $"{_regions.Count} / 16";
        ROIProgressBar.Value = _regions.Count;
        SaveButton.IsEnabled = _regions.Count == 16;
        
        // Обновляем текущую инструкцию
        UpdateCurrentInstruction();
    }

    /// <summary>
    /// Обновляет панель с текущей инструкцией
    /// </summary>
    private async void UpdateCurrentInstruction()
    {
        if (_regions.Count >= 16)
        {
            // Все области выделены
            CurrentInstructionPanel.Visibility = Visibility.Collapsed;
            
            // Показываем сообщение о завершении
            await ShowInfoDialog("Отлично!", "Все 16 областей успешно выделены! Теперь нажмите 'Сохранить' для сохранения конфигурации.");
            return;
        }

        // Показываем панель с инструкцией
        CurrentInstructionPanel.Visibility = Visibility.Visible;
        
        var currentStep = _regions.Count;
        
        // Анимация смены инструкции
        if (currentStep > 0)
        {
            // Плавное исчезновение
            CurrentInstructionPanel.Opacity = 0;
            await System.Threading.Tasks.Task.Delay(150);
        }
        
        CurrentStepText.Text = $"Шаг {currentStep + 1} из 16";
        CurrentInstructionText.Text = _stepInstructions[currentStep].instruction;
        CurrentInstructionIcon.Text = _stepInstructions[currentStep].icon;
        
        // Плавное появление
        CurrentInstructionPanel.Opacity = 1;
        
        Console.WriteLine($"[ROIConfigWindow] Показана инструкция: Шаг {currentStep + 1} - {_stepInstructions[currentStep].instruction}");
    }

    private void ROIItem_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid) grid.Opacity = 0.7;
    }

    private void ROIItem_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid) grid.Opacity = 1.0;
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _regions.Clear();
        ROICanvas.Children.Clear();
        UpdateProgress();
    }

    private async void DeleteRegionsButton_Click(object sender, RoutedEventArgs e)
    {
        // Показываем диалог подтверждения
        var dialog = new ContentDialog
        {
            Title = "⚠️ Удаление всех областей",
            Content = "Вы действительно хотите удалить все настроенные области ROI?\n\nЭто действие нельзя отменить.",
            PrimaryButtonText = "Удалить",
            CloseButtonText = "Отмена",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        
        if (result == ContentDialogResult.Primary)
        {
            // Удаляем файл настроек
            ScreenCaptureService.DeleteROIConfiguration();
            
            // Очищаем текущие области
            _regions.Clear();
            ROICanvas.Children.Clear();
            
            UpdateProgress();
            
            await ShowInfoDialog("Успешно", "Все области ROI удалены. Вы можете настроить их заново.");
            System.Diagnostics.Debug.WriteLine($"[ROIConfigWindow] Все области ROI удалены");
        }
    }

    private void LoadTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        var savedROI = ScreenCaptureService.LoadROIConfiguration();
        
        if (savedROI == null || savedROI.Length == 0)
        {
            _ = ShowInfoDialog("📥 Загрузка шаблона", "Сохранённых областей не найдено. Сначала настройте и сохраните ROI.");
            return;
        }

        // Очищаем текущие области
        _regions.Clear();
        ROICanvas.Children.Clear();

        // Загружаем сохранённые
        foreach (var roi in savedROI)
        {
            _regions.Add(new ROIRegion
            {
                Name = roi.Name,
                X = roi.X,
                Y = roi.Y,
                Width = roi.Width,
                Height = roi.Height
            });
        }

        UpdateProgress();
        _ = ShowInfoDialog("Успешно", $"Загружено {savedROI.Length} областей из сохранённого шаблона.");
        System.Diagnostics.Debug.WriteLine($"[ROIConfigWindow] Шаблон загружен: {savedROI.Length} областей");
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => this.Close();

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_regions.Count != 16)
        {
            await ShowInfoDialog("⚠️ Внимание", "Необходимо выделить все 16 областей.");
            return;
        }

        var roiArray = _regions.Select(r => new ScreenCaptureService.MelBetROI
        {
            Name = r.Name,
            X = r.X,
            Y = r.Y,
            Width = r.Width,
            Height = r.Height
        }).ToArray();

        ScreenCaptureService.SaveROIConfiguration(roiArray);
        await ShowInfoDialog("Успешно", "Конфигурация областей сохранена!");
        this.Close();
    }

    /// <summary>
    /// Автоматически масштабирует скриншот для вмещения в доступное пространство окна
    /// </summary>
    private void FitScreenshotToView()
    {
        try
        {
            // Получаем доступное пространство для отображения
            var availableWidth = PreviewPanel.ActualWidth - 48; // Минус padding
            var availableHeight = PreviewPanel.ActualHeight - 48;
            
            // Получаем размеры скриншота
            var imageWidth = ROICanvas.Width;
            var imageHeight = ROICanvas.Height;
            
            if (imageWidth <= 0 || imageHeight <= 0 || availableWidth <= 0 || availableHeight <= 0)
            {
                Console.WriteLine("[ROIConfigWindow] Некорректные размеры для масштабирования");
                return;
            }
            
            // Рассчитываем коэффициент масштабирования
            var scaleWidth = availableWidth / imageWidth;
            var scaleHeight = availableHeight / imageHeight;
            var maxScale = Math.Min(scaleWidth, scaleHeight);
            
            // Используем 95% от максимального масштаба для лучшей видимости
            // Это даёт небольшой отступ и делает скриншот крупнее
            var scale = maxScale * 0.95;
            
            // Ограничиваем минимальный масштаб, но позволяем масштабирование больше 1.0
            scale = Math.Max(0.1, Math.Min(2.0, scale));
            
            Console.WriteLine($"[ROIConfigWindow] Масштабирование: {imageWidth}x{imageHeight}");
            Console.WriteLine($"[ROIConfigWindow] Доступное пространство: {availableWidth}x{availableHeight}");
            Console.WriteLine($"[ROIConfigWindow] Применён масштаб: {scale:F2}x (95% от max {maxScale:F2}x)");
            
            // Применяем масштаб (преобразуем в float)
            ScreenshotScrollViewer.ChangeView(0, 0, (float)scale);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ROIConfigWindow] Ошибка масштабирования: {ex.Message}");
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private async System.Threading.Tasks.Task ShowInfoDialog(string title, string message)
    {
        var titleTextBlock = new TextBlock
        {
            Text = title,
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        
        // Если заголовок "Успешно", делаем его зелёным
        if (title == "Успешно" || title == "Отлично!")
        {
            titleTextBlock.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 185, 129)); // Зелёный цвет
        }
        
        var dialog = new ContentDialog
        {
            Title = titleTextBlock,
            Content = message,
            CloseButtonText = "ОК",
            XamlRoot = this.Content.XamlRoot
        };
        await dialog.ShowAsync();
    }
}

public class ROIRegion
{
    public string Name { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
