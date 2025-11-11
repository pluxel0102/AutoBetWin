using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AutoBet.Models;

namespace AutoBet.Services;

public static class OpenRouterService
{
    private static HttpClient? _httpClient;
    private static ProxySettings? _currentProxySettings;
    private const string ApiBaseUrl = "https://openrouter.ai/api/v1";
    
    /// <summary>
    /// Получает или создает HttpClient с текущими настройками прокси
    /// </summary>
    private static HttpClient GetHttpClient()
    {
        var proxySettings = SettingsService.LoadProxySettings();
        
        // Если настройки прокси изменились, пересоздаем клиент
        if (_httpClient == null || !ProxySettingsEqual(_currentProxySettings, proxySettings))
        {
            _httpClient?.Dispose();
            _httpClient = CreateHttpClient(proxySettings);
            _currentProxySettings = proxySettings;
        }
        
        return _httpClient;
    }
    
    /// <summary>
    /// Создает HttpClient с настройками прокси
    /// </summary>
    private static HttpClient CreateHttpClient(ProxySettings proxySettings)
    {
        var handler = new HttpClientHandler();
        
        if (proxySettings.Enabled && !string.IsNullOrWhiteSpace(proxySettings.Host))
        {
            // Определяем схему прокси в зависимости от типа
            string proxyScheme = proxySettings.Type == ProxyType.Socks5 ? "socks5" : "http";
            var proxyUri = new Uri($"{proxyScheme}://{proxySettings.Host}:{proxySettings.Port}");
            handler.Proxy = new WebProxy(proxyUri);
            
            // Если есть логин/пароль
            if (!string.IsNullOrWhiteSpace(proxySettings.Username))
            {
                handler.Proxy.Credentials = new NetworkCredential(
                    proxySettings.Username,
                    proxySettings.Password
                );
            }
            
            handler.UseProxy = true;
            System.Diagnostics.Debug.WriteLine($"[OpenRouter] Используется прокси: {proxyScheme}://{proxySettings.Host}:{proxySettings.Port}");
        }
        else
        {
            handler.UseProxy = false;
        }
        
        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
    }
    
    /// <summary>
    /// Сравнивает настройки прокси
    /// </summary>
    private static bool ProxySettingsEqual(ProxySettings? a, ProxySettings? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        
        return a.Enabled == b.Enabled &&
               a.Host == b.Host &&
               a.Port == b.Port &&
               a.Username == b.Username &&
               a.Password == b.Password &&
               a.Type == b.Type;
    }

