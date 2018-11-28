using System;
using System.Linq;

namespace Konamiman.NestorGithub
{
    class LocalRepository
    {
        const string databaseDirectory = ".ngh";
        const string stateFileName = "state";
        const string treeFileName = "tree";
        private static readonly string stateFilePath;
        private static readonly string treeFilePath;

        private readonly FilesystemDirectory directory;
        private readonly ApiClient api;

        private string commitSha;

        public string FullRepositoryName { get; private set; }
        public string BranchName { get; private set; }

        static LocalRepository()
        {
            stateFilePath = FilesystemDirectory.CombinePath(databaseDirectory, stateFileName);
            treeFilePath = FilesystemDirectory.CombinePath(databaseDirectory, treeFileName);
        }

        public LocalRepository(FilesystemDirectory directory, ApiClient api, bool allowNonLinkedDirectory = false)
        {
            this.directory = directory;
            this.api = api;
            this.FullRepositoryName = GetRepositoryNameFor(directory.PhysicalPath, allowNonLinkedDirectory);

            if (IsInitialized)
                ParseStateFile();
        }

        public bool HasContents => directory.HasContents;

        public bool IsInitialized => directory.DirectoryExists(databaseDirectory);

        public static string GetRepositoryNameFor(string localPath, bool allowNonLinkedDirectory = false)
        {
            var directory = new FilesystemDirectory(localPath);
            if (directory.FileExists(stateFilePath))
                return directory.ReadTextFile(stateFilePath).SplitInLines()[0];

            if (allowNonLinkedDirectory)
                return null;
            else
                throw new InvalidOperationException($"{localPath} is not linked to any remote repository");
        }

        public void Clone(string fullRepositoryName)
        {
            if (IsInitialized && fullRepositoryName != this.FullRepositoryName)
            {
                throw new InvalidOperationException($"The repository at '{directory.PhysicalPath}' is already initialized for repository {this.FullRepositoryName}");
            }
            else if (!IsInitialized && directory.HasContents)
            {
                throw new InvalidOperationException($"The target directory '{directory.PhysicalPath}' is not empty");
            }

            directory.CreateIfNotExists();
            this.FullRepositoryName = fullRepositoryName;

            RepositoryFileReference[] treeFiles = null;

            if (IsInitialized)
            {
                PrintLine($"Respository {fullRepositoryName} already initialized, continuing the clone process\r\n");
                treeFiles = ParseTreeFile();
            }
            else
            {
                PrintLine("Getting repository information...");

                BranchName = api.GetRepositoryInfo().DefaultBranch;
                commitSha = null;

                try
                {
                    commitSha = api.GetBranchCommitSha(BranchName);
                }
                catch (ApiException ex) when (ex.StatusCode == 409)
                {
                    throw new InvalidOperationException($"The repository {fullRepositoryName} is empty, please use 'link' instead of 'clone'");
                }

                var treeSha = api.GetCommitData(commitSha);
                treeFiles = api.GetTreeFiles(commitSha);

                UpdateStateFile();
                UpdateTreeFile(treeFiles);
            }

            PrintLine($"Getting files for branch {BranchName}");
            foreach (var file in treeFiles)
            {
                if (directory.FileExists(file.Path) && directory.GetFileSize(file.Path) == file.Size)
                {
                    PrintLine($"  {file.Path} - already exists, skipping");
                }
                else
                {
                    PrintLine($"  {file.Path} ...");
                    DownloadFile(file);
                }
            }
        }

        public void Link(string fullRepositoryName)
        {
            if (!directory.Exists)
                throw new InvalidOperationException($"Directory {directory.PhysicalPath} does not exist");

            if (this.IsInitialized)
            {
                throw new InvalidOperationException($"The repository at '{directory.PhysicalPath}' is already initialized for repository {this.FullRepositoryName}");
            }

            this.FullRepositoryName = fullRepositoryName;

            RepositoryFileReference[] treeFiles = null;

            PrintLine("Getting repository information...");

            BranchName = api.GetRepositoryInfo().DefaultBranch;
            commitSha = null;

            try
            {
                commitSha = api.GetBranchCommitSha(BranchName);
            }
            catch (ApiException ex) when (ex.StatusCode == 409)
            {
                //409 = no commits exist, whe don't care when just linking
            }

            if (commitSha == null)
            {
                UpdateStateFile();
            }
            else
            {
                var treeSha = api.GetCommitData(commitSha);
                treeFiles = api.GetTreeFiles(commitSha);

                UpdateStateFile();
                UpdateTreeFile(treeFiles);
            }
        }

        public void Unlink()
        {
            if(!directory.DirectoryExists(""))
            {
                throw new InvalidOperationException($"Directory '{directory.PhysicalPath}' does not exist");
            }

            if (!IsInitialized)
            {
                throw new InvalidOperationException($"The target directory '{directory.PhysicalPath}' is not linked to any remote repository");
            }

            ParseStateFile();

            directory.DeleteDirectory(databaseDirectory);
        }

        RepositoryFileReference[] ParseTreeFile()
        {
            return directory
                .ReadTextFile(treeFilePath)
                .SplitInLines()
                .Select(line => {
                    var parts = line.SplitBySpace(3);
                    return new RepositoryFileReference { BlobSha = parts[0], Size = long.Parse(parts[1]), Path = parts[2] };
                })
                .ToArray();
        }

        private void UpdateStateFile()
        {
            directory.CreateFile($"{FullRepositoryName}\r\n{BranchName}\r\n{commitSha}\r\n", stateFilePath);
        }

        private void UpdateTreeFile(RepositoryFileReference[] fileReferences)
        {
            var fileLines = fileReferences.Select(fr => $"{fr.BlobSha} {fr.Size} {fr.Path}");

            directory.CreateFile(fileLines.JoinInLines(), treeFilePath);
        }

        private void DownloadFile(RepositoryFileReference fileReference)
        {
            var fileContents = api.GetBlob(fileReference.BlobSha);
            directory.CreateFile(fileContents, fileReference.Path);
        }

        private void ParseStateFile()
        {
            var stateFileContents = directory.ReadTextFile(stateFilePath);
            var stateFileParts = stateFileContents.SplitInLines(removeEmptyEntries: false);
            this.FullRepositoryName = stateFileParts[0];
            this.BranchName = stateFileParts[1];
            this.commitSha = stateFileParts[2];
        }

        public static void PrintLine(string value)
        {
            Printer.PrintLine(value);
        }
    }
}
