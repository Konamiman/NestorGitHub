using System;
using System.Collections.Generic;
using System.Linq;

namespace Konamiman.NestorGithub
{
    class LocalRepository
    {
        const string databaseDirectory = ".ngh";
        const string databaseDirectoryWithSlash = databaseDirectory + "/";
        const string stateFileName = "state";
        const string treeFileName = "tree";
        private static readonly string stateFilePath;
        private static readonly string treeFilePath;

        private readonly FilesystemDirectory directory;

        public ApiClient Api { get; private set; }
        public string FullRepositoryName { get; private set; }
        public string BranchName { get; private set; }
        public string CommitSha { get; private set; }

        static LocalRepository()
        {
            stateFilePath = FilesystemDirectory.CombinePath(databaseDirectory, stateFileName);
            treeFilePath = FilesystemDirectory.CombinePath(databaseDirectory, treeFileName);
        }

        public LocalRepository(FilesystemDirectory directory, ApiClient api, bool allowNonLinkedDirectory = false)
        {
            this.directory = directory;
            this.Api = api;
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

                var branchCount = Api.GetBranchCount();
                if(branchCount == 0)
                    throw new InvalidOperationException($"The repository {fullRepositoryName} is empty, please use 'link' instead of 'clone'");

                BranchName = Api.GetRepositoryInfo().DefaultBranch;
                CommitSha = Api.GetBranchCommitSha(BranchName);
                var treeSha = Api.GetCommitTreeSha(CommitSha);
                treeFiles = Api.GetTreeFileReferences(treeSha);

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

            DoForAllLocalFiles(file => { directory.SetUnmodified(file); });
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

            var branchCount = Api.GetBranchCount();
            if (branchCount == 0)
            {
                BranchName = "master";
                CommitSha = null;
                UpdateStateFile();
                UpdateTreeFile(new RepositoryFileReference[0]);
            }
            else
            {
                BranchName = Api.GetRepositoryInfo().DefaultBranch;
                CommitSha = Api.GetBranchCommitSha(BranchName);
                var treeSha = Api.GetCommitTreeSha(CommitSha);
                treeFiles = Api.GetTreeFileReferences(treeSha);

                UpdateStateFile();
                UpdateTreeFile(treeFiles);
            }

            DoForAllLocalFiles(file => { directory.SetModified(file); });
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

        public void Commit(string authorName, string authorEmail, string commitMessage)
        {
            PrintLine("Checking local changes...");
            var localChanges = GetLocalChanges();

            if (!localChanges.HasChanges)
                throw new InvalidOperationException("No local changes, nothing to commit");

            PrintLine("Checking state of remote repository...");

            if (!ExistsRemotely())
                throw new InvalidOperationException("The remote repository doesn't exist!");

            if (!BranchExistsRemotely())
            {
                if(Api.GetBranchCount() > 0)
                    throw new InvalidOperationException($"Branch {BranchName} doesn't exist remotely!"); //TODO

                PrintLine("Creating initial empty commit...");
                CommitSha = CreateInitialEmptyCommit(authorName, authorEmail);
            }
            else if (!IsUpToDateWithRemote())
                throw new InvalidOperationException($"Your local repository isn't up to date with the remote repository, you need to pull before you can commit");

            var allChangedFiles = localChanges.AddedFiles.Union(localChanges.ModifiedFiles).ToArray();

            var localChangesFileReferences = new List<RepositoryFileReference>();
            if (allChangedFiles.Length > 0)
            {
                PrintLine("Pushing new and changed files...");
                foreach (var filePath in allChangedFiles)
                {
                    PrintLine($"  {filePath}...");
                    var fileContents = directory.GetFileContents(filePath);
                    var fileSha = Api.CreateBlob(fileContents);
                    localChangesFileReferences.Add(new RepositoryFileReference { Path = filePath, Size = fileContents.Length, BlobSha = fileSha });
                }
            }

            var currentCommitFileReferences = ParseTreeFile();
            var localChangesFilenames = localChanges.AllChangedFiles;
            var unchangedFileReferences = currentCommitFileReferences.Where(r => !localChangesFilenames.Contains(r.Path)).ToArray();

            PrintLine("Creating remote tree...");
            var treeSha = Api.CreateTree(unchangedFileReferences.Union(localChangesFileReferences).ToArray());

            PrintLine("Creating commit...");
            var newCommitSha = Api.CreateCommit(commitMessage, treeSha, CommitSha, authorName, authorEmail);

            PrintLine("Updating branch reference...");
            Api.SetBranchCommitSha(BranchName, newCommitSha);

            PrintLine("Updating local state...");

            CommitSha = newCommitSha;
            var currentLocalFileReferences = unchangedFileReferences.Union(localChangesFileReferences).ToArray();
            UpdateTreeFile(currentLocalFileReferences);
            UpdateStateFile();
            DoForAllLocalFiles(file => { directory.SetUnmodified(file); });
        }

        private string CreateInitialEmptyCommit(string authorName, string authorEmail)
        {
            const string EmptyTreeSha = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";

            Api.CreateFileInRemoteRepository("dummy", BranchName, "dummy commit", new byte[] { 0 });
            var commitSha = Api.CreateCommit("Initial commit", EmptyTreeSha, null, authorName, authorEmail);
            Api.SetBranchCommitSha(BranchName, commitSha, force: true);
            return commitSha;
        }

        public bool ExistsRemotely()
        {
            return Api.RepositoryExists();
        }

        public bool BranchExistsRemotely()
        {
            return Api.GetBranchCommitSha(BranchName) != null;
        }

        public bool IsUpToDateWithRemote()
        {
            var remoteCommitSha = Api.GetBranchCommitSha(BranchName);
            return remoteCommitSha == CommitSha;
        }

        public RepositoryFileReference[] ParseTreeFile()
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
            directory.CreateFile($"{FullRepositoryName}\r\n{BranchName}\r\n{CommitSha}\r\n", stateFilePath);
        }

