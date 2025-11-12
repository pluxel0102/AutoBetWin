using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using AutoBet.Models;
using AutoBet.Services;

namespace AutoBet.Controllers;

/// <summary>
/// Контроллер для MelBet режима (игра в нарды)
/// Портировано из Python версии melbet_controller.py
/// </summary>
public class MelBetController
{
    // Константы
    private const int DETECTION_INTERVAL_MS = 50;
    private const int STABLE_HASH_DURATION_MS = 1500;
    private const int MAX_DETECTION_TIME_MS = 300000; // 5 минут
    private const int CLICK_DELAY_MS = 500;
    private const int BETWEEN_CLICKS_DELAY_MS = 300;

    // Состояние и настройки
    public MelBetGameState GameState { get; private set; }
    public MelBetSettings Settings { get; private set; }
    
    private StaticFrameDetector _staticDetector;
    private string? _apiKey;
    
    // ROI области - 16 областей
    private (int X, int Y, int Width, int Height)? _diceArea;
    private (int X, int Y, int Width, int Height)? _blueBetArea;
    private (int X, int Y, int Width, int Height)? _redBetArea;
    private Dictionary<int, (int X, int Y, int Width, int Height)?> _betButtons;
    private (int X, int Y, int Width, int Height)? _multiplierX2Area;
    private (int X, int Y, int Width, int Height)? _noDoubleBetArea;
    private (int X, int Y, int Width, int Height)? _scrollRightArea;
    private (int X, int Y, int Width, int Height)? _scrollLeftArea;
    
    // Состояние игры
    private bool _isActive = false;
    private bool _isPaused = false;
    private Thread? _gameThread;
    private CancellationTokenSource? _cancellationTokenSource;
    
    // События для UI
    public event Action<string>? OnLogMessage;
    public event Action<string, Exception?>? OnError;
    public event Action<MelBetGameState>? OnStateChanged;
    public event Action<string>? OnGameStopped;

    public MelBetController()
    {
        GameState = new MelBetGameState();
        Settings = new MelBetSettings();
        _staticDetector = new StaticFrameDetector();
        _betButtons = new Dictionary<int, (int X, int Y, int Width, int Height)?>();
        
        Log("MelBetController инициализирован");
    }

    /// <summary>
    /// Проверка настройки всех необходимых ROI областей
    /// </summary>
    public bool AreROIAreasConfigured
    {
        get
        {
            // Обязательные области
            if (_diceArea == null || _blueBetArea == null || _redBetArea == null)
                return false;
            
            // Хотя бы одна кнопка ставки
            if (!_betButtons.Values.Any(v => v.HasValue))
                return false;
            
            // X2 обязательна
            if (_multiplierX2Area == null)
                return false;
            
            return true;
        }
    }

