using System;
using System.Text.Json.Serialization;

namespace AutoBet.Models;

/// <summary>
/// Настройки игры MelBet
/// </summary>
public class MelBetSettings
{
    /// <summary>
    /// Базовая ставка (начальная)
    /// </summary>
    public int BaseBet { get; set; }
    
    /// <summary>
    /// Предпочитаемый цвет для ставок
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public BetColor PreferredColor { get; set; } = BetColor.Blue;
    
    /// <summary>
    /// Стратегия управления ставками
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public BetStrategy Strategy { get; set; } = BetStrategy.Martingale;
    
    /// <summary>
    /// Тестовый режим (симуляция без реальных кликов)
    /// </summary>
    public bool EnableTestMode { get; set; } = false;
    
    /// <summary>
    /// Размещать ставку "Не дубль" после 4 ничьих подряд
    /// </summary>
    public bool EnableNoDoubleBet { get; set; } = true;
    
    /// <summary>
    /// Сумма ставки "Не дубль"
    /// </summary>
    public int NoDoubleBetAmount { get; set; } = 200000;

    /// <summary>
    /// Количество проигрышей подряд, после которых нужно менять цвет (1-5)
    /// </summary>
    public int ColorSwitchAfterLosses { get; set; } = 2;

    public MelBetSettings()
    {
        // Загружаем сохранённую начальную ставку или используем 10 по умолчанию
        BaseBet = Services.SettingsService.LoadMelBetBaseBet();
        
        // Загружаем сохранённый предпочитаемый цвет
        var savedColor = Services.SettingsService.LoadMelBetPreferredColor();
        PreferredColor = savedColor == "Red" ? BetColor.Red : BetColor.Blue;

        // Загружаем настройку смены цвета
        ColorSwitchAfterLosses = Services.SettingsService.LoadMelBetColorSwitchAfterLosses();
        
        // Загружаем стратегию
        Strategy = Services.SettingsService.LoadMelBetStrategy();
    }
    
    /// <summary>
    /// Валидация настроек
    /// </summary>
    /// <returns>(isValid, errorMessage)</returns>
    public (bool IsValid, string ErrorMessage) Validate()
    {
        if (BaseBet < 10)
            return (false, "Базовая ставка должна быть не менее 10");
        
        if (NoDoubleBetAmount < 10)
            return (false, "Ставка 'Не дубль' должна быть не менее 10");
        
        return (true, string.Empty);
    }
    
    /// <summary>
    /// Клонирование настроек
    /// </summary>
    public MelBetSettings Clone()
    {
        return new MelBetSettings
        {
            BaseBet = this.BaseBet,
            PreferredColor = this.PreferredColor,
            Strategy = this.Strategy,
            EnableTestMode = this.EnableTestMode,
            EnableNoDoubleBet = this.EnableNoDoubleBet,
            NoDoubleBetAmount = this.NoDoubleBetAmount,
            ColorSwitchAfterLosses = this.ColorSwitchAfterLosses
        };
    }
}