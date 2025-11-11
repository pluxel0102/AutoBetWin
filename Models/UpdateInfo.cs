using System;

namespace AutoBet.Models;

/// <summary>
/// Информация об обновлении
/// </summary>
public class UpdateInfo
{
    /// <summary>
    /// Версия обновления (например "1.2.0")
    /// </summary>
    public string Version { get; set; } = string.Empty;
    
    /// <summary>
    /// Описание изменений (changelog)
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Ссылка на скачивание
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Дата выпуска
    /// </summary>
    public DateTime ReleaseDate { get; set; }
    
    /// <summary>
    /// Размер файла в байтах
    /// </summary>
    public long FileSize { get; set; }
    
    /// <summary>
    /// Доступно ли обновление
    /// </summary>
    public bool IsUpdateAvailable { get; set; }
}
