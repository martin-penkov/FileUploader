using FileUploader.Common.Types;

namespace FileUploader.Caching
{
    public interface IFileUploaderCache
    {
        void AddOrUpdate(string originalName, FileDescription fileDescr);

        FileDescription Get(string originalName);
    }
}
