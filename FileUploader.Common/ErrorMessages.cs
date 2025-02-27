namespace FileUploader.Common
{
    public static class ErrorMessage
    {
        public const string FileNotFound = "The requested file was not found.";
        public const string Unauthorized = "You do not have permission to perform this action.";
        public const string UnexpectedError = "An unexpected error has occurred.";
        public const string PhysicalFileNotFound = "File could not be retrieved.";
        public const string FileWithSameNameExists = "Upload failed! Name already in use";
        public const string ErrorDuringFileUpload = "Error during file upload";
    }
}
