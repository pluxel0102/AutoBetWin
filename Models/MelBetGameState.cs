using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace AutoBet.Models;

/// <summary>
/// Состояние игры MelBet
/// </summary>
public class MelBetGameState
{
    /// <summary>
    /// Активна ли игра
    /// </summary>
    public bool IsGameActive { get; set; } = false;
    
    /// <summary>
    /// На паузе ли игра
    /// </summary>
    public bool IsPaused { get; set; } = false;
    
    /// <summary>
    /// Время начала игры (Unix timestamp в миллисекундах)
    /// </summary>
    public long GameStartTime { get; set; } = 0;
    
    /// <summary>
    /// Текущая ставка
    /// </summary>
    public int CurrentBet { get; set; } = 10;
    
    /// <summary>
    /// Текущий выбранный цвет
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public BetColor CurrentColor { get; set; } = BetColor.Blue;
    
    /// <summary>
    /// Количество последовательных ничьих
    /// </summary>
    public int ConsecutiveDraws { get; set; } = 0;
    
    /// <summary>
    /// Количество последовательных проигрышей на текущем цвете
    /// </summary>
    public int ConsecutiveLosses { get; set; } = 0;
    
    /// <summary>
    /// Текущий уровень лесенки (для стратегии Ladder)
    /// </summary>
    public int LadderLevel { get; set; } = 0;
    
    /// <summary>
    /// Сколько еще раз нужно оставаться на текущем уровне лесенки (для стратегии Ladder)
    /// </summary>
    public int LadderStaysRemaining { get; set; } = 0;
    
    /// <summary>
    /// Была ли размещена ставка "Не дубль"
    /// </summary>
    public bool WasNoDoubleBetPlaced { get; set; } = false;
    
    /// <summary>
    /// Всего сыграно раундов
    /// </summary>
    public int TotalRounds { get; set; } = 0;
    
    /// <summary>
    /// Количество побед
    /// </summary>
    public int Wins { get; set; } = 0;
    
    /// <summary>
    /// Количество поражений
    /// </summary>
    public int Losses { get; set; } = 0;
    
    /// <summary>
    /// Количество ничьих
    /// </summary>
    public int Draws { get; set; } = 0;
    
    /// <summary>
    /// Текущий баланс (виртуальный, для статистики)
    /// </summary>
    public int Balance { get; set; } = 0;
    
    /// <summary>
    /// Статистика по результатам кубиков (ключ: "left:right", значение: количество)
    /// </summary>
    public Dictionary<string, int> DiceStatistics { get; set; } = new Dictionary<string, int>();
    
    /// <summary>
    /// История бросков кубиков (для детальной статистики)
    /// </summary>
    public List<(int Blue, int Red)> RollsHistory { get; set; } = new List<(int Blue, int Red)>();
    
    /// <summary>
    /// Последнее значение синего кубика
    /// </summary>
    public int LastBlueValue { get; set; } = 0;
    
    /// <summary>
    /// Последнее значение красного кубика
    /// </summary>
    public int LastRedValue { get; set; } = 0;
    
    /// <summary>
    /// Проверка, нужна ли ставка "Не дубль"
    /// </summary>
    public bool ShouldPlaceNoDoubleBet()
    {
        return ConsecutiveDraws >= 4 && !WasNoDoubleBetPlaced;
    }
    
    /// <summary>
    /// Расчет количества нажатий X2 для достижения текущей ставки
    /// </summary>
    public int GetDoublingClicksNeeded()
    {
        if (CurrentBet <= 10) return 0;
        
        // Проверяем, является ли ставка степенью двойки, умноженной на 10
        int bet = CurrentBet;
        int clicks = 0;
        
        while (bet > 10 && bet % 2 == 0)
        {
            bet /= 2;
            clicks++;
        }
        
        return bet == 10 ? clicks : 0;
    }
    
    /// <summary>
    /// Применение стратегии управления ставками при проигрыше
    /// </summary>
    /// <param name="settings">Настройки игры</param>
    private void ApplyBettingStrategyOnLoss(MelBetSettings settings)
    {
        System.Diagnostics.Debug.WriteLine($"[MelBetGameState] ApplyBettingStrategyOnLoss вызван. Стратегия: {settings.Strategy}, ConsecutiveLosses: {ConsecutiveLosses}");
        
        if (settings.Strategy == BetStrategy.Martingale)
        {
            // Классический Мартингейл - удвоение после каждого проигрыша
            CurrentBet = CurrentBet * 2;
            
            System.Diagnostics.Debug.WriteLine($"[MelBetGameState] 📈 Мартингейл: ставка увеличена до {CurrentBet}");
        }
        else if (settings.Strategy == BetStrategy.Ladder)
        {
            // Стратегия "Лесенка"
            // Удваиваем ставку после каждого проигрыша
            CurrentBet = CurrentBet * 2;
            LadderLevel++;
            
            System.Diagnostics.Debug.WriteLine($"[MelBetGameState] 🪜 Лесенка: проигрыш #{ConsecutiveLosses}, ставка удвоена до {CurrentBet}, уровень {LadderLevel}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[MelBetGameState] ⚠️ Неизвестная стратегия: {settings.Strategy}");
        }
    }
    
