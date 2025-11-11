using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.IO;
using AutoBet.Models;

namespace AutoBet.Services;

/// <summary>
/// Сервис проверки и установки обновлений через GitHub Releases API
/// </summary>
public static class UpdateService
{
    private const string GITHUB_REPO_OWNER = "pluxel0102";
    private const string GITHUB_REPO_NAME = "AutoBetWin";
    private const string CURRENT_VERSION = "1.0.2"; // Текущая версия приложения
    
    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    
    static UpdateService()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "AutoBet-Windows-App");
    }
    
    /// <summary>
    /// Получить текущую версию приложения
    /// </summary>
    public static string GetCurrentVersion() => CURRENT_VERSION;
    
    /// <summary>
    /// Проверить наличие обновлений на GitHub
    /// </summary>
    public static async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        try
        {
            var url = $"https://api.github.com/repos/{GITHUB_REPO_OWNER}/{GITHUB_REPO_NAME}/releases/latest";
            
            var response = await _httpClient.GetAsync(url);
            
            // Если релизов ещё нет (404) - это нормально
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Debug.WriteLine("[UpdateService] На GitHub пока нет релизов");
                return new UpdateInfo
                {
                    Version = CURRENT_VERSION,
                    IsUpdateAvailable = false
                };
            }
            
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[UpdateService] GitHub API вернул код {response.StatusCode}");
                return null;
            }
            
            var json = await response.Content.ReadAsStringAsync();
            var release = JsonSerializer.Deserialize<GitHubRelease>(json);
            
            if (release == null)
            {
                Debug.WriteLine("[UpdateService] Не удалось распарсить ответ GitHub API");
                return null;
            }
            
            // Парсим версию из тега (например "v1.2.0" -> "1.2.0")
            var latestVersion = release.tag_name?.TrimStart('v') ?? "0.0.0";
            var currentVersion = CURRENT_VERSION;
            
            // Сравниваем версии
            var isNewer = CompareVersions(latestVersion, currentVersion) > 0;
            
            if (!isNewer)
            {
                Debug.WriteLine($"[UpdateService] Обновлений нет. Текущая: {currentVersion}, Последняя: {latestVersion}");
                return new UpdateInfo
                {
                    Version = currentVersion,
                    IsUpdateAvailable = false
                };
            }
            
            // Ищем .exe файл в assets
            var asset = release.assets?.FirstOrDefault(a => 
                a.name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true);
            
            Debug.WriteLine($"[UpdateService] Найдено обновление: {latestVersion}");
            
            return new UpdateInfo
            {
                Version = latestVersion,
                Description = release.body ?? "Нет описания",
                DownloadUrl = asset?.browser_download_url ?? release.html_url ?? "",
                ReleaseDate = release.published_at,
                FileSize = asset?.size ?? 0,
                IsUpdateAvailable = true
            };
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[UpdateService] Ошибка сети: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateService] Ошибка проверки обновлений: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Сравнить две версии (формат: "1.2.3")
    /// </summary>
    /// <returns>-1 если v1 < v2, 0 если равны, 1 если v1 > v2</returns>
    private static int CompareVersions(string v1, string v2)
    {
        try
        {
            var parts1 = v1.Split('.').Select(int.Parse).ToArray();
            var parts2 = v2.Split('.').Select(int.Parse).ToArray();
            
            for (int i = 0; i < Math.Max(parts1.Length, parts2.Length); i++)
            {
                int p1 = i < parts1.Length ? parts1[i] : 0;
                int p2 = i < parts2.Length ? parts2[i] : 0;
                
                if (p1 < p2) return -1;
                if (p1 > p2) return 1;
            }
            
            return 0;
        }
        catch
        {
            return 0;
        }
    }
    
    /// <summary>
    /// Открыть страницу скачивания в браузере
    /// </summary>
    public static void OpenDownloadPage(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateService] Ошибка открытия браузера: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Скачать и установить обновление
    /// </summary>
    /// <param name="downloadUrl">URL для скачивания Setup.exe</param>
    /// <param name="progress">Прогресс скачивания (0-100)</param>
    public static async Task<(bool Success, string ErrorMessage)> DownloadAndInstallUpdateAsync(
        string downloadUrl, 
        IProgress<int>? progress = null)
    {
        try
        {
            Debug.WriteLine($"[UpdateService] Начало скачивания: {downloadUrl}");
            
            // Путь для временного файла
            var tempPath = Path.Combine(Path.GetTempPath(), "AutoBet_Update_Setup.exe");
            
            // Удаляем старый файл если есть
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
            
            // Скачиваем файл с прогрессом
            using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                if (!response.IsSuccessStatusCode)
                {
                    return (false, $"Ошибка скачивания: HTTP {response.StatusCode}");
                }
                
                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var downloadedBytes = 0L;
                
                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    int bytesRead;
                    
                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        downloadedBytes += bytesRead;
                        
                        if (totalBytes > 0 && progress != null)
                        {
                            var percentage = (int)((downloadedBytes * 100) / totalBytes);
                            progress.Report(percentage);
                        }
                    }
                }
            }
            
            Debug.WriteLine($"[UpdateService] Файл скачан: {tempPath}");
            progress?.Report(100);
            
            // Проверяем что файл скачался
            if (!File.Exists(tempPath))
            {
                return (false, "Файл не найден после скачивания");
            }
            
            // Запускаем Setup.exe и закрываем текущее приложение
            var startInfo = new ProcessStartInfo
            {
                FileName = tempPath,
                UseShellExecute = true,
                Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS" // Тихая установка Inno Setup
            };
            
            Debug.WriteLine("[UpdateService] Запуск установщика...");
            Process.Start(startInfo);
            
            // Даём секунду на запуск установщика
            await Task.Delay(1000);
            
            // Завершаем текущее приложение
            Debug.WriteLine("[UpdateService] Завершение приложения для установки обновления");
            Environment.Exit(0);
            
            return (true, string.Empty);
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[UpdateService] Ошибка сети: {ex.Message}");
            return (false, $"Ошибка сети: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateService] Ошибка установки: {ex.Message}");
            return (false, $"Ошибка: {ex.Message}");
        }
    }
    
    // Модели для десериализации GitHub API
    private class GitHubRelease
    {
        public string? tag_name { get; set; }
        public string? name { get; set; }
        public string? body { get; set; }
        public string? html_url { get; set; }
        public DateTime published_at { get; set; }
        public GitHubAsset[]? assets { get; set; }
    }
    
    private class GitHubAsset
    {
        public string? name { get; set; }
        public string? browser_download_url { get; set; }
        public long size { get; set; }
    }
}
