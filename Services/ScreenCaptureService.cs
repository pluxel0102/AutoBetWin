using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AutoBet.Services;

/// <summary>
/// Сервис для захвата областей экрана (ROI - Region of Interest)
/// </summary>
public class ScreenCaptureService
{
    // P/Invoke для получения HWND окна по процессу
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>
    /// Захватывает область экрана и возвращает байты изображения в формате PNG
    /// </summary>
    public static async Task<byte[]?> CaptureRegion(int x, int y, int width, int height)
    {
        try
        {
            if (width <= 0 || height <= 0)
                return null;

            return await Task.Run(() => CaptureScreenshot(x, y, width, height));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScreenCapture] Ошибка захвата: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Захватывает весь экран
    /// </summary>
    public static async Task<byte[]?> CaptureFullScreen()
    {
        try
        {
            return await Task.Run(() =>
            {
                // Получаем границы основного экрана
                var screenBounds = GetPrimaryScreenBounds();
                return CaptureScreenshot(screenBounds.X, screenBounds.Y, screenBounds.Width, screenBounds.Height);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScreenCapture] Ошибка захвата полного экрана: {ex.Message}");
            return null;
        }
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    private static (int X, int Y, int Width, int Height) GetPrimaryScreenBounds()
    {
        int width = GetSystemMetrics(SM_CXSCREEN);
        int height = GetSystemMetrics(SM_CYSCREEN);
        return (0, 0, width, height);
    }

    private static byte[] CaptureScreenshot(int x, int y, int width, int height)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        
        graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
        
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    /// <summary>
    /// Получает координаты активного окна
    /// </summary>
    public static (int x, int y, int width, int height)? GetActiveWindowBounds()
    {
        try
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return null;

            if (GetWindowRect(hwnd, out RECT rect))
            {
                return (
                    rect.Left,
                    rect.Top,
                    rect.Right - rect.Left,
                    rect.Bottom - rect.Top
                );
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// ROI конфигурация для MelBet
    /// </summary>
    public class MelBetROI
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Сохраняет ROI конфигурацию в настройки
    /// </summary>
    public static void SaveROIConfiguration(MelBetROI[] regions)
    {
        // Конвертируем в формат SettingsService
        var settingsRegions = regions.Select(r => new SettingsService.ROIRegion
        {
            Name = r.Name,
            X = r.X,
            Y = r.Y,
            Width = r.Width,
            Height = r.Height
        }).ToArray();

        SettingsService.SaveMelBetROI(settingsRegions);
    }

    /// <summary>
    /// Загружает ROI конфигурацию из настроек
    /// </summary>
    public static MelBetROI[]? LoadROIConfiguration()
    {
        var settingsRegions = SettingsService.LoadMelBetROI();
        
        if (settingsRegions == null || settingsRegions.Length == 0)
            return null;

        // Конвертируем из формата SettingsService
        return settingsRegions.Select(r => new MelBetROI
        {
            Name = r.Name,
            X = r.X,
            Y = r.Y,
            Width = r.Width,
            Height = r.Height
        }).ToArray();
    }

    /// <summary>
    /// Удаляет все сохранённые ROI области
    /// </summary>
    public static void DeleteROIConfiguration()
    {
        SettingsService.DeleteMelBetROI();
    }
}
