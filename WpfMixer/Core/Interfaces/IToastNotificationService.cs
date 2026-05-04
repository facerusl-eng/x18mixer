namespace WpfMixer.Core.Interfaces;

public interface IToastNotificationService
{
    void ShowInfo(string message, int milliseconds = 2500);
    void ShowWarning(string message, int milliseconds = 3500);
    void ShowError(string message, int milliseconds = 5000);
}
