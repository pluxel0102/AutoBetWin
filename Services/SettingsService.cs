using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Text.Json;

namespace AutoBet.Services;

public static class SettingsService
{
    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AutoBet",
        "settings.json"
    );

    private class SettingsData
    {
        public string Theme { get; set; } = "Dark";
        public string ApiKey { get; set; } = "";
        public string RecognitionModel { get; set; } = "openai/gpt-5-chat";
        public string AnalysisModel { get; set; } = "deepseek/deepseek-v3.2-exp";
        public ROIRegion[]? MelBetROI { get; set; }
        public ProxyData? Proxy { get; set; }
        public int MelBetBaseBet { get; set; } = 10;
        public string MelBetPreferredColor { get; set; } = "Blue";
        public int MelBetColorSwitchAfterLosses { get; set; } = 2;
        public string MelBetStrategy { get; set; } = "Martingale";
        public bool EnableNoDoubleBet { get; set; } = true;
    }
    
    private class ProxyData
    {
        public bool Enabled { get; set; }
        public string Host { get; set; } = "";
        public int Port { get; set; } = 0;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string Type { get; set; } = "Http";
    }

    public class ROIRegion
    {
        public string Name { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    private static SettingsData LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<SettingsData>(json);
                System.Diagnostics.Debug.WriteLine($"[SettingsService] Настройки загружены из: {SettingsFilePath}");
                return settings ?? new SettingsData();
            }
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Файл настроек не найден: {SettingsFilePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка загрузки настроек: {ex.Message}");
        }
        return new SettingsData();
    }

    private static void SaveSettings(SettingsData settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory!);
            }

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Настройки сохранены в: {SettingsFilePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка сохранения настроек: {ex.Message}");
        }
    }

    /// <summary>
    /// Сохраняет тему приложения
    /// </summary>
    public static void SaveTheme(ElementTheme theme)
    {
        try
        {
            var settings = LoadSettings();
            settings.Theme = theme.ToString();
            SaveSettings(settings);
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Тема сохранена: {theme}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка сохранения темы: {ex.Message}");
        }
    }

    /// <summary>
    /// Загружает сохранённую тему приложения
    /// </summary>
    public static ElementTheme LoadTheme()
    {
        try
        {
            var settings = LoadSettings();
            if (Enum.TryParse<ElementTheme>(settings.Theme, out var theme))
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsService] Тема загружена: {theme}");
                return theme;
            }
            System.Diagnostics.Debug.WriteLine("[SettingsService] Тема не найдена, используется Light");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка загрузки темы: {ex.Message}");
        }

        // По умолчанию светлая тема
        return ElementTheme.Light;
    }

    /// <summary>
    /// Проверяет, является ли тема тёмной
    /// </summary>
    public static bool IsDarkTheme()
    {
        return LoadTheme() == ElementTheme.Dark;
    }

    // OpenRouter API методы

    /// <summary>
    /// Сохраняет API ключ OpenRouter
    /// </summary>
    public static void SaveApiKey(string apiKey)
    {
        try
        {
            var settings = LoadSettings();
            settings.ApiKey = apiKey ?? "";
            SaveSettings(settings);
            System.Diagnostics.Debug.WriteLine($"[SettingsService] API ключ сохранён: {apiKey?.Substring(0, Math.Min(10, apiKey?.Length ?? 0))}...");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка сохранения API ключа: {ex.Message}");
        }
    }

    /// <summary>
    /// Загружает API ключ OpenRouter
    /// </summary>
    public static string LoadApiKey()
    {
        try
        {
            var settings = LoadSettings();
            return settings.ApiKey;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка загрузки API ключа: {ex.Message}");
        }
        return string.Empty;
    }

    /// <summary>
    /// Сохраняет модель распознавания
    /// </summary>
    public static void SaveRecognitionModel(string modelId)
    {
        try
        {
            var settings = LoadSettings();
            settings.RecognitionModel = modelId ?? "openai/gpt-5-chat";
            SaveSettings(settings);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка сохранения модели распознавания: {ex.Message}");
        }
    }

    /// <summary>
    /// Загружает модель распознавания
    /// </summary>
    public static string LoadRecognitionModel()
    {
        try
        {
            var settings = LoadSettings();
            return settings.RecognitionModel;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка загрузки модели распознавания: {ex.Message}");
        }
        return "openai/gpt-5-chat"; // По умолчанию GPT-5
    }

    /// <summary>
    /// Сохраняет модель анализа
    /// </summary>
    public static void SaveAnalysisModel(string modelId)
    {
        try
        {
            var settings = LoadSettings();
            settings.AnalysisModel = modelId ?? "deepseek/deepseek-v3.2-exp";
            SaveSettings(settings);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка сохранения модели анализа: {ex.Message}");
        }
    }

    /// <summary>
    /// Загружает модель анализа
    /// </summary>
    public static string LoadAnalysisModel()
    {
        try
        {
            var settings = LoadSettings();
            return settings.AnalysisModel;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка загрузки модели анализа: {ex.Message}");
        }
        return "deepseek/deepseek-v3.2-exp"; // По умолчанию DeepSeek
    }

    /// <summary>
    /// Сохраняет конфигурацию ROI для MelBet
    /// </summary>
    public static void SaveMelBetROI(ROIRegion[] regions)
    {
        try
        {
            var settings = LoadSettings();
            settings.MelBetROI = regions;
            SaveSettings(settings);
            System.Diagnostics.Debug.WriteLine($"[SettingsService] MelBet ROI сохранены: {regions.Length} областей");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка сохранения MelBet ROI: {ex.Message}");
        }
    }

    /// <summary>
    /// Загружает конфигурацию ROI для MelBet
    /// </summary>
    public static ROIRegion[]? LoadMelBetROI()
    {
        try
        {
            var settings = LoadSettings();
            if (settings.MelBetROI != null && settings.MelBetROI.Length > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsService] MelBet ROI загружены: {settings.MelBetROI.Length} областей");
                return settings.MelBetROI;
            }
            System.Diagnostics.Debug.WriteLine("[SettingsService] MelBet ROI не найдены");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка загрузки MelBet ROI: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Удаляет все сохранённые ROI области для MelBet
    /// </summary>
    public static void DeleteMelBetROI()
    {
        try
        {
            var settings = LoadSettings();
            settings.MelBetROI = Array.Empty<ROIRegion>();
            SaveSettings(settings);
            System.Diagnostics.Debug.WriteLine("[SettingsService] MelBet ROI удалены");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка удаления MelBet ROI: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Сохраняет настройки прокси
    /// </summary>
    public static void SaveProxySettings(Models.ProxySettings proxySettings)
    {
        try
        {
            var settings = LoadSettings();
            settings.Proxy = new ProxyData
            {
                Enabled = proxySettings.Enabled,
                Host = proxySettings.Host,
                Port = proxySettings.Port,
                Username = proxySettings.Username,
                Password = proxySettings.Password,
                Type = proxySettings.Type.ToString()
            };
            SaveSettings(settings);
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Прокси настройки сохранены: {proxySettings.Host}:{proxySettings.Port}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка сохранения прокси: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Загружает настройки прокси
    /// </summary>
    public static Models.ProxySettings LoadProxySettings()
    {
        try
        {
            var settings = LoadSettings();
            if (settings.Proxy != null)
            {
                var proxySettings = new Models.ProxySettings
                {
                    Enabled = settings.Proxy.Enabled,
                    Host = settings.Proxy.Host,
                    Port = settings.Proxy.Port,
                    Username = settings.Proxy.Username,
                    Password = settings.Proxy.Password
                };
                
                if (Enum.TryParse<Models.ProxyType>(settings.Proxy.Type, out var proxyType))
                {
                    proxySettings.Type = proxyType;
                }
                
                System.Diagnostics.Debug.WriteLine($"[SettingsService] Прокси загружены: {proxySettings.Host}:{proxySettings.Port}");
                return proxySettings;
            }
            System.Diagnostics.Debug.WriteLine("[SettingsService] Прокси не настроены");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка загрузки прокси: {ex.Message}");
        }
        return new Models.ProxySettings();
    }

    /// <summary>
    /// Сохраняет начальную ставку для МелБет
    /// </summary>
    public static void SaveMelBetBaseBet(int baseBet)
    {
        try
        {
            var settings = LoadSettings();
            settings.MelBetBaseBet = baseBet;
            SaveSettings(settings);
            System.Diagnostics.Debug.WriteLine($"[SettingsService] МелБет начальная ставка сохранена: {baseBet}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка сохранения начальной ставки МелБет: {ex.Message}");
        }
    }

    /// <summary>
    /// Загружает начальную ставку для МелБет (по умолчанию 10)
    /// </summary>
    public static int LoadMelBetBaseBet()
    {
        try
        {
            var settings = LoadSettings();
            if (settings.MelBetBaseBet > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsService] МелБет начальная ставка загружена: {settings.MelBetBaseBet}");
                return settings.MelBetBaseBet;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка загрузки начальной ставки МелБет: {ex.Message}");
        }
        return 10; // По умолчанию 10
    }

    /// <summary>
    /// Сохраняет предпочитаемый цвет для МелБет
    /// </summary>
    public static void SaveMelBetPreferredColor(string color)
    {
        try
        {
            var settings = LoadSettings();
            settings.MelBetPreferredColor = color;
            SaveSettings(settings);
            System.Diagnostics.Debug.WriteLine($"[SettingsService] МелБет предпочитаемый цвет сохранён: {color}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка сохранения предпочитаемого цвета МелБет: {ex.Message}");
        }
    }

    /// <summary>
    /// Загружает предпочитаемый цвет для МелБет (по умолчанию "Blue")
    /// </summary>
    public static string LoadMelBetPreferredColor()
    {
        try
        {
            var settings = LoadSettings();
            if (!string.IsNullOrEmpty(settings.MelBetPreferredColor))
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsService] МелБет предпочитаемый цвет загружен: {settings.MelBetPreferredColor}");
                return settings.MelBetPreferredColor;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка загрузки предпочитаемого цвета МелБет: {ex.Message}");
        }
        return "Blue"; // По умолчанию синий
    }

    /// <summary>
    /// Сохраняет настройку смены цвета после проигрышей для МелБет
    /// </summary>
    public static void SaveMelBetColorSwitchAfterLosses(int lossesCount)
    {
        try
        {
            var settings = LoadSettings();
            settings.MelBetColorSwitchAfterLosses = lossesCount;
            SaveSettings(settings);
            System.Diagnostics.Debug.WriteLine($"[SettingsService] МелБет смена цвета после проигрышей сохранена: {lossesCount}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка сохранения настройки смены цвета МелБет: {ex.Message}");
        }
    }

    /// <summary>
    /// Загружает настройку смены цвета после проигрышей для МелБет
    /// </summary>
    public static int LoadMelBetColorSwitchAfterLosses()
    {
        try
        {
            var settings = LoadSettings();
            System.Diagnostics.Debug.WriteLine($"[SettingsService] МелБет смена цвета после проигрышей загружена: {settings.MelBetColorSwitchAfterLosses}");
            return settings.MelBetColorSwitchAfterLosses;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка загрузки настройки смены цвета МелБет: {ex.Message}");
        }
        return 2; // По умолчанию после 2 проигрышей
    }

    /// <summary>
    /// Сохраняет стратегию управления ставками для МелБет
    /// </summary>
    public static void SaveMelBetStrategy(Models.BetStrategy strategy)
    {
        try
        {
            var settings = LoadSettings();
            settings.MelBetStrategy = strategy.ToString();
            SaveSettings(settings);
            System.Diagnostics.Debug.WriteLine($"[SettingsService] МелБет стратегия сохранена: {strategy}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка сохранения стратегии МелБет: {ex.Message}");
        }
    }

    /// <summary>
    /// Загружает стратегию управления ставками для МелБет
    /// </summary>
    public static Models.BetStrategy LoadMelBetStrategy()
    {
        try
        {
            var settings = LoadSettings();
            if (!string.IsNullOrEmpty(settings.MelBetStrategy))
            {
                if (Enum.TryParse<Models.BetStrategy>(settings.MelBetStrategy, out var strategy))
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsService] МелБет стратегия загружена: {strategy}");
                    return strategy;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка загрузки стратегии МелБет: {ex.Message}");
        }
        return Models.BetStrategy.Martingale; // По умолчанию Мартингейл
    }

    /// <summary>
    /// Сохраняет настройку включения ставки "Не дубль"
    /// </summary>
    public static void SaveEnableNoDoubleBet(bool enabled)
    {
        try
        {
            var settings = LoadSettings();
            settings.EnableNoDoubleBet = enabled;
            SaveSettings(settings);
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ставка 'Не дубль' сохранена: {enabled}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка сохранения настройки 'Не дубль': {ex.Message}");
        }
    }

    /// <summary>
    /// Загружает настройку включения ставки "Не дубль"
    /// </summary>
    public static bool LoadEnableNoDoubleBet()
    {
        try
        {
            var settings = LoadSettings();
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ставка 'Не дубль' загружена: {settings.EnableNoDoubleBet}");
            return settings.EnableNoDoubleBet;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Ошибка загрузки настройки 'Не дубль': {ex.Message}");
        }
        return true; // По умолчанию включено
    }
}