    /// <summary>
    /// Обработка результата раунда
    /// </summary>
    /// <param name="blueResult">Результат синего кубика (1-6)</param>
    /// <param name="redResult">Результат красного кубика (1-6)</param>
    /// <param name="settings">Настройки игры для применения стратегии</param>
    /// <returns>Новое состояние игры</returns>
    public MelBetGameState ProcessGameResult(int blueResult, int redResult, MelBetSettings settings)
    {
        var newState = this.Clone();
        newState.TotalRounds++;
        
        // Сохраняем последние значения
        newState.LastBlueValue = blueResult;
        newState.LastRedValue = redResult;
        
        // Добавляем в историю бросков (ограничиваем до 100)
        newState.RollsHistory.Add((blueResult, redResult));
        if (newState.RollsHistory.Count > 100)
        {
            newState.RollsHistory.RemoveAt(0);
        }
        
        // Сохраняем статистику по кубикам
        string diceKey = $"{blueResult}:{redResult}";
        if (!newState.DiceStatistics.ContainsKey(diceKey))
            newState.DiceStatistics[diceKey] = 0;
        newState.DiceStatistics[diceKey]++;
        
        // Определяем результат
        if (blueResult == redResult)
        {
            // Ничья - считается как проигрыш
            newState.Draws++;
            newState.ConsecutiveDraws++;
            newState.Losses++;  // Добавляем к проигрышам
            newState.ConsecutiveLosses++;  // Увеличиваем счетчик последовательных проигрышей
            newState.Balance -= CurrentBet;  // Вычитаем текущую ставку
            
            System.Diagnostics.Debug.WriteLine($"[MelBetGameState] ⚖️ Ничья {blueResult}:{redResult} - считается как проигрыш");
            
            // Применяем стратегию управления ставками
            newState.ApplyBettingStrategyOnLoss(settings);
            
            // Проверяем, нужно ли менять цвет
            if (settings.Strategy == BetStrategy.Ladder)
            {
                // Для Лесенки: сложная последовательность смены цвета
                // Правило: проигрыши 1,3,7,9,11... → смена цвета
                //          проигрыши 2,4,5,6,8,10... → остаемся
                // Исключение: проигрыши 4,5,6 всегда остаемся на одном цвете
                
                bool shouldSwitch = false;
                
                if (newState.ConsecutiveLosses == 4 || newState.ConsecutiveLosses == 5 || newState.ConsecutiveLosses == 6)
                {
                    // Проигрыши 4,5,6 - всегда остаемся
                    shouldSwitch = false;
                    System.Diagnostics.Debug.WriteLine($"[MelBetGameState] 🪜 Лесенка: проигрыш #{newState.ConsecutiveLosses} (зона 4-6) → остаёмся на {newState.CurrentColor}");
                }
                else if (newState.ConsecutiveLosses % 2 == 1)
                {
                    // Нечётные проигрыши (кроме зоны 4-6) → меняем
                    shouldSwitch = true;
                }
                
                if (shouldSwitch)
                {
                    var oldColor = newState.CurrentColor;
                    newState.CurrentColor = newState.CurrentColor == BetColor.Blue ? BetColor.Red : BetColor.Blue;
                    System.Diagnostics.Debug.WriteLine($"[MelBetGameState] 🪜 Лесенка: проигрыш #{newState.ConsecutiveLosses} (нечётный) → смена цвета {oldColor} → {newState.CurrentColor}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[MelBetGameState] 🪜 Лесенка: проигрыш #{newState.ConsecutiveLosses} (чётный) → остаёмся на {newState.CurrentColor}");
                }
            }
            else
            {
                // Для Мартингейла: используем настройку ColorSwitchAfterLosses
                System.Diagnostics.Debug.WriteLine($"[MelBetGameState] 🔢 Счётчик проигрышей: {newState.ConsecutiveLosses}, порог смены: {settings.ColorSwitchAfterLosses}");
                if (newState.ConsecutiveLosses >= settings.ColorSwitchAfterLosses)
                {
                    var oldColor = newState.CurrentColor;
                    newState.CurrentColor = newState.CurrentColor == BetColor.Blue ? BetColor.Red : BetColor.Blue;
                    newState.ConsecutiveLosses = 0;  // Сбрасываем счетчик после смены
                    System.Diagnostics.Debug.WriteLine($"[MelBetGameState] 🔄 Смена цвета {oldColor} → {newState.CurrentColor} после {settings.ColorSwitchAfterLosses} проигрышей");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[MelBetGameState] ⏳ Смены цвета нет, продолжаем на {newState.CurrentColor}");
                }
            }
            
            // Если была ставка "Не дубль" - также проиграли
            if (WasNoDoubleBetPlaced)
            {
                newState.Balance -= settings.NoDoubleBetAmount;
                newState.WasNoDoubleBetPlaced = false;
            }
        }
        else
        {
            // Есть победитель
            newState.ConsecutiveDraws = 0;
            newState.WasNoDoubleBetPlaced = false;
            
            bool blueWins = blueResult > redResult;
            bool playerWins = (blueWins && CurrentColor == BetColor.Blue) || 
                            (!blueWins && CurrentColor == BetColor.Red);
            
            if (playerWins)
            {
                // Победа
                newState.Wins++;
                newState.Balance += CurrentBet;
                newState.ConsecutiveLosses = 0;  // Сбрасываем счетчик проигрышей
                
                System.Diagnostics.Debug.WriteLine($"[MelBetGameState] ✅ ПОБЕДА! Текущая ставка: {CurrentBet}, сбрасываем к BaseBet: {settings.BaseBet}");
                
                // Сбрасываем ставку к базовой и лесенку
                newState.CurrentBet = settings.BaseBet;
                newState.LadderLevel = 0;
                newState.LadderStaysRemaining = 0;
                
                System.Diagnostics.Debug.WriteLine($"[MelBetGameState] 🔄 После победы: CurrentBet={newState.CurrentBet}, LadderLevel={newState.LadderLevel}");
            }
            else
            {
                // Поражение
                newState.Losses++;
                newState.ConsecutiveLosses++;  // Увеличиваем счетчик последовательных проигрышей
                newState.Balance -= CurrentBet;
                
                System.Diagnostics.Debug.WriteLine($"[MelBetGameState] ❌ Проигрыш: {(blueWins ? "синий" : "красный")} выиграл, а мы ставили на {CurrentColor}");
                
                // Применяем стратегию управления ставками
                newState.ApplyBettingStrategyOnLoss(settings);
                
                // Проверяем, нужно ли менять цвет
                if (settings.Strategy == BetStrategy.Ladder)
                {
                    // Для Лесенки: меняем цвет на нечётных проигрышах (1, 3, 5, 7...)
                    if (newState.ConsecutiveLosses % 2 == 1)
                    {
                        var oldColor = newState.CurrentColor;
                        newState.CurrentColor = newState.CurrentColor == BetColor.Blue ? BetColor.Red : BetColor.Blue;
                        System.Diagnostics.Debug.WriteLine($"[MelBetGameState] 🪜 Лесенка: проигрыш #{newState.ConsecutiveLosses} (нечётный) → смена цвета {oldColor} → {newState.CurrentColor}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[MelBetGameState] 🪜 Лесенка: проигрыш #{newState.ConsecutiveLosses} (чётный) → остаёмся на {newState.CurrentColor}");
                    }
                }
                else
                {
                    // Для Мартингейла: используем настройку ColorSwitchAfterLosses
                    System.Diagnostics.Debug.WriteLine($"[MelBetGameState] 🔢 Счётчик проигрышей: {newState.ConsecutiveLosses}, порог смены: {settings.ColorSwitchAfterLosses}");
                    if (newState.ConsecutiveLosses >= settings.ColorSwitchAfterLosses)
                    {
                        var oldColor = newState.CurrentColor;
                        newState.CurrentColor = newState.CurrentColor == BetColor.Blue ? BetColor.Red : BetColor.Blue;
                        newState.ConsecutiveLosses = 0;  // Сбрасываем счетчик после смены
                        System.Diagnostics.Debug.WriteLine($"[MelBetGameState] 🔄 Смена цвета {oldColor} → {newState.CurrentColor} после {settings.ColorSwitchAfterLosses} проигрышей");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[MelBetGameState] ⏳ Смены цвета нет, продолжаем на {newState.CurrentColor}");
                    }
                }
            }
        }
        
        return newState;
    }
    
