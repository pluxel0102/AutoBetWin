using System.Text.Json.Serialization;

namespace AutoBet.Models;

/// <summary>
/// Настройки прокси-сервера
/// </summary>
public class ProxySettings
{
    /// <summary>
    /// Включить использование прокси
    /// </summary>
    public bool Enabled { get; set; } = false;
    
    /// <summary>
    /// IP адрес или хост прокси-сервера
    /// </summary>
    public string Host { get; set; } = string.Empty;
    
    /// <summary>
    /// Порт прокси-сервера
    /// </summary>
    public int Port { get; set; } = 0;
    
    /// <summary>
    /// Логин для авторизации
    /// </summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// Пароль для авторизации
    /// </summary>
    public string Password { get; set; } = string.Empty;
    
    /// <summary>
    /// Тип прокси
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProxyType Type { get; set; } = ProxyType.Http;
    
    /// <summary>
    /// Валидация настроек прокси
    /// </summary>
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (!Enabled)
            return (true, null);
            
        if (string.IsNullOrWhiteSpace(Host))
            return (false, "IP адрес/хост прокси не указан");
            
        if (Port <= 0 || Port > 65535)
            return (false, "Порт должен быть от 1 до 65535");
            
        return (true, null);
    }
}

/// <summary>
/// Тип прокси-сервера
/// </summary>
public enum ProxyType
{
    /// <summary>
    /// HTTP/HTTPS прокси
    /// </summary>
    Http,
    
    /// <summary>
    /// SOCKS5 прокси
    /// </summary>
    Socks5
}
