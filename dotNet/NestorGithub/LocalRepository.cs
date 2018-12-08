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
                treeFiles = GetRemoteFileReferences();

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
            var localState = GetLocalState();

            if (!localState.HasChanges)
                throw new InvalidOperationException("No local changes, nothing to commit");

            PrintLine("Checking state of remote repository...");

            if (!ExistsRemotely())
                throw new InvalidOperationException("The remote repository doesn't exist!");

            if (!BranchExistsRemotely())
            {
                if(Api.GetBranchCount() > 0)
                    throw new InvalidOperationException($"Branch {BranchName} doesn't exist remotely!");

                PrintLine("Creating initial empty commit...");
                CommitSha = CreateInitialEmptyCommit(authorName, authorEmail);
            }
            else if (!IsUpToDateWithRemote())
                throw new InvalidOperationException($"Your local repository isn't up to date with the remote repository, you need to pull before you can commit");

            var addedAndModifiedFiles = localState.AddedFiles.Union(localState.ModifiedFiles).ToArray();

            var addedAndModifiedFileReferences = new List<RepositoryFileReference>();
            if (addedAndModifiedFiles.Length > 0)
            {
                PrintLine("Pushing new and changed files...");
                foreach (var filePath in addedAndModifiedFiles)
                {
                    PrintLine($"  {filePath}...");
                    var fileContents = directory.GetFileContents(filePath);
                    var fileSha = Api.CreateBlob(fileContents);
                    addedAndModifiedFileReferences.Add(new RepositoryFileReference { Path = filePath, Size = fileContents.Length, BlobSha = fileSha });
                }
            }

            var currentCommitFileReferences = ParseTreeFile();
            var allChangedFiles = localState.AllChangedFiles;
            var unchangedFileReferences = currentCommitFileReferences.Where(r => !allChangedFiles.Contains(r.Path)).ToArray();

            PrintLine("Creating remote tree...");
            var treeSha = Api.CreateTree(unchangedFileReferences.Union(addedAndModifiedFileReferences).ToArray());

            PrintLine("Creating commit...");
            var newCommitSha = Api.CreateCommit(commitMessage, treeSha, CommitSha, authorName, authorEmail);

            PrintLine("Updating branch reference...");
            Api.SetBranchCommitSha(BranchName, newCommitSha);

            PrintLine("Updating local state...");

            CommitSha = newCommitSha;
            var currentLocalFileReferences = unchangedFileReferences.Union(addedAndModifiedFileReferences).ToArray();
            UpdateTreeFile(currentLocalFileReferences);
            UpdateStateFile();
            DoForAllLocalFiles(file => { directory.SetUnmodified(file); });
        }

        public void Pull(PullConflictStrategy conflictStrategy)
        {
            UI.PrintLine("Checking remote status...");

            if (!ExistsRemotely())
                throw new InvalidOperationException("The remote repository doesn't exist!");

            if (!BranchExistsRemotely())
                throw new InvalidOperationException($"Branch '{BranchName}' doesn't exist remotely!");

            var remoteCommitSha = Api.GetBranchCommitSha(BranchName);
            if (remoteCommitSha == CommitSha)
                throw new InvalidOperationException("Your local repository is up to date with remote, nothing to pull");

            UI.PrintLine("Calculating changes...");

            var newRemoteFileReferences = GetRemoteFileReferences(remoteCommitSha);
            var oldRemoteFileReferences = ParseTreeFile();
            var remoteState = CalculateRemoteState(oldRemoteFileReferences, newRemoteFileReferences);
            var localState = GetLocalState();

            var filenamesToDownload = remoteState.AddedFiles.Union(remoteState.ModifiedFiles).ToList();
            var filenamesToDeleteLocally = remoteState.DeletedFiles.ToList();

            //* Conflict type #1: file was modified both remotely and locally

            var filenamesModifiedRemotelyAndLocally = remoteState.ModifiedFiles.Intersect(localState.ModifiedFiles);
 
            foreach (var filename in filenamesModifiedRemotelyAndLocally)
            {
                UI.Print($"\r\n{filename} was modified both locally and remotely");
                if (conflictStrategy == PullConflictStrategy.Ask)
                {
                    UI.Print($".\r\nDo you want to keep the [L]ocal version or to overwrite it with the [R]emote version? ");
                    ConsoleKey key = UI.ReadKey(ConsoleKey.L, ConsoleKey.R);
                    UI.PrintLine("");
                    if (key == ConsoleKey.L)
                        filenamesToDownload.Remove(filename);
                }
                else if (conflictStrategy == PullConflictStrategy.KeepLocal)
                {
                    UI.PrintLine(", the local copy will be kept.");
                    filenamesToDownload.Remove(filename);
                }
                else
                {
                    UI.PrintLine(", the remote copy will be downloaded.");
                }
            }

            //* Conflict type #2: file was modified remotely but deleted locally

            var filenamesModifiedRemotelyButDeletedLocally = remoteState.ModifiedFiles.Intersect(localState.DeletedFiles);

            foreach (var filename in filenamesModifiedRemotelyButDeletedLocally)
            {
                UI.Print($"\r\n{filename} was modified remotely but deleted locally");
                if (conflictStrategy == PullConflictStrategy.Ask)
                {
                    UI.Print($".\r\nDo you want to keep the file [D]eleted or to download the [R]emote version? ");
                    ConsoleKey key = UI.ReadKey(ConsoleKey.D, ConsoleKey.R);
                    UI.PrintLine("");
                    if (key == ConsoleKey.D)
                        filenamesToDownload.Remove(filename);
                }
                else if (conflictStrategy == PullConflictStrategy.KeepLocal)
                {
                    UI.PrintLine(", the file will remain deleted.");
                    filenamesToDownload.Remove(filename);
                }
                else
                {
                    UI.PrintLine(", the remote copy will be downloaded.");
                }
            }

            //* Conflict type #3: file was deleted remotely but modified locally

            var filenamesDeletedRemotelyButModifiedLocally = remoteState.DeletedFiles.Intersect(localState.ModifiedFiles);

            foreach (var filename in filenamesDeletedRemotelyButModifiedLocally)
            {
                UI.Print($"\r\n{filename} was deleted remotely but modified locally");
                if (conflictStrategy == PullConflictStrategy.Ask)
                {
                    UI.Print($".\r\nDo you want to [D]elete the local file or [K]eep it? ");
                    ConsoleKey key = UI.ReadKey(ConsoleKey.D, ConsoleKey.K);
                    UI.PrintLine("");
                    if (key == ConsoleKey.K)
                        filenamesToDeleteLocally.Remove(filename);
                }
                else if (conflictStrategy == PullConflictStrategy.KeepLocal)
                {
                    filenamesToDeleteLocally.Remove(filename);
                    UI.PrintLine(", the local copy will be kept.");
                }
                else
                {
                    UI.PrintLine(", the local copy will be deleted.");
                }
            }

            //* Conflict type #4: file was added remotely and locally

            var filenamesAddedRemotelyAndLocally = remoteState.AddedFiles.Intersect(localState.AddedFiles);

            foreach (var filename in filenamesAddedRemotelyAndLocally)
            {
                UI.Print($"\r\n{filename} was added both locally and remotely");
                if (conflictStrategy == PullConflictStrategy.Ask)
                {
                    UI.Print($".\r\nDo you want to keep the [L]ocal file or to download the [R]emote version? ");
                    ConsoleKey key = UI.ReadKey(ConsoleKey.L, ConsoleKey.R);
                    UI.PrintLine("");
                    if (key == ConsoleKey.L)
                        filenamesToDownload.Remove(filename);
                }
                else if (conflictStrategy == PullConflictStrategy.KeepLocal)
                {
                    filenamesToDownload.Remove(filename);
                    UI.PrintLine(", the local copy will be kept.");
                }
                else
                {
                    UI.PrintLine(", the remote copy will be downloaded.");
                }
            }

            UI.PrintLine("");

            var newRemoteFileReferencesByName = newRemoteFileReferences.ToDictionary(r => r.Path);
            foreach (var filename in filenamesToDownload)
            {
                UI.PrintLine($"Downloading {filename} ...");
                var fileContents = Api.GetBlob(newRemoteFileReferencesByName[filename].BlobSha);
                directory.CreateFile(fileContents, filename);
                directory.SetUnmodified(filename);
            }

            foreach(var filename in filenamesToDeleteLocally)
            {
                UI.PrintLine($"Deleting {filename} ...");
                directory.DeleteFile(filename);
            }

            CommitSha = remoteCommitSha;
            UpdateStateFile();
            UpdateTreeFile(newRemoteFileReferences);

            UI.PrintLine("");
        }

        private RepositoryState CalculateRemoteState(RepositoryFileReference[] oldReferences, RepositoryFileReference[] newReferences)
        {
            var oldReferencesByName = oldReferences.ToDictionary(r => r.Path);
            var newReferencesByName = newReferences.ToDictionary(r => r.Path);
            var newFilenames = newReferencesByName.Keys;
            var oldFilenames = oldReferencesByName.Keys;

            var addedFilenames = newFilenames.Except(oldFilenames);
            var deletedFilenames = oldFilenames.Except(newFilenames);
            var commonFilenames = newFilenames.Except(addedFilenames);
            var modifiedFilenames = commonFilenames.Where(f => oldReferencesByName[f].BlobSha != newReferencesByName[f].BlobSha);

            return new RepositoryState
            {
                AddedFiles = addedFilenames.ToArray(),
                ModifiedFiles = modifiedFilenames.ToArray(),
                DeletedFiles = deletedFilenames.ToArray(),
                UnchangedFiles = commonFilenames.Except(modifiedFilenames).ToArray()
            };
        }

        private RepositoryFileReference[] GetRemoteFileReferences(string commitSha = null)
        {
            var remoteTreeSha = Api.GetCommitTreeSha(commitSha ?? this.CommitSha);
            return Api.GetTreeFileReferences(remoteTreeSha);
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

        private RepositoryFileReference[] ParseTreeFile()
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

        private static void PrintLine(string value)
        {
            UI.PrintLine(value);
        }

        public RepositoryState GetLocalState()
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

            return new RepositoryState()
            {
                AddedFiles = addedFiles.ToArray(),
                ModifiedFiles = modifiedFiles.ToArray(),
                DeletedFiles = deletedFiles.ToArray(),
                UnchangedFiles = allLocalFiles.Except(addedFiles).Except(modifiedFiles).Except(deletedFiles).ToArray()
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
