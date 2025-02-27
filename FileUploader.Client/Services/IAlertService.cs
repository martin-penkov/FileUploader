namespace FileUploader.Client.Services
{
    public interface IAlertService
    {
        public event Action<string> OnShow;

        public void ShowAlert(string message);
    }
}
