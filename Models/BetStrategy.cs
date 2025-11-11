namespace AutoBet.Models;

/// <summary>
/// Стратегии управления ставками в игре MelBet
/// </summary>
public enum BetStrategy
{
    /// <summary>
    /// Классический Мартингейл - удвоение ставки после каждого проигрыша
    /// </summary>
    Martingale,
    
    /// <summary>
    /// Лесенка - прогрессия с задержками на определенных уровнях
    /// </summary>
    Ladder
}
