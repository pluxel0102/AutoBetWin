# Тестовый скрипт для проверки сохранения настроек
Write-Host "Запуск приложения для теста настроек..."
Write-Host "1. Измените тему на тёмную"
Write-Host "2. Вставьте API ключ"
Write-Host "3. Закройте приложение"
Write-Host "4. Запустите снова и проверьте, сохранились ли настройки"
Write-Host ""
Write-Host "Нажмите любую клавишу для запуска..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

.\bin\x64\Debug\net8.0-windows10.0.19041.0\AutoBet.exe