    /// <summary>
    /// Получить отчет по статистике кубиков
    /// </summary>
    public string GetDiceStatisticsReport()
    {
        if (DiceStatistics.Count == 0)
            return "Нет данных";
        
        var sb = new StringBuilder();
        sb.AppendLine($"Всего раундов: {TotalRounds}");
        sb.AppendLine($"Ничьих: {Draws} ({(TotalRounds > 0 ? (Draws * 100.0 / TotalRounds):0):F1}%)");
        sb.AppendLine($"Побед: {Wins} ({(TotalRounds > 0 ? (Wins * 100.0 / TotalRounds):0):F1}%)");
        sb.AppendLine($"Поражений: {Losses} ({(TotalRounds > 0 ? (Losses * 100.0 / TotalRounds):0):F1}%)");
        sb.AppendLine($"Баланс: {Balance}");
        sb.AppendLine();
        sb.AppendLine("Распределение результатов кубиков:");
        
        var sorted = DiceStatistics.OrderByDescending(x => x.Value).Take(10);
        foreach (var kvp in sorted)
        {
            double percentage = TotalRounds > 0 ? (kvp.Value * 100.0 / TotalRounds) : 0;
            sb.AppendLine($"  {kvp.Key}: {kvp.Value} раз ({percentage:F1}%)");
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Получить детальную статистику по каждому кубику за разные периоды
    /// </summary>
    public string GetDetailedDiceStatistics()
    {
        if (RollsHistory.Count == 0)
            return "📊 Нет данных о бросках";
        
        var sb = new StringBuilder();
        
        var periods = new[]
        {
            ("Все броски", RollsHistory.Count),
            ("Последние 50", Math.Min(50, RollsHistory.Count)),
            ("Последние 25", Math.Min(25, RollsHistory.Count)),
            ("Последние 10", Math.Min(10, RollsHistory.Count))
        };
        
        foreach (var (periodName, count) in periods)
        {
            var rolls = RollsHistory.Skip(Math.Max(0, RollsHistory.Count - count)).ToList();
            
            // Подсчитываем статистику по каждому кубику
            var redStats = new int[7]; // индексы 1-6
            var blueStats = new int[7];
            
            foreach (var (blue, red) in rolls)
            {
                if (blue >= 1 && blue <= 6) blueStats[blue]++;
                if (red >= 1 && red <= 6) redStats[red]++;
            }
            
            sb.AppendLine();
            sb.AppendLine($"━━━ {periodName} ({count} бросков) ━━━");
            
            // Красный кубик
            sb.Append("🔴 Красный: ");
            var redParts = new List<string>();
            for (int i = 1; i <= 6; i++)
            {
                redParts.Add($"{i}-{redStats[i]} раз");
            }
            sb.AppendLine(string.Join(", ", redParts));
            
            // Синий кубик
            sb.Append("🔵 Синий: ");
            var blueParts = new List<string>();
            for (int i = 1; i <= 6; i++)
            {
                blueParts.Add($"{i}-{blueStats[i]} раз");
            }
            sb.AppendLine(string.Join(", ", blueParts));
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Сброс состояния игры
    /// </summary>
    public void Reset(MelBetSettings settings)
    {
        IsGameActive = false;
        IsPaused = false;
        GameStartTime = 0;
        CurrentBet = settings.BaseBet;
        CurrentColor = settings.PreferredColor;
        ConsecutiveDraws = 0;
        ConsecutiveLosses = 0;
        LadderLevel = 0;
        LadderStaysRemaining = 0;
        WasNoDoubleBetPlaced = false;
        TotalRounds = 0;
        Wins = 0;
        Losses = 0;
        Draws = 0;
        Balance = 0;
        DiceStatistics.Clear();
        RollsHistory.Clear();
        LastBlueValue = 0;
        LastRedValue = 0;
    }
    
    /// <summary>
    /// Клонирование состояния
    /// </summary>
    public MelBetGameState Clone()
    {
        return new MelBetGameState
        {
            IsGameActive = this.IsGameActive,
            IsPaused = this.IsPaused,
            GameStartTime = this.GameStartTime,
            CurrentBet = this.CurrentBet,
            CurrentColor = this.CurrentColor,
            ConsecutiveDraws = this.ConsecutiveDraws,
            ConsecutiveLosses = this.ConsecutiveLosses,
            LadderLevel = this.LadderLevel,
            LadderStaysRemaining = this.LadderStaysRemaining,
            WasNoDoubleBetPlaced = this.WasNoDoubleBetPlaced,
            TotalRounds = this.TotalRounds,
            Wins = this.Wins,
            Losses = this.Losses,
            Draws = this.Draws,
            Balance = this.Balance,
            DiceStatistics = new Dictionary<string, int>(this.DiceStatistics),
            RollsHistory = new List<(int Blue, int Red)>(this.RollsHistory),
            LastBlueValue = this.LastBlueValue,
            LastRedValue = this.LastRedValue
        };
    }
}
