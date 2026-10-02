using System;
using System.Collections.Concurrent;
using System.IO;

namespace DataDictionaryWeb.Services
{
    public class DownloadItem
    {
        public string FilePath { get; set; } = string.Empty;
        public string DownloadFileName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class DocumentDownloadManager
    {
        private readonly ConcurrentDictionary<string, DownloadItem> _downloads = new();

        public string RegisterTempFile(string filePath, string downloadFileName)
        {
            CleanExpiredFiles();

            string token = Guid.NewGuid().ToString("N");
            _downloads[token] = new DownloadItem
            {
                FilePath = filePath,
                DownloadFileName = downloadFileName,
                CreatedAt = DateTime.UtcNow
            };

            return token;
        }

        public bool TryGetFile(string token, out string? filePath, out string? fileName)
        {
            if (_downloads.TryRemove(token, out var item))
            {
                if (File.Exists(item.FilePath))
                {
                    filePath = item.FilePath;
                    fileName = item.DownloadFileName;
                    return true;
                }
            }

            filePath = null;
            fileName = null;
            return false;
        }

        private void CleanExpiredFiles()
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-15);
            foreach (var kvp in _downloads)
            {
                if (kvp.Value.CreatedAt < cutoff)
                {
                    if (_downloads.TryRemove(kvp.Key, out var expired))
                    {
                        try
                        {
                            if (File.Exists(expired.FilePath))
                            {
                                File.Delete(expired.FilePath);
                            }
                        }
                        catch { }
                    }
                }
            }
        }
    }
}
