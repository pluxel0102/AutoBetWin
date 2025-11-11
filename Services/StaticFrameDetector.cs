using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace AutoBet.Services;

/// <summary>
/// Детектор статических кадров - определяет стабильность изображения
/// Используется для проверки, что кубики остановились и результат стабилен
/// </summary>
public class StaticFrameDetector
{
    private readonly Queue<string> _frameHashes;
    private readonly int _requiredStableFrames;
    private readonly int _maxHistorySize;

    /// <summary>
    /// Конструктор детектора
    /// </summary>
    /// <param name="requiredStableFrames">Количество последовательных одинаковых кадров для признания стабильности</param>
    /// <param name="maxHistorySize">Максимальный размер истории хешей</param>
    public StaticFrameDetector(int requiredStableFrames = 3, int maxHistorySize = 10)
    {
        _requiredStableFrames = requiredStableFrames;
        _maxHistorySize = maxHistorySize;
        _frameHashes = new Queue<string>(maxHistorySize);
    }

    /// <summary>
    /// Добавляет новый кадр и проверяет стабильность
    /// </summary>
    /// <param name="frameData">Байты изображения кадра</param>
    /// <returns>True, если кадр стабилен (не меняется последние N кадров)</returns>
    public bool AddFrame(byte[] frameData)
    {
        if (frameData == null || frameData.Length == 0)
            return false;

        // Вычисляем хеш кадра
        string frameHash = ComputeHash(frameData);

        // Добавляем в историю
        _frameHashes.Enqueue(frameHash);

        // Ограничиваем размер истории
        while (_frameHashes.Count > _maxHistorySize)
        {
            _frameHashes.Dequeue();
        }

        // Проверяем стабильность
        return IsStable();
    }

    /// <summary>
    /// Проверяет, стабилен ли текущий кадр
    /// </summary>
    public bool IsStable()
    {
        // Нужно минимум requiredStableFrames кадров
        if (_frameHashes.Count < _requiredStableFrames)
            return false;

        // Берём последние N кадров
        var recentFrames = _frameHashes.TakeLast(_requiredStableFrames).ToList();

        // Проверяем, что все хеши одинаковые
        string firstHash = recentFrames[0];
        return recentFrames.All(hash => hash == firstHash);
    }

    /// <summary>
    /// Получает текущий уровень стабильности (0.0 - 1.0)
    /// </summary>
    public double GetStabilityScore()
    {
        if (_frameHashes.Count < 2)
            return 0.0;

        var recentFrames = _frameHashes.TakeLast(_requiredStableFrames).ToList();
        if (recentFrames.Count < 2)
            return 0.0;

        string mostCommonHash = recentFrames
            .GroupBy(h => h)
            .OrderByDescending(g => g.Count())
            .First()
            .Key;

        int matchingFrames = recentFrames.Count(h => h == mostCommonHash);
        return (double)matchingFrames / recentFrames.Count;
    }

    /// <summary>
    /// Сбрасывает историю кадров
    /// </summary>
    public void Reset()
    {
        _frameHashes.Clear();
    }

    /// <summary>
    /// Количество кадров в истории
    /// </summary>
    public int FrameCount => _frameHashes.Count;

    /// <summary>
    /// Вычисляет быстрый хеш изображения
    /// </summary>
    private string ComputeHash(byte[] data)
    {
        using var sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(data);
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Альтернативный метод: вычисление перцептивного хеша (pHash)
    /// Более устойчив к небольшим изменениям изображения
    /// </summary>
    public static string ComputePerceptualHash(byte[] imageData, int hashSize = 8)
    {
        // TODO: Реализовать pHash алгоритм
        // 1. Конвертировать в grayscale
        // 2. Уменьшить до hashSize x hashSize
        // 3. Вычислить DCT (Discrete Cosine Transform)
        // 4. Оставить top-left квадрант
        // 5. Вычислить среднее значение
        // 6. Создать битовую маску (1 если > среднего, 0 иначе)
        
        // Пока используем обычный SHA256
        using var sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(imageData);
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Вычисляет расстояние Хэмминга между двумя хешами
    /// Используется для сравнения перцептивных хешей
    /// </summary>
    public static int HammingDistance(string hash1, string hash2)
    {
        if (hash1.Length != hash2.Length)
            return int.MaxValue;

        int distance = 0;
        for (int i = 0; i < hash1.Length; i++)
        {
            if (hash1[i] != hash2[i])
                distance++;
        }

        return distance;
    }

    /// <summary>
    /// Проверяет, похожи ли два изображения (на основе перцептивного хеша)
    /// </summary>
    /// <param name="hash1">Первый хеш</param>
    /// <param name="hash2">Второй хеш</param>
    /// <param name="threshold">Порог различия (0-100, меньше = строже)</param>
    public static bool AreSimilar(string hash1, string hash2, int threshold = 10)
    {
        int distance = HammingDistance(hash1, hash2);
        int maxDistance = hash1.Length;
        
        if (maxDistance == 0)
            return true;

        int percentDifference = (distance * 100) / maxDistance;
        return percentDifference <= threshold;
    }
}
