using System;
using System.IO;
using System.Text;

namespace AFrame.Save
{
    public sealed class AtomicJsonFile
    {
        private readonly string path;

        public AtomicJsonFile(string path) => this.path = Path.GetFullPath(path);

        public bool Exists => File.Exists(path);
        public string Read() => File.ReadAllText(path);

        public void Write(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("The save JSON is empty.", nameof(json));

            var directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var bytes = Encoding.UTF8.GetBytes(json);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                if (File.Exists(path))
                    File.Replace(temporary, path, null);
                else
                    File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
    }
}
