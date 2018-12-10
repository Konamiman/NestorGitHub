using System;
using System.IO;
using System.Text;
using System.Linq;

namespace Konamiman.NestorGithub
{
    class FilesystemDirectory
    {
        private string rootPath;

        public FilesystemDirectory(string path = null)
        {
            this.rootPath = PathWithProperCasingAndSeparators(path ?? Directory.GetCurrentDirectory());
            if(this.rootPath == null)
                throw new InvalidOperationException($"Directory {path} does not exist");
        }

        public string PhysicalPath => rootPath;

        public bool HasContents => Directory.GetFileSystemEntries(rootPath).Length > 0;

        public static bool AbsoluteDirectoryExists(string path) => Directory.Exists(path);

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

        public byte[] GetFileContents(params string[] pathSegments)
        {
            return File.ReadAllBytes(Combine(pathSegments));
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

        public long GetFileSize(string path)
        {
            var fileInfo = new FileInfo(Combine(path));
            return fileInfo.Length;
        }

        private string Combine(params string[] pathSegments)
        {
            return CombinePath(rootPath, CombinePath(pathSegments));
        }

        public static string CombinePath(params string[] pathSegments)
        {
            return Path.Combine(pathSegments).Replace(Path.DirectorySeparatorChar, '/');
        }

        public bool IsModified(params string[] pathSegments)
        {
            var filePath = Combine(pathSegments);
            return (File.GetAttributes(filePath) & FileAttributes.Archive) == FileAttributes.Archive;
        }

        public void SetModified(params string[] pathSegments)
        {
            var filePath = Combine(pathSegments);
            var currentAttributes = File.GetAttributes(filePath);
            File.SetAttributes(filePath, currentAttributes | FileAttributes.Archive);
        }

        public void SetUnmodified(params string[] pathSegments)
        {
            var filePath = Combine(pathSegments);
            var currentAttributes = File.GetAttributes(filePath);
            File.SetAttributes(filePath, currentAttributes & ~FileAttributes.Archive);
        }

        public void DoForAllFiles(Func<string, bool> action)
        {
            DoForAllFilesCore(rootPath, action);
        }

        private bool DoForAllFilesCore(string directory, Func<string, bool> action)
        {
            foreach(var file in Directory.GetFiles(directory))
            {
                var relativeFile = file.Replace(Path.DirectorySeparatorChar, '/').Replace(rootPath, "").Trim('/');
                var continues = action(relativeFile);
                if(!continues) return false;
            }

            foreach(var subdirectory in Directory.GetDirectories(directory))
            {
                var continues = DoForAllFilesCore(subdirectory, action);
                if (!continues) return false;
            }

            return true;
        }

        public void DeleteFile(string filename)
        {
            File.Delete(Combine(filename));
        }

        public bool MoveOneDirectoryUp()
        {
            var slashIndex = rootPath.LastIndexOf("/");
            if (slashIndex == -1) return false;
            rootPath = rootPath.Substring(0, slashIndex);
            return true;
        }

        //https://stackoverflow.com/a/28919652/4574
        public string PathWithProperCasingAndSeparators(string filePath)
        {
            string fullFilePath = Path.GetFullPath(filePath).Replace('/', Path.DirectorySeparatorChar);

            string fixedPath = "";
            foreach (string token in fullFilePath.Split('\\'))
            {
                //first token should be drive token
                if (fixedPath == "")
                {
                    //fix drive casing
                    string drive = string.Concat(token, "\\");
                    drive = DriveInfo.GetDrives()
                        .First(driveInfo => driveInfo.Name.Equals(drive, StringComparison.OrdinalIgnoreCase)).Name;

                    fixedPath = drive;
                }
                else
                {
                    fixedPath = Directory.GetFileSystemEntries(fixedPath, token).FirstOrDefault();
                    if (fixedPath == null) return null;
                }
            }

            return fixedPath.Replace(Path.DirectorySeparatorChar, '/');
        }

        public static string AbsoluteDirectoryOf(string path)
        {
            if (path == null)
                return Path.GetFullPath(".");

            var pathDirectory = Path.GetDirectoryName(path);
            if (pathDirectory == "") pathDirectory = ".";

            return Path.GetFullPath(pathDirectory);
        }

        public string RelativePathOf(string path)
        {
            var absolutePath = PathWithProperCasingAndSeparators(path);
            return absolutePath.Substring(rootPath.Length + 1);
        }

        public string[] FindFiles(string pathspec)
        {
            var directoryToSearchIn = AbsoluteDirectoryOf(pathspec);
            var searchPattern = Path.GetFileName(pathspec);
            if (searchPattern == "") searchPattern = "*.*";
            return Directory.GetFiles(directoryToSearchIn, searchPattern);
        }
    }
}
