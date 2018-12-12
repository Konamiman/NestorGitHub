using System;
using System.IO;
using System.Text;
using System.Linq;

namespace Konamiman.NestorGithub
{
    /// <summary>
    /// This class encapsulates all the file and directory operations performed on a directory in the local filesystem.
    /// </summary>
    class FilesystemDirectory
    {
        private string rootPath;

        /// <summary>
        /// Creates a new instance of the class
        /// </summary>
        /// <param name="path">Root path where the class will operate, null for the current directory.</param>
        public FilesystemDirectory(string path = null)
        {
            this.rootPath = PathWithProperCasingAndSeparators(path ?? Directory.GetCurrentDirectory());
            if(this.rootPath == null)
                throw new InvalidOperationException($"Directory {path} does not exist");
        }

        public string PhysicalPath => rootPath;

        public bool HasContents => Directory.GetFileSystemEntries(rootPath).Length > 0;

        public static bool AbsoluteDirectoryExists(string path) => Directory.Exists(path);

        public bool DirectoryExists(string path) => Directory.Exists(AbsolutePathOf(path));

        public bool FileExists(string path) => File.Exists(AbsolutePathOf(path));

        public void CreateFile(string contents, string path)
        {
            CreateFile(Encoding.UTF8.GetBytes(contents), path);
        }

        public void CreateFile(byte[] contents, string path)
        {
            var fileInfo = new FileInfo(AbsolutePathOf(path));
            fileInfo.Directory.Create();
            File.WriteAllBytes(fileInfo.FullName, contents);
        }

        public byte[] GetFileContents(string path)
        {
            return File.ReadAllBytes(AbsolutePathOf(path));
        }

        public string ReadTextFile(string path)
        {
            return File.ReadAllText(AbsolutePathOf(rootPath, path), Encoding.UTF8);
        }

        public void DeleteDirectory(string path)
        {
            if(DirectoryExists(path))
            {
                Directory.Delete(AbsolutePathOf(path), recursive: true);
            }
        }

        public long GetFileSize(string path)
        {
            var fileInfo = new FileInfo(AbsolutePathOf(path));
            return fileInfo.Length;
        }


        public static string CombinePath(params string[] pathSegments)
        {
            return Path.Combine(pathSegments).Replace(Path.DirectorySeparatorChar, '/');
        }

        private string AbsolutePathOf(params string[] pathSegments)
        {
            return CombinePath(rootPath, CombinePath(pathSegments));
        }

        public bool IsModified(string path)
        {
            var filePath = AbsolutePathOf(path);
            return (File.GetAttributes(filePath) & FileAttributes.Archive) == FileAttributes.Archive;
        }

        public void SetModified(string path)
        {
            var filePath = AbsolutePathOf(path);
            var currentAttributes = File.GetAttributes(filePath);
            File.SetAttributes(filePath, currentAttributes | FileAttributes.Archive);
        }

        public void SetUnmodified(string path)
        {
            var filePath = AbsolutePathOf(path);
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
            File.Delete(AbsolutePathOf(filename));
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
