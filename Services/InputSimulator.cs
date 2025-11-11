using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AutoBet.Services;

/// <summary>
/// Сервис для симуляции ввода (клики мышью)
/// </summary>
public static class InputSimulator
{
    // Windows API для симуляции кликов мыши
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

    // Константы для mouse_event
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;

    /// <summary>
    /// Выполняет клик левой кнопкой мыши по указанным координатам
    /// </summary>
    /// <param name="x">X координата</param>
    /// <param name="y">Y координата</param>
    /// <param name="delayMs">Задержка после клика в миллисекундах</param>
    public static async Task ClickAsync(int x, int y, int delayMs = 100)
    {
        // Перемещаем курсор
        SetCursorPos(x, y);
        
        // Небольшая задержка для стабильности
        await Task.Delay(10);
        
        // Нажатие левой кнопки
        mouse_event(MOUSEEVENTF_LEFTDOWN, x, y, 0, UIntPtr.Zero);
        
        // Задержка между нажатием и отпусканием
        await Task.Delay(50);
        
        // Отпускание левой кнопки
        mouse_event(MOUSEEVENTF_LEFTUP, x, y, 0, UIntPtr.Zero);
        
        // Задержка после клика
        if (delayMs > 0)
            await Task.Delay(delayMs);
    }

    /// <summary>
    /// Выполняет клик по центру области
    /// </summary>
    /// <param name="x">X координата верхнего левого угла</param>
    /// <param name="y">Y координата верхнего левого угла</param>
    /// <param name="width">Ширина области</param>
    /// <param name="height">Высота области</param>
    /// <param name="delayMs">Задержка после клика</param>
    public static async Task ClickAreaAsync(int x, int y, int width, int height, int delayMs = 100)
    {
        int centerX = x + width / 2;
        int centerY = y + height / 2;
        await ClickAsync(centerX, centerY, delayMs);
    }

    /// <summary>
    /// Симулирует свайп (перетаскивание мыши)
    /// </summary>
    public static async Task SwipeAsync(int startX, int startY, int endX, int endY, int durationMs = 300)
    {
        SetCursorPos(startX, startY);
        await Task.Delay(10);
        
        mouse_event(MOUSEEVENTF_LEFTDOWN, startX, startY, 0, UIntPtr.Zero);
        await Task.Delay(50);
        
        // Плавное перемещение
        int steps = 10;
        for (int i = 1; i <= steps; i++)
        {
            int x = startX + (endX - startX) * i / steps;
            int y = startY + (endY - startY) * i / steps;
            SetCursorPos(x, y);
            await Task.Delay(durationMs / steps);
        }
        
        mouse_event(MOUSEEVENTF_LEFTUP, endX, endY, 0, UIntPtr.Zero);
        await Task.Delay(100);
    }
}