    public class ApiTestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Тестирует API ключ OpenRouter с выбранными моделями
    /// </summary>
    public static async Task<ApiTestResult> TestApiKey(string apiKey, string recognitionModel, string analysisModel)
    {
        try
        {
            // Используем модель распознавания для теста (она обычно быстрее)
            string testModel = !string.IsNullOrWhiteSpace(recognitionModel) 
                ? recognitionModel 
                : "openai/gpt-5-chat";

            // Формируем запрос согласно документации OpenRouter
            var requestBody = new
            {
                model = testModel,
                messages = new[]
                {
                    new { role = "user", content = "Hello" }
                },
                max_tokens = 50  // Минимум 16 токенов требуется
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Создаём запрос
            var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/chat/completions");
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Headers.Add("HTTP-Referer", "https://autobet.app"); // Опционально: ваш сайт
            request.Headers.Add("X-Title", "AutoBet"); // Опционально: название приложения
            request.Content = content;

            // Отправляем запрос
            var httpClient = GetHttpClient();
            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // Парсим успешный ответ
                using var doc = JsonDocument.Parse(responseContent);
                var root = doc.RootElement;

                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    return new ApiTestResult
                    {
                        Success = true,
                        Message = $"API ключ работает корректно!\n\n" +
                                 $"Модель распознавания: {GetModelDisplayName(recognitionModel)}\n" +
                                 $"Модель анализа: {GetModelDisplayName(analysisModel)}\n\n" +
                                 $"Все настройки сохранены."
                    };
                }
                else
                {
                    return new ApiTestResult
                    {
                        Success = false,
                        Message = "API вернул некорректный ответ."
                    };
                }
            }
            else
            {
                // Обработка ошибок с детальным парсингом
                string errorMessage = "Неизвестная ошибка";
                string errorCode = "";
                string errorType = "";

                try
                {
                    using var doc = JsonDocument.Parse(responseContent);
                    if (doc.RootElement.TryGetProperty("error", out var error))
                    {
                        // Получаем сообщение об ошибке
                        if (error.TryGetProperty("message", out var message))
                        {
                            errorMessage = message.GetString() ?? errorMessage;
                        }
                        
                        // Получаем код ошибки
                        if (error.TryGetProperty("code", out var code))
                        {
                            errorCode = code.GetString() ?? "";
                        }
                        
                        // Получаем тип ошибки
                        if (error.TryGetProperty("type", out var type))
                        {
                            errorType = type.GetString() ?? "";
                        }
                    }
                }
                catch
                {
                    // Если не удалось распарсить JSON, используем сырой ответ
                    errorMessage = responseContent.Length > 500 
                        ? responseContent.Substring(0, 500) + "..." 
                        : responseContent;
                }

                // Формируем детальное сообщение об ошибке
                var detailedMessage = new StringBuilder();
                detailedMessage.AppendLine($"HTTP Status: {(int)response.StatusCode} ({response.StatusCode})");
                
                if (!string.IsNullOrWhiteSpace(errorType))
                {
                    detailedMessage.AppendLine($"Тип ошибки: {errorType}");
                }
                
                if (!string.IsNullOrWhiteSpace(errorCode))
                {
                    detailedMessage.AppendLine($"Код ошибки: {errorCode}");
                }
                
                detailedMessage.AppendLine($"\nСообщение:");
                detailedMessage.AppendLine(errorMessage);
                
                // Добавляем подсказки для частых ошибок
                if (errorMessage.Contains("invalid") && errorMessage.Contains("key", StringComparison.OrdinalIgnoreCase))
                {
                    detailedMessage.AppendLine("\n💡 Проверьте корректность API ключа на https://openrouter.ai/keys");
                }
                else if (errorMessage.Contains("insufficient", StringComparison.OrdinalIgnoreCase) || 
                         errorMessage.Contains("credits", StringComparison.OrdinalIgnoreCase))
                {
                    detailedMessage.AppendLine("\n💡 Недостаточно кредитов. Пополните баланс на https://openrouter.ai/credits");
                }
                else if (errorMessage.Contains("model", StringComparison.OrdinalIgnoreCase))
                {
                    detailedMessage.AppendLine("\n💡 Возможно, модель недоступна или указан неверный ID модели");
                }

                return new ApiTestResult
                {
                    Success = false,
                    Message = detailedMessage.ToString()
                };
            }
        }
        catch (HttpRequestException ex)
        {
            return new ApiTestResult
            {
                Success = false,
                Message = $"Ошибка сети:\n{ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new ApiTestResult
            {
                Success = false,
                Message = $"Произошла ошибка:\n{ex.Message}"
            };
        }
    }

    private static string GetModelDisplayName(string modelId)
    {
        return modelId switch
        {
            "openai/gpt-5-chat" => "ChatGPT 5",
            "google/gemini-2.5-flash-lite-preview-09-2025" => "Gemini 2.5 Flash Lite Preview",
            "deepseek/deepseek-v3.2-exp" => "DeepSeek V3.2 Exp",
            "anthropic/claude-opus-4.1" => "Claude Opus 4.1",
            _ => modelId
        };
    }

