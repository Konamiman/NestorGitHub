using System;
using System.IO;
using System.Text;

namespace Konamiman.NestorGithub
{
    class FilesystemDirectory
    {
        const string databaseDirectory = ".ngh";
        private readonly string rootPath;

        public FilesystemDirectory(string path = null)
        {
            this.rootPath = path ?? Directory.GetCurrentDirectory();
        }

        public string PhysicalPath => rootPath;

        public bool HasContents => Directory.GetFileSystemEntries(rootPath).Length > 0;

        public bool Exists => Directory.Exists(rootPath);

        public void CreateIfNotExists()
        {
            if (!Exists) Directory.CreateDirectory(rootPath);
        }

        public bool DirectoryExists(params string[] pathSegments) => Directory.Exists(Combine(pathSegments));

        public bool FileExists(params string[] pathSegments) => File.Exists(Combine(pathSegments));

        public void CreateFile(string contents, params string[] pathSegments)
        {
            CreateFile(Encoding.UTF8.GetBytes(contents), pathSegments);
        }

        public void CreateFile(byte[] contents, params string[] pathSegments)
        {
            var fileInfo = new FileInfo(Combine(pathSegments));
            fileInfo.Directory.Create();
            File.WriteAllBytes(fileInfo.FullName, contents);
        }

        public string ReadTextFile(string path)
        {
            return File.ReadAllText(Combine(rootPath, path), Encoding.UTF8);
        }

        public void DeleteDirectory(string path)
        {
            if(DirectoryExists(path))
            {
                Directory.Delete(Combine(path), recursive: true);
            }
        }

        private string Combine(params string[] pathSegments)
        {
            return CombinePath(rootPath, CombinePath(pathSegments));
        }

        public static string CombinePath(params string[] pathSegments)
        {
            return Path.Combine(pathSegments).Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}