        private void UpdateTreeFile(RepositoryFileReference[] fileReferences)
        {
            var fileLines = fileReferences.Select(fr => $"{fr.BlobSha} {fr.Size} {fr.Path}");

            directory.CreateFile(fileLines.JoinInLines(), treeFilePath);
        }

        private void DownloadFile(RepositoryFileReference fileReference)
        {
            var fileContents = Api.GetBlob(fileReference.BlobSha);
            directory.CreateFile(fileContents, fileReference.Path);
        }

        private void ParseStateFile()
        {
            var stateFileContents = directory.ReadTextFile(stateFilePath);
            var stateFileParts = stateFileContents.SplitInLines(removeEmptyEntries: false);
            this.FullRepositoryName = stateFileParts[0];
            this.BranchName = stateFileParts[1];
            this.CommitSha = stateFileParts[2];
        }

        public static void PrintLine(string value)
        {
            Printer.PrintLine(value);
        }

        public LocalRepositoryChanges GetLocalChanges()
        {
            var remoteFiles = directory.FileExists(treeFilePath)
                ? ParseTreeFile().Select(f => f.Path)
                : new string[0];
            var addedFiles = new List<string>();
            var modifiedFiles = new List<string>();
            var allLocalFiles = new List<string>();

            DoForAllLocalFiles(file =>
            {
                allLocalFiles.Add(file);

                if (!remoteFiles.Contains(file))
                    addedFiles.Add(file);
                else if (directory.IsModified(file))
                    modifiedFiles.Add(file);
            });

            var deletedFiles = remoteFiles.Except(allLocalFiles);

            return new LocalRepositoryChanges()
            {
                AddedFiles = addedFiles.ToArray(),
                ModifiedFiles = modifiedFiles.ToArray(),
                DeletedFiles = deletedFiles.ToArray()
            };
        }

        private void DoForAllLocalFiles(Action<string> action)
        {
            directory.DoForAllFiles(file =>
            {
                if (!file.StartsWith(databaseDirectoryWithSlash))
                    action(file);

                return true;
            });
        }
    }
}