    /// <summary>
    /// Результат анализа изображения
    /// </summary>
    public class ImageAnalysisResult
    {
        public bool Success { get; set; }
        public string Response { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Тестирует прокси-сервер
    /// </summary>
    public static async Task<ApiTestResult> TestProxy(ProxySettings proxySettings)
    {
        try
        {
            // Создаём временный HttpClient с прокси
            var handler = new HttpClientHandler();
            
            if (!string.IsNullOrWhiteSpace(proxySettings.Host) && proxySettings.Port > 0)
            {
                // Определяем схему прокси в зависимости от типа
                string proxyScheme = proxySettings.Type == ProxyType.Socks5 ? "socks5" : "http";
                var proxyUri = new Uri($"{proxyScheme}://{proxySettings.Host}:{proxySettings.Port}");
                handler.Proxy = new WebProxy(proxyUri);
                
                // Если есть логин/пароль
                if (!string.IsNullOrWhiteSpace(proxySettings.Username))
                {
                    handler.Proxy.Credentials = new NetworkCredential(
                        proxySettings.Username,
                        proxySettings.Password
                    );
                }
                
                handler.UseProxy = true;
            }
            else
            {
                return new ApiTestResult
                {
                    Success = false,
                    Message = "❌ Некорректные настройки прокси.\n\nУкажите IP адрес и порт."
                };
            }
            
            using var testClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            
            // Проверяем доступность через прокси
            // Используем быстрый HTTP endpoint для теста
            var testUrl = "https://api.ipify.org?format=json"; // Возвращает внешний IP
            
            var response = await testClient.GetAsync(testUrl);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                
                // Парсим IP из ответа
                string externalIp = "неизвестно";
                try
                {
                    using var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.TryGetProperty("ip", out var ipElement))
                    {
                        externalIp = ipElement.GetString() ?? "неизвестно";
                    }
                }
                catch { }
                
                return new ApiTestResult
                {
                    Success = true,
                    Message = $"Прокси работает корректно!\n\n" +
                             $"Хост: {proxySettings.Host}\n" +
                             $"Порт: {proxySettings.Port}\n" +
                             $"Тип: {proxySettings.Type}\n" +
                             $"Внешний IP: {externalIp}\n\n" +
                             $"Прокси-сервер успешно подключен и функционирует."
                };
            }
            else
            {
                return new ApiTestResult
                {
                    Success = false,
                    Message = $"❌ Прокси не отвечает.\n\n" +
                             $"HTTP статус: {(int)response.StatusCode} ({response.StatusCode})\n\n" +
                             $"Проверьте настройки прокси-сервера."
                };
            }
        }
        catch (TaskCanceledException)
        {
            return new ApiTestResult
            {
                Success = false,
                Message = "❌ Превышено время ожидания (10 сек).\n\n" +
                         $"Прокси-сервер не отвечает.\n" +
                         $"Проверьте:\n" +
                         $"• IP адрес и порт\n" +
                         $"• Доступность прокси\n" +
                         $"• Логин и пароль (если требуется)"
            };
        }
        catch (HttpRequestException ex)
        {
            // Переводим стандартные английские ошибки на русский
            string errorMessage = ex.Message;
            string translatedMessage = errorMessage;
            
            if (errorMessage.Contains("An error occurred while sending the request"))
            {
                translatedMessage = "Ошибка при отправке запроса через прокси";
            }
            else if (errorMessage.Contains("No connection could be made"))
            {
                translatedMessage = "Не удалось установить соединение с прокси-сервером";
            }
            else if (errorMessage.Contains("actively refused"))
            {
                translatedMessage = "Прокси-сервер отклонил соединение";
            }
            else if (errorMessage.Contains("timed out"))
            {
                translatedMessage = "Время ожидания подключения истекло";
            }
            
            return new ApiTestResult
            {
                Success = false,
                Message = $"❌ Ошибка подключения к прокси.\n\n" +
                         $"Детали: {translatedMessage}\n\n" +
                         $"Возможные причины:\n" +
                         $"• Неверный IP адрес или порт\n" +
                         $"• Прокси-сервер недоступен\n" +
                         $"• Неверный логин/пароль"
            };
        }
        catch (Exception ex)
        {
            return new ApiTestResult
            {
                Success = false,
                Message = $"❌ Неизвестная ошибка.\n\n{ex.Message}"
            };
        }
    }

