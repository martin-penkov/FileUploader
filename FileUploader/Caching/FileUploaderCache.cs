using FileUploader.Common.Types;
using System.Collections.Concurrent;

namespace FileUploader.Caching
{
    public class FileUploaderCache : IFileUploaderCache
    {
        readonly ConcurrentDictionary<string, FileUploadCacheEntry> m_entries = new ConcurrentDictionary<string, FileUploadCacheEntry>();

        public void AddOrUpdate(string originalName, FileDescription fileDescr)
        {
            FileUploadCacheEntry data = Generate(fileDescr);

            m_entries.AddOrUpdate(originalName, data, (key, current) => data);
        }

        public FileDescription? Get(string originalName)
        {
            FileUploadCacheEntry result;

            bool entry = m_entries.TryGetValue(originalName, out result);

            if (!entry)
            {
                return null;
            }

            return MapToFileDescription(result);
        }

        public void ClearEntry(string originalName)
        {
            FileUploadCacheEntry value;
            m_entries.TryRemove(originalName, out value);
        }

        FileUploadCacheEntry Generate(FileDescription fileDescr)
        {
            return new FileUploadCacheEntry
            {
                NameWithoutExtension = fileDescr.NameWithoutExtension,
                PhysicalPath = fileDescr.PhysicalPath,
                RelativeLocation = fileDescr.RelativeLocation,
                Size = fileDescr.Size,
                Extension = fileDescr.Extension
            };
        }

        FileDescription MapToFileDescription(FileUploadCacheEntry entry)
        {
            return new FileDescription
            {
                NameWithoutExtension = entry.NameWithoutExtension,
                PhysicalPath = entry.PhysicalPath,
                RelativeLocation = entry.RelativeLocation,
                Size = entry.Size,
                Extension = entry.Extension
            };
        }
    }
}
