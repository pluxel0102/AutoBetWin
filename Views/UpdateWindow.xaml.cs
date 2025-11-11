using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AutoBet.Models;
using AutoBet.Services;
using System;

namespace AutoBet.Views;

public sealed partial class UpdateWindow : Window
{
    private readonly UpdateInfo _updateInfo;
    private bool _isDownloading = false;

    public UpdateWindow(UpdateInfo updateInfo)
    {
        this.InitializeComponent();
        _updateInfo = updateInfo;
        
        // Настройка окна
        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(null);
        
        // Устанавливаем размер окна через AppWindow API
        var scale = (float)this.AppWindow.ClientSize.Width / 1920.0f; // Получаем масштаб
        var width = (int)(600 * Math.Max(scale, 1.0f));
        var height = (int)(500 * Math.Max(scale, 1.0f));
        this.AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
        
        LoadUpdateInfo();
    }

    private void LoadUpdateInfo()
    {
        var currentVersion = UpdateService.GetCurrentVersion();
        VersionText.Text = $"Версия {currentVersion} → {_updateInfo.Version}";
        
        // Форматируем changelog
        ChangelogText.Text = string.IsNullOrWhiteSpace(_updateInfo.Description) 
            ? "Нет описания изменений" 
            : _updateInfo.Description;
        
        // Размер файла
        if (_updateInfo.FileSize > 0)
        {
            var sizeMB = _updateInfo.FileSize / (1024.0 * 1024.0);
            FileSizeText.Text = $"Размер: {sizeMB:F2} МБ";
        }
        else
        {
            FileSizeText.Text = "Размер: неизвестно";
        }
        
        // Дата выпуска
        if (_updateInfo.ReleaseDate != default)
        {
            ReleaseDateText.Text = $"Дата выпуска: {_updateInfo.ReleaseDate:dd.MM.yyyy HH:mm}";
        }
        else
        {
            ReleaseDateText.Text = "Дата выпуска: неизвестно";
        }
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isDownloading) return;
        
        if (string.IsNullOrEmpty(_updateInfo.DownloadUrl))
        {
            await ShowErrorDialog("Ошибка", "Ссылка на скачивание не найдена");
            return;
        }
        
        _isDownloading = true;
        
        // Меняем интерфейс на режим загрузки
        DownloadButton.IsEnabled = false;
        LaterButton.IsEnabled = false;
        DownloadButton.Content = "Скачивание...";
        
        // Создаём прогресс бар
        var progressBar = new ProgressBar
        {
            IsIndeterminate = false,
            Value = 0,
            Margin = new Thickness(0, 16, 0, 0)
        };
        
        ContentPanel.Children.Add(progressBar);
        
        // Скачиваем и устанавливаем
        var progress = new Progress<int>(percent =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                progressBar.Value = percent;
                DownloadButton.Content = $"Скачивание... {percent}%";
            });
        });
        
        var result = await UpdateService.DownloadAndInstallUpdateAsync(_updateInfo.DownloadUrl, progress);
        
        if (!result.Success)
        {
            _isDownloading = false;
            DownloadButton.IsEnabled = true;
            LaterButton.IsEnabled = true;
            DownloadButton.Content = "Скачать обновление";
            
            ContentPanel.Children.Remove(progressBar);
            
            await ShowErrorDialog("Ошибка установки", result.ErrorMessage);
        }
        
        // Если успешно - приложение закроется автоматически
    }

    private void LaterButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
    
    private async System.Threading.Tasks.Task ShowErrorDialog(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.Content.XamlRoot
        };
        
        await dialog.ShowAsync();
    }
}
