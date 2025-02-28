namespace FileUploader.Client.Services.AlertService
{
    public interface IAlertService
    {
        public event Action<string> OnShow;

        public void ShowAlert(string message);
    }
}
