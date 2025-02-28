namespace FileUploader.Client.Services.AlertService
{
    public class AlertService : IAlertService
    {
        public event Action<string> OnShow;

        public void ShowAlert(string message)
        {
            OnShow?.Invoke(message);
        }
    }
}