    /// <summary>
    /// Анализирует изображение с помощью Vision модели OpenRouter
    /// </summary>
    /// <param name="apiKey">API ключ OpenRouter</param>
    /// <param name="modelId">ID модели с поддержкой Vision</param>
    /// <param name="imageBytes">Байты изображения (PNG или JPEG)</param>
    /// <param name="prompt">Промпт для анализа</param>
    /// <param name="maxTokens">Максимальное количество токенов в ответе</param>
    public static async Task<ImageAnalysisResult> AnalyzeImage(
        string apiKey, 
        string modelId, 
        byte[] imageBytes, 
        string prompt,
        int maxTokens = 100)
    {
        try
        {
            // Конвертируем изображение в base64
            string base64Image = Convert.ToBase64String(imageBytes);
            
            // Определяем MIME тип (поддерживаем PNG и JPEG)
            string mimeType = "image/png";
            if (imageBytes.Length > 2)
            {
                // JPEG signature: FF D8 FF
                if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8 && imageBytes[2] == 0xFF)
                {
                    mimeType = "image/jpeg";
                }
            }

            // Формируем запрос согласно OpenRouter Vision API
            var requestBody = new
            {
                model = modelId,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = prompt },
                            new
                            {
                                type = "image_url",
                                image_url = new
                                {
                                    url = $"data:{mimeType};base64,{base64Image}"
                                }
                            }
                        }
                    }
                },
                max_tokens = maxTokens
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Создаём запрос
            var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/chat/completions");
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Headers.Add("HTTP-Referer", "https://autobet.app");
            request.Headers.Add("X-Title", "AutoBet");
            request.Content = content;

            // Отправляем запрос
            var httpClient = GetHttpClient();
            var response = await httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // Парсим успешный ответ
                using var doc = JsonDocument.Parse(responseContent);
                var root = doc.RootElement;

                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out var message))
                    {
                        if (message.TryGetProperty("content", out var contentElement))
                        {
                            string responseText = contentElement.GetString() ?? string.Empty;
                            return new ImageAnalysisResult
                            {
                                Success = true,
                                Response = responseText.Trim()
                            };
                        }
                    }
                }

                return new ImageAnalysisResult
                {
                    Success = false,
                    ErrorMessage = "API вернул некорректный ответ"
                };
            }
            else
            {
                // Обработка ошибок
                string errorMessage = $"HTTP {(int)response.StatusCode}: {response.StatusCode}";
                
                try
                {
                    using var doc = JsonDocument.Parse(responseContent);
                    if (doc.RootElement.TryGetProperty("error", out var error))
                    {
                        // Пытаемся извлечь детальное описание ошибки
                        string detailedError = "";
                        
                        if (error.TryGetProperty("message", out var message))
                        {
                            detailedError = message.GetString() ?? "";
                        }
                        
                        if (error.TryGetProperty("code", out var code))
                        {
                            detailedError = $"[{code.GetString()}] {detailedError}";
                        }
                        
                        if (error.TryGetProperty("metadata", out var metadata))
                        {
                            if (metadata.TryGetProperty("provider_name", out var provider))
                            {
                                detailedError += $" (Provider: {provider.GetString()})";
                            }
                        }
                        
                        errorMessage = !string.IsNullOrEmpty(detailedError) ? detailedError : errorMessage;
                    }
                    else
                    {
                        // Если нет поля error, выводим весь ответ
                        errorMessage = responseContent.Length > 500 
                            ? responseContent.Substring(0, 500) + "..." 
                            : responseContent;
                    }
                }
                catch
                {
                    // Используем сырой ответ
                    errorMessage = responseContent.Length > 500 
                        ? responseContent.Substring(0, 500) + "..." 
                        : responseContent;
                }

                return new ImageAnalysisResult
                {
                    Success = false,
                    ErrorMessage = errorMessage
                };
            }
        }
        catch (Exception ex)
        {
            return new ImageAnalysisResult
            {
                Success = false,
                ErrorMessage = $"Ошибка: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Распознаёт значения кубиков на изображении
    /// </summary>
    /// <param name="apiKey">API ключ</param>
    /// <param name="modelId">ID модели распознавания</param>
    /// <param name="leftDiceImage">Изображение левого кубика</param>
    /// <param name="rightDiceImage">Изображение правого кубика</param>
    /// <returns>Кортеж (левый кубик, правый кубик) или null при ошибке</returns>
    public static async Task<(int? left, int? right)> RecognizeDice(
        string apiKey,
        string modelId,
        byte[] leftDiceImage,
        byte[] rightDiceImage)
    {
        try
        {
            // Анализируем левый кубик
            var leftPrompt = "You are analyzing a dice image. Return ONLY a single digit number (1-6) representing the dice value. No other text.";
            var leftResult = await AnalyzeImage(apiKey, modelId, leftDiceImage, leftPrompt, 10);
            
            if (!leftResult.Success)
            {
                return (null, null);
            }

            // Анализируем правый кубик
            var rightPrompt = "You are analyzing a dice image. Return ONLY a single digit number (1-6) representing the dice value. No other text.";
            var rightResult = await AnalyzeImage(apiKey, modelId, rightDiceImage, rightPrompt, 10);
            
            if (!rightResult.Success)
            {
                return (null, null);
            }

            // Парсим результаты
            if (int.TryParse(leftResult.Response.Trim(), out int leftValue) &&
                int.TryParse(rightResult.Response.Trim(), out int rightValue))
            {
                // Валидация значений
                if (leftValue >= 1 && leftValue <= 6 && rightValue >= 1 && rightValue <= 6)
                {
                    return (leftValue, rightValue);
                }
            }

            return (null, null);
        }
        catch
        {
            return (null, null);
        }
    }
}