    /// <summary>
    /// Загрузить настройки и ROI области
    /// </summary>
    public bool LoadSettings()
    {
        try
        {
            Log("📊 Загрузка настроек MelBet...");
            
            // Загружаем настройки игры (базовая ставка, цвет, стратегия)
            Settings.BaseBet = SettingsService.LoadMelBetBaseBet();
            
            var colorString = SettingsService.LoadMelBetPreferredColor();
            Settings.PreferredColor = colorString == "Red" ? BetColor.Red : BetColor.Blue;
            
            Settings.ColorSwitchAfterLosses = SettingsService.LoadMelBetColorSwitchAfterLosses();
            Settings.Strategy = SettingsService.LoadMelBetStrategy();
            
            Log($"⚙️ Базовая ставка: {Settings.BaseBet}, Цвет: {Settings.PreferredColor}, Стратегия: {Settings.Strategy}");
            
            // Загружаем ROI области
            var regions = SettingsService.LoadMelBetROI();
            if (regions == null || regions.Length != 16)
            {
                LogError("❌ ROI области не настроены или неполные");
                return false;
            }
            
            // Маппинг по названиям областей
            foreach (var region in regions)
            {
                var area = (region.X, region.Y, region.Width, region.Height);
                
                if (region.Name.Contains("кубик", StringComparison.OrdinalIgnoreCase))
                    _diceArea = area;
                else if (region.Name.Contains("Blue", StringComparison.OrdinalIgnoreCase))
                    _blueBetArea = area;
                else if (region.Name.Contains("Red", StringComparison.OrdinalIgnoreCase))
                    _redBetArea = area;
                // Важно: проверяем длинные числа ПЕРВЫМИ, потом короткие!
                else if (region.Name.Contains("ставки 20000"))
                    _betButtons[20000] = area;
                else if (region.Name.Contains("ставки 10000"))
                    _betButtons[10000] = area;
                else if (region.Name.Contains("ставки 5000"))
                    _betButtons[5000] = area;
                else if (region.Name.Contains("ставки 2000"))
                    _betButtons[2000] = area;
                else if (region.Name.Contains("ставки 1000"))
                    _betButtons[1000] = area;
                else if (region.Name.Contains("ставки 500"))
                    _betButtons[500] = area;
                else if (region.Name.Contains("ставки 100"))
                    _betButtons[100] = area;
                else if (region.Name.Contains("ставки 50"))
                    _betButtons[50] = area;
                else if (region.Name.Contains("ставки 10"))
                    _betButtons[10] = area;
                else if (region.Name.Contains("X2"))
                    _multiplierX2Area = area;
                else if (region.Name.Contains("Не дубль"))
                    _noDoubleBetArea = area;
                else if (region.Name.Contains("вправо"))
                    _scrollRightArea = area;
                else if (region.Name.Contains("влево"))
                    _scrollLeftArea = area;
            }
            
            // Логируем загруженные области
            var loadedAreas = new List<string>();
            if (_diceArea.HasValue) loadedAreas.Add("dice");
            if (_blueBetArea.HasValue) loadedAreas.Add("blue");
            if (_redBetArea.HasValue) loadedAreas.Add("red");
            foreach (var bet in _betButtons.Where(b => b.Value.HasValue))
                loadedAreas.Add($"bet_{bet.Key}");
            if (_multiplierX2Area.HasValue) loadedAreas.Add("x2");
            if (_noDoubleBetArea.HasValue) loadedAreas.Add("no_double");
            if (_scrollRightArea.HasValue) loadedAreas.Add("scroll_right");
            if (_scrollLeftArea.HasValue) loadedAreas.Add("scroll_left");
            
            Log($"📊 Загружено областей: {loadedAreas.Count}/16: {string.Join(", ", loadedAreas)}");
            
            // Загружаем API ключ OpenRouter
            _apiKey = SettingsService.LoadApiKey();
            if (!string.IsNullOrEmpty(_apiKey))
            {
                Log("✅ OpenRouter API ключ загружен");
            }
            else
            {
                Log("⚠️ OpenRouter API ключ не найден");
            }
            
            Log("✅ Настройки MelBet загружены");
            return true;
        }
        catch (Exception ex)
        {
            LogError($"❌ Ошибка загрузки настроек: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Запустить игру
    /// </summary>
    public void StartGame()
    {
        if (_isActive)
        {
            Log("⚠️ Игра уже запущена");
            return;
        }
        
        // Загружаем настройки и ROI области
        if (!LoadSettings())
        {
            LogError("❌ Не удалось загрузить настройки");
            OnGameStopped?.Invoke("Не удалось загрузить настройки");
            return;
        }
        
        // Валидация
        var (isValid, errorMsg) = Settings.Validate();
        if (!isValid)
        {
            LogError($"❌ Ошибка валидации настроек: {errorMsg}");
            OnGameStopped?.Invoke($"Ошибка валидации: {errorMsg}");
            return;
        }
        
        if (!ValidateROI())
        {
            LogError("❌ Не настроены все необходимые ROI области");
            OnGameStopped?.Invoke("Не настроены ROI области. Откройте настройки ROI через главное окно.");
            return;
        }
        
        // Инициализация
        _isActive = true;
        _isPaused = false;
        _cancellationTokenSource = new CancellationTokenSource();
        
        GameState.IsGameActive = true;
        GameState.GameStartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        GameState.CurrentColor = Settings.PreferredColor;
        GameState.CurrentBet = Settings.BaseBet;
        _staticDetector.Reset();
        
        // Запуск игрового потока
        _gameThread = new Thread(() => GameLoop(_cancellationTokenSource.Token))
        {
            IsBackground = true,
            Name = "MelBet Game Loop"
        };
        _gameThread.Start();
        
        Log("🚀 MelBet игра запущена!");
        NotifyStateChanged();
    }

    /// <summary>
    /// Остановить игру
    /// </summary>
    public void StopGame()
    {
        if (!_isActive)
            return;
        
        _isActive = false;
        GameState.IsGameActive = false;
        
        _cancellationTokenSource?.Cancel();
        
        // Ждём завершения потока
        if (_gameThread != null && _gameThread.IsAlive && Thread.CurrentThread != _gameThread)
        {
            _gameThread.Join(TimeSpan.FromSeconds(2));
        }
        
        Log("🛑 MelBet игра остановлена");
        Log("\n" + new string('=', 60));
        Log("📊 СТАТИСТИКА ПО КУБИКАМ");
        Log(new string('=', 60));
        Log($"Стратегия: {(Settings.Strategy == Models.BetStrategy.Martingale ? "📈 Мартингейл" : "🪜 Лесенка")}");
        Log(GameState.GetDiceStatisticsReport());
        Log(new string('=', 60) + "\n");
        
        OnGameStopped?.Invoke("Игра остановлена пользователем");
    }

    /// <summary>
    /// Пауза/возобновление игры
    /// </summary>
    public void TogglePause()
    {
        if (!_isActive)
            return;
        
        _isPaused = !_isPaused;
        GameState.IsPaused = _isPaused;
        
        Log(_isPaused ? "⏸️ Пауза" : "▶️ Возобновление");
        NotifyStateChanged();
    }

    /// <summary>
    /// Основной игровой цикл
    /// </summary>
    private async void GameLoop(CancellationToken cancellationToken)
    {
        try
        {
            while (_isActive && !cancellationToken.IsCancellationRequested)
            {
                // Проверка паузы
                if (_isPaused)
                {
                    await Task.Delay(500, cancellationToken);
                    continue;
                }
                
                // 1. Размещение ставки
                Log("💰 Размещение ставки...");
                await PlaceBet(cancellationToken);
                
                // 2. Ожидание результата
                Log("⏳ Ожидание результата игры...");
                var result = await WaitForGameResult(cancellationToken);
                
                if (result == null)
                {
                    Log("⚠️ Не удалось получить результат, пропускаем раунд");
                    await Task.Delay(2000, cancellationToken);
                    continue;
                }
                
                // 3. Обработка результата
                Log($"🎲 Результат: Синий={result.Value.Blue}, Красный={result.Value.Red}");
                
                var oldBet = GameState.CurrentBet;
                var oldLevel = GameState.LadderLevel;
                var oldColor = GameState.CurrentColor;
                
                GameState = GameState.ProcessGameResult(result.Value.Blue, result.Value.Red, Settings);
                
                // Логируем изменение цвета
                if (oldColor != GameState.CurrentColor)
                {
                    Log($"🔄 Смена цвета: {oldColor} → {GameState.CurrentColor}");
                }
                
                // Логируем изменение ставки для лесенки
                if (Settings.Strategy == Models.BetStrategy.Ladder && oldBet != GameState.CurrentBet)
                {
                    Log($"🪜 Лесенка: уровень {oldLevel} → {GameState.LadderLevel}, ставка {oldBet} → {GameState.CurrentBet}");
                }
                
                NotifyStateChanged();
                
                // 4. Пауза перед следующей ставкой
                Log("⏱️ Ожидание 5 секунд перед следующей ставкой...");
                await Task.Delay(5000, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            Log("⏹️ Игровой цикл отменён");
        }
        catch (Exception ex)
        {
            LogError($"❌ Ошибка в игровом цикле: {ex.Message}", ex);
        }
        finally
        {
            if (_isActive)
                StopGame();
        }
    }

    /// <summary>
    /// Размещение ставки
    /// </summary>
    private async Task PlaceBet(CancellationToken cancellationToken)
    {
        if (!_isActive || cancellationToken.IsCancellationRequested)
            return;
        
        try
        {
            // Проверяем, нужна ли ставка "Не дубль"
            if (GameState.ShouldPlaceNoDoubleBet() && Settings.EnableNoDoubleBet)
            {
                Log("🎲 Размещение ставки 'Не дубль'...");
                await PlaceNoDoubleBet(cancellationToken);
                return;
            }
            
            // Обычная ставка на цвет
            int betAmount = GameState.CurrentBet;
            Log($"🎯 Ставка: {betAmount} на {GameState.CurrentColor}");
            
            // 1. Выбираем цвет
            var colorArea = GameState.CurrentColor == BetColor.Blue ? _blueBetArea : _redBetArea;
            if (colorArea == null)
            {
                LogError("❌ Область цвета не настроена");
                return;
            }
            
            // 2. Находим оптимальную базовую кнопку и считаем нужные клики X2
            int baseAmount;
            int clicksNeeded;
            
            if (_betButtons.ContainsKey(betAmount) && _betButtons[betAmount].HasValue)
            {
                // Есть прямая кнопка для этой суммы
                baseAmount = betAmount;
                clicksNeeded = 0;
            }
            else
            {
                // Ищем наибольшую доступную кнопку <= нужной суммы, от которой можно удвоением достичь цели
                // Доступные кнопки: 10, 50, 100, 500, 1000, 2000, 5000, 10000, 20000
                var availableButtons = new[] { 10, 50, 100, 500, 1000, 2000, 5000, 10000, 20000 }
                    .Where(b => _betButtons.ContainsKey(b) && _betButtons[b].HasValue && b <= betAmount)
                    .OrderByDescending(b => b)
                    .ToList();
                
                baseAmount = 0;
                clicksNeeded = 0;
                
                foreach (var btn in availableButtons)
                {
                    // Проверяем, можно ли удвоением от этой кнопки достичь нужной суммы
                    int current = btn;
                    int clicks = 0;
                    
                    while (current < betAmount)
                    {
                        current *= 2;
                        clicks++;
                    }
                    
                    if (current == betAmount)
                    {
                        baseAmount = btn;
                        clicksNeeded = clicks;
                        break;
                    }
                }
                
                if (baseAmount == 0)
                {
                    LogError($"❌ Невозможно составить ставку {betAmount} из доступных кнопок");
                    return;
                }
            }
            
            // 3. Нажимаем базовую кнопку
            await ClickArea(_betButtons[baseAmount]!.Value, $"Выбор ставки {baseAmount}", cancellationToken);
            
            // 4. Клик на цвет (размещает базовую ставку)
            await ClickArea(colorArea.Value, $"Клик на {GameState.CurrentColor} цвет", cancellationToken);
            
            // 5. Удвоение через X2 (ПОСЛЕ клика на цвет!)
            if (clicksNeeded > 0 && _multiplierX2Area.HasValue)
            {
                Log($"💰 Удвоение от {baseAmount}: {clicksNeeded} нажатий X2 → {betAmount}");
                for (int i = 0; i < clicksNeeded && !cancellationToken.IsCancellationRequested; i++)
                {
                    await ClickArea(_multiplierX2Area.Value, $"X2 нажатие {i + 1}/{clicksNeeded}", cancellationToken);
                    await Task.Delay(BETWEEN_CLICKS_DELAY_MS, cancellationToken);
                }
            }
            
            Log($"✅ Ставка {betAmount} размещена на {GameState.CurrentColor}");
        }
        catch (Exception ex)
        {
            LogError($"❌ Ошибка размещения ставки: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Размещение ставки "Не дубль" (после 4 ничьих подряд)
    /// </summary>
    private async Task PlaceNoDoubleBet(CancellationToken cancellationToken)
    {
        // Разрешаем вызов только когда игра активна
        if (!_isActive || cancellationToken.IsCancellationRequested)
            return;
        
        try
        {
            // 1. Прокрутка вправо (4 свайпа)
            if (_scrollRightArea.HasValue)
            {
                Log("➡️ Прокрутка вправо (4x)...");
                for (int i = 0; i < 4 && !cancellationToken.IsCancellationRequested; i++)
                {
                    await ClickArea(_scrollRightArea.Value, $"Свайп вправо {i + 1}/4", cancellationToken);
                    await Task.Delay(BETWEEN_CLICKS_DELAY_MS, cancellationToken);
                }
            }
            
            await Task.Delay(CLICK_DELAY_MS, cancellationToken);
            
            // 2. Выбираем ставку 200000 (кнопка 20000 после скролла)
            if (_betButtons.ContainsKey(20000) && _betButtons[20000].HasValue)
            {
                await ClickArea(_betButtons[20000]!.Value, "Выбор ставки 200000", cancellationToken);
            }
            else
            {
                Log("⚠️ Кнопка ставки 200000 (20000) не найдена");
            }
            
            await Task.Delay(BETWEEN_CLICKS_DELAY_MS, cancellationToken);
            
            // 3. Клик на "Не дубль"
            if (_noDoubleBetArea.HasValue)
            {
                await ClickArea(_noDoubleBetArea.Value, "Выбор ставки 'Не дубль'", cancellationToken);
            }
            
            await Task.Delay(CLICK_DELAY_MS, cancellationToken);
            
            // 4. Прокрутка влево (возврат)
            if (_scrollLeftArea.HasValue)
            {
                Log("⬅️ Прокрутка влево (4x) - возврат...");
                for (int i = 0; i < 4 && !cancellationToken.IsCancellationRequested; i++)
                {
                    await ClickArea(_scrollLeftArea.Value, $"Свайп влево {i + 1}/4", cancellationToken);
                    await Task.Delay(BETWEEN_CLICKS_DELAY_MS, cancellationToken);
                }
            }
            
            Log("✅ Ставка 'Не дубль' размещена");
            
            // ВАЖНО: Сбрасываем текущую ставку к базовой из настроек после "Не дубль"
            // Иначе следующая ставка будет 200,000 вместо BaseBet из настроек
            GameState.WasNoDoubleBetPlaced = true;
            GameState.CurrentBet = Settings.BaseBet;
            Log($"🔄 Ставка сброшена к базовой из настроек: {Settings.BaseBet}");
        }
        catch (Exception ex)
        {
            LogError($"❌ Ошибка размещения ставки 'Не дубль': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Ожидание результата игры
    /// </summary>
    private async Task<(int Blue, int Red)?> WaitForGameResult(CancellationToken cancellationToken)
    {
        if (_diceArea == null)
        {
            LogError("❌ Область кубиков не настроена");
            return null;
        }
        
        var startTime = DateTime.Now;
        string? lastHash = null;
        DateTime? stableStartTime = null;
        
        while ((DateTime.Now - startTime).TotalMilliseconds < MAX_DETECTION_TIME_MS && !cancellationToken.IsCancellationRequested)
        {
            if (!_isActive || _isPaused)
            {
                await Task.Delay(500, cancellationToken);
                continue;
            }
            
            // Захват области кубиков
            var screenshot = await ScreenCaptureService.CaptureRegion(
                _diceArea.Value.X, _diceArea.Value.Y,
                _diceArea.Value.Width, _diceArea.Value.Height);
            
            if (screenshot == null)
            {
                await Task.Delay(DETECTION_INTERVAL_MS, cancellationToken);
                continue;
            }
            
            // Вычисляем хэш
            var currentHash = CalculateImageHash(screenshot);
            
            if (currentHash == lastHash)
            {
                // Изображение стабильно
                stableStartTime ??= DateTime.Now;
                
                // Если стабильно достаточно долго - анализируем
                if ((DateTime.Now - stableStartTime.Value).TotalMilliseconds >= STABLE_HASH_DURATION_MS)
                {
                    Log("🔍 Изображение стабилизировалось, анализируем...");
                    var result = await AnalyzeDice(screenshot, cancellationToken);
                    if (result != null)
                        return result;
                    
                    // Анализ не удался, сбрасываем
                    lastHash = null;
                    stableStartTime = null;
                }
            }
            else
            {
                // Изображение изменилось
                lastHash = currentHash;
                stableStartTime = null;
            }
            
            await Task.Delay(DETECTION_INTERVAL_MS, cancellationToken);
        }
        
        Log("⏱️ Таймаут ожидания результата");
        return null;
    }

    /// <summary>
    /// Анализ кубиков через AI
    /// </summary>
    private async Task<(int Blue, int Red)?> AnalyzeDice(byte[] screenshot, CancellationToken cancellationToken)
    {
        const int MAX_RETRIES = 3;
        const int RETRY_DELAY_MS = 2000;
        
        try
        {
            if (!string.IsNullOrEmpty(_apiKey))
            {
                var recognitionModel = SettingsService.LoadRecognitionModel();
                Log($"🤖 Анализ через OpenRouter (модель: {recognitionModel}, размер изображения: {screenshot.Length} байт)...");
                
                var prompt = @"You are a dice recognition system. Count the WHITE dots on each die and respond ONLY with numbers in format ""left:right"".

Image has 2 dice:
- LEFT die: BLUE background
- RIGHT die: RED background

Standard dice patterns:
1 = ● (center only)
2 = ●● (diagonal)
3 = ●●● (diagonal)
4 = ●●●● (4 corners, NO center)
5 = ●●●●● (4 corners + center)
6 = ●●●●●● (2 columns of 3)

CRITICAL: 
- 4 has NO center dot
- 5 has center dot
- 6 has two columns

YOUR RESPONSE FORMAT (examples):
4:5
1:6
3:3
6:2

Count the white dots on LEFT and RIGHT dice, then respond with ONLY ""left:right"" format. No explanations, no extra text.";
                
                // Повторяем запрос при ошибках
                for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
                {
                    if (attempt > 1)
                    {
                        Log($"🔄 Повторная попытка {attempt}/{MAX_RETRIES}...");
                        await Task.Delay(RETRY_DELAY_MS, cancellationToken);
                    }
                    
                    var apiResult = await OpenRouterService.AnalyzeImage(_apiKey, recognitionModel, screenshot, prompt, maxTokens: 20);
                    
                    if (apiResult.Success && !string.IsNullOrEmpty(apiResult.Response))
                    {
                        Log($"📥 Ответ OpenRouter: '{apiResult.Response}'");
                        var result = ParseDiceResponse(apiResult.Response);
                        if (result != null)
                        {
                            if (attempt > 1)
                                Log($"✅ Успешно после {attempt} попыток");
                            Log($"✅ Успешно распознано: Синий={result.Value.Blue}, Красный={result.Value.Red}");
                            return result;
                        }
                        else
                        {
                            Log($"⚠️ Не удалось распарсить ответ: '{apiResult.Response}'");
                        }
                    }
                    else if (!string.IsNullOrEmpty(apiResult.ErrorMessage))
                    {
                        Log($"❌ ОШИБКА OpenRouter (попытка {attempt}/{MAX_RETRIES}): {apiResult.ErrorMessage}");
                        
                        // Проверяем, есть ли смысл повторять
                        if (apiResult.ErrorMessage.Contains("503") || 
                            apiResult.ErrorMessage.Contains("upstream connect error") ||
                            apiResult.ErrorMessage.Contains("Provider returned error"))
                        {
                            if (attempt < MAX_RETRIES)
                                continue; // Повторяем при 503
                        }
                        else
                        {
                            // Другие ошибки (401, 429, etc.) - не повторяем
                            LogError($"OpenRouter API error", null);
                            break;
                        }
                    }
                }
                
                Log($"⚠️ AI распознавание не удалось после {MAX_RETRIES} попыток");
            }
            else
            {
                Log("⚠️ API ключ не настроен");
            }
            
            return null;
        }
        catch (Exception ex)
        {
            LogError($"❌ Ошибка анализа кубиков: {ex.Message}", ex);
            return null;
        }
    }

    /// <summary>
    /// Парсинг ответа AI
    /// </summary>
    private (int Blue, int Red)? ParseDiceResponse(string response)
    {
        try
        {
            var match = System.Text.RegularExpressions.Regex.Match(response, @"(\d):(\d)");
            if (match.Success)
            {
                int left = int.Parse(match.Groups[1].Value);
                int right = int.Parse(match.Groups[2].Value);
                if (left >= 1 && left <= 6 && right >= 1 && right <= 6)
                    return (left, right);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Клик по области
    /// </summary>
    private async Task ClickArea((int X, int Y, int Width, int Height) area, string description, CancellationToken cancellationToken)
    {
        if (Settings.EnableTestMode)
        {
            Log($"🧪 [TEST MODE] Клик: {description}");
            await Task.Delay(CLICK_DELAY_MS, cancellationToken);
            return;
        }
        
        try
        {
            await InputSimulator.ClickAreaAsync(area.X, area.Y, area.Width, area.Height, CLICK_DELAY_MS);
            Log($"🖱️ Клик: {description}");
        }
        catch (Exception ex)
        {
            LogError($"❌ Ошибка клика: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Вычисление хэша изображения
    /// </summary>
    private string CalculateImageHash(byte[] image)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(image);
        return BitConverter.ToString(hash).Replace("-", "");
    }

    /// <summary>
    /// Валидация ROI областей
    /// </summary>
    private bool ValidateROI()
    {
        var required = new Dictionary<string, bool>
        {
            ["dice_area"] = _diceArea.HasValue,
            ["blue_bet_area"] = _blueBetArea.HasValue,
            ["red_bet_area"] = _redBetArea.HasValue,
            ["bet_10_area"] = _betButtons.ContainsKey(10) && _betButtons[10].HasValue,
            ["multiplier_x2_area"] = _multiplierX2Area.HasValue
        };
        
        foreach (var item in required.Where(x => !x.Value))
        {
            LogError($"❌ Не настроена область: {item.Key}");
            return false;
        }
        
        return true;
    }

    // Вспомогательные методы
    private void Log(string message)
    {
        Debug.WriteLine($"[MelBet] {message}");
        OnLogMessage?.Invoke(message);
    }

    private void LogError(string message, Exception? ex = null)
    {
        Debug.WriteLine($"[MelBet ERROR] {message}");
        if (ex != null)
            Debug.WriteLine($"[MelBet ERROR] Exception: {ex}");
        OnError?.Invoke(message, ex);
    }

    private void NotifyStateChanged()
    {
        OnStateChanged?.Invoke(GameState);
    }

    public bool IsGameActive => _isActive;
    public bool IsPaused => _isPaused;
}
