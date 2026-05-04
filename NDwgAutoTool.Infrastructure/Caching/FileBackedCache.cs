using System.IO;

namespace NDwgAutoTool.Infrastructure.Caching
{
    public sealed class FileBackedCache<T>
    {
        private readonly object _sync = new();
        private readonly Func<string, T> _load;
        private string? _path;
        private DateTime _lastWriteUtc;
        private T? _value;

        public FileBackedCache(Func<string, T> load)
        {
            _load = load;
        }

        public T Get(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("File path is required.", nameof(path));

            var info = new FileInfo(path);
            if (!info.Exists)
                throw new FileNotFoundException("Cached source file was not found.", path);

            lock (_sync)
            {
                if (_value != null &&
                    string.Equals(_path, info.FullName, StringComparison.OrdinalIgnoreCase) &&
                    _lastWriteUtc == info.LastWriteTimeUtc)
                {
                    return _value;
                }

                _value = _load(info.FullName);
                _path = info.FullName;
                _lastWriteUtc = info.LastWriteTimeUtc;
                return _value;
            }
        }

        public void Clear()
        {
            lock (_sync)
            {
                _path = null;
                _lastWriteUtc = default;
                _value = default;
            }
        }
    }
}
