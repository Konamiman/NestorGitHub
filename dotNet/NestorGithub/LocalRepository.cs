using System;
using System.Collections.Generic;
using System.Linq;

namespace Konamiman.NestorGithub
{
    /// <summary>
    /// This class represents a local copy of a GitHub repository, and contains the logic necessary
    /// to perform the commands exposed by the aplication. It delegates to the ApiClient and FilesystemDirectory
    /// classes as appropriate.
    /// </summary>
    class LocalRepository
    {
        const string databaseDirectory = ".ngh";
        const string databaseDirectoryWithSlash = databaseDirectory + "/";
        const string stateFileName = "state";
        const string treeFileName = "tree";
        private static readonly string stateFilePath;
        private static readonly string treeFilePath;

        private readonly FilesystemDirectory Directory;

        public ApiClient Api { get; private set; }
        public string FullRepositoryName { get; private set; }
        public string LocalBranchName { get; private set; }
        public string LocalCommitSha { get; private set; }

        static LocalRepository()
        {
            stateFilePath = FilesystemDirectory.CombinePath(databaseDirectory, stateFileName);
            treeFilePath = FilesystemDirectory.CombinePath(databaseDirectory, treeFileName);
        }

        public LocalRepository(string directoryPath, ApiClient api, bool allowNonLinkedDirectory = false)
            :this(directoryPath, allowNonLinkedDirectory)
        {
            this.Api = api;

            if (IsInitialized)
                ParseStateFile();
        }

        private LocalRepository(string directoryPath, bool allowNonLinkedDirectory = false)
        {
            var directory = new FilesystemDirectory(directoryPath);
            directoryPath = directory.PhysicalPath;
            var isLinked = MoveDirectoryUpUntilRepositoryRoot(directory);
            if (isLinked)
            {
                this.FullRepositoryName = directory.ReadTextFile(stateFilePath).SplitInLines()[0];
            }
            else if (allowNonLinkedDirectory)
            {
                this.FullRepositoryName = null;
                directory = new FilesystemDirectory(directoryPath);
            }
            else
                throw new InvalidOperationException($"{directoryPath} is not linked to any remote repository");

            this.Directory = directory;
        }

        public string LocalPath => Directory.PhysicalPath;

        public bool HasContents => Directory.HasContents;

        public bool IsInitialized => Directory.DirectoryExists(databaseDirectory);

        public static string GetRepositoryNameFor(string localPath)
        {
            var repo = new LocalRepository(localPath);
            return repo.FullRepositoryName;
        }

        private static bool MoveDirectoryUpUntilRepositoryRoot(FilesystemDirectory directory)
        {
            while(true)
            {
                if (directory.FileExists(stateFilePath)) return true;
                if (!directory.MoveOneDirectoryUp()) return false;
            }
        }

        public void Clone(string fullRepositoryName)
        {
            if (IsInitialized && fullRepositoryName != this.FullRepositoryName)
            {
                throw new InvalidOperationException($"The repository at '{Directory.PhysicalPath}' is already initialized for repository {this.FullRepositoryName}");
            }
            else if (!IsInitialized && Directory.HasContents)
            {
                throw new InvalidOperationException($"The target directory '{Directory.PhysicalPath}' is not empty");
            }

            this.FullRepositoryName = fullRepositoryName;

            RepositoryFileReference[] treeFiles = null;

            if (IsInitialized)
            {
                UI.PrintLine($"Respository {fullRepositoryName} already initialized, continuing the clone process\r\n");
                treeFiles = ParseTreeFile();
            }
            else
            {
                UI.PrintLine("Getting repository information...");

                var branchCount = Api.GetBranchCount();
                if(branchCount == 0)
                    throw new InvalidOperationException($"The repository {fullRepositoryName} is empty, please use 'link' instead of 'clone'");

                LocalBranchName = Api.GetRepositoryInfo().DefaultBranch;
                LocalCommitSha = Api.GetBranchCommitSha(LocalBranchName);
                treeFiles = GetRemoteFileReferences();

                UpdateStateFile();
                UpdateTreeFile(treeFiles);
            }

            UI.PrintLine($"Getting files for branch {LocalBranchName}");
            foreach (var file in treeFiles)
            {
                if (Directory.FileExists(file.Path) && Directory.GetFileSize(file.Path) == file.Size)
                {
                    UI.PrintLine($"  {file.Path} - already exists, skipping");
                }
                else
                {
                    UI.PrintLine($"  {file.Path} ...");
                    var fileContents = Api.GetBlob(file.BlobSha);
                    Directory.CreateFile(fileContents, file.Path);
                }
            }

            DoForAllLocalFiles(file => { Directory.SetUnmodified(file); });
        }

        public void Link(string fullRepositoryName)
        {
            if (this.IsInitialized)
            {
                throw new InvalidOperationException($"The repository at '{Directory.PhysicalPath}' is already initialized for repository {this.FullRepositoryName}");
            }

            this.FullRepositoryName = fullRepositoryName;

            RepositoryFileReference[] treeFiles = null;

            UI.PrintLine("Getting repository information...");

            var branchCount = Api.GetBranchCount();
            if (branchCount == 0)
            {
                LocalBranchName = "master";
                LocalCommitSha = null;
                UpdateStateFile();
                UpdateTreeFile(new RepositoryFileReference[0]);
            }
            else
            {
                LocalBranchName = Api.GetRepositoryInfo().DefaultBranch;
                LocalCommitSha = Api.GetBranchCommitSha(LocalBranchName);
                var treeSha = Api.GetCommitTreeSha(LocalCommitSha);
                treeFiles = Api.GetTreeFileReferences(treeSha);

                UpdateStateFile();
                UpdateTreeFile(treeFiles);
            }

            DoForAllLocalFiles(file => { Directory.SetModified(file); });
        }

        public void Unlink()
        {
            if(!Directory.DirectoryExists(""))
            {
                throw new InvalidOperationException($"Directory '{Directory.PhysicalPath}' does not exist");
            }

            if (!IsInitialized)
            {
                throw new InvalidOperationException($"The target directory '{Directory.PhysicalPath}' is not linked to any remote repository");
            }

            ParseStateFile();

            Directory.DeleteDirectory(databaseDirectory);
        }

        public void Commit(string authorName, string authorEmail, string commitMessage)
        {
            UI.PrintLine("Checking local changes...");
            var localState = GetLocalState();

            if (!localState.HasChanges)
                throw new InvalidOperationException("No local changes, nothing to commit");

            UI.PrintLine("Checking state of remote repository...");

            if (!ExistsRemotely())
                throw new InvalidOperationException("The remote repository doesn't exist!");

            if (!BranchExistsRemotely())
            {
                if (Api.GetBranchCount() > 0)
                {
                    throw new InvalidOperationException($"Branch {LocalBranchName} doesn't exist remotely, you can create it with 'ngh branch -n {LocalBranchName}'");
                }

                UI.PrintLine("Creating initial empty commit...");
                LocalCommitSha = CreateInitialEmptyCommit(LocalBranchName, authorName, authorEmail);
            }
            else if (!IsUpToDateWithRemote())
                throw new InvalidOperationException($"Your local repository isn't up to date with the remote repository, you need to pull before you can commit");

            var addedAndModifiedFiles = localState.AddedFiles.Union(localState.ModifiedFiles).ToArray();

            var addedAndModifiedFileReferences = new List<RepositoryFileReference>();
            if (addedAndModifiedFiles.Length > 0)
            {
                UI.PrintLine("Pushing new and changed files...");
                foreach (var filePath in addedAndModifiedFiles)
                {
                    UI.PrintLine($"  {filePath}...");
                    var fileContents = Directory.GetFileContents(filePath);
                    var fileSha = Api.CreateBlob(fileContents);
                    addedAndModifiedFileReferences.Add(new RepositoryFileReference { Path = filePath, Size = fileContents.Length, BlobSha = fileSha });
                }
            }

            var currentCommitFileReferences = ParseTreeFile();
            var allChangedFiles = localState.AllChangedFiles;
            var unchangedFileReferences = currentCommitFileReferences.Where(r => !allChangedFiles.Contains(r.Path)).ToArray();

            UI.PrintLine("Creating remote tree...");
            var treeSha = Api.CreateTree(unchangedFileReferences.Union(addedAndModifiedFileReferences).ToArray());

            UI.PrintLine("Creating commit...");
            var newCommitSha = Api.CreateCommit(commitMessage, treeSha, LocalCommitSha, authorName, authorEmail);

            UI.PrintLine("Updating branch reference...");
            Api.SetBranchCommitSha(LocalBranchName, newCommitSha);

            UI.PrintLine("Updating local state...");

            LocalCommitSha = newCommitSha;
            var currentLocalFileReferences = unchangedFileReferences.Union(addedAndModifiedFileReferences).ToArray();
            UpdateTreeFile(currentLocalFileReferences);
            UpdateStateFile();
            DoForAllLocalFiles(file => { Directory.SetUnmodified(file); });
        }

        public void Pull(PullConflictStrategy conflictStrategy, string remoteCommitSha = null)
        {
            UI.PrintLine("Checking remote status...");

            if (!ExistsRemotely())
                throw new InvalidOperationException("The remote repository doesn't exist!");

            if (remoteCommitSha == null)
            {
                if (!BranchExistsRemotely())
                    throw new InvalidOperationException($"Branch '{LocalBranchName}' doesn't exist remotely!");

                remoteCommitSha = Api.GetBranchCommitSha(LocalBranchName);
            }

            if (remoteCommitSha == LocalCommitSha)
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
                Directory.CreateFile(fileContents, filename);
                Directory.SetUnmodified(filename);
            }

            foreach(var filename in filenamesToDeleteLocally)
            {
                UI.PrintLine($"Deleting {filename} ...");
                Directory.DeleteFile(filename);
            }

            LocalCommitSha = remoteCommitSha;
            UpdateStateFile();
            UpdateTreeFile(newRemoteFileReferences);

            UI.PrintLine("");
        }

        public void CreateRemoteBranch(string branchName, string baseBranchName, string authorName, string authorEmail)
        {
            if(BranchExistsRemotely(branchName))
            {
                throw new InvalidOperationException($"Branch '{branchName}' already exists remotely, you can switch to it with 'ngh {branchName}'");
            }

            string commitSha;
            if (baseBranchName == null)
            {
                if (!string.IsNullOrEmpty(LocalCommitSha))
                    commitSha = LocalCommitSha;
                else if (Api.GetBranchCount() > 0)
                    throw new InvalidOperationException(
@"The local repository doesn't point to any commit, please specify a base branch name. You can list the existing remote branches with 'ngh branches'.");
                else
                {
                    UI.PrintLine("Creating initial empty commit...");
                    commitSha = CreateInitialEmptyCommit(branchName, authorName, authorEmail);
                }
            }
            else if (!BranchExistsRemotely(baseBranchName))
                throw new InvalidOperationException($"Branch '{baseBranchName}' doesn't exist remotely.");
            else
                commitSha = Api.GetBranchCommitSha(baseBranchName);

            Api.CreateBranch(branchName, commitSha);
        }

        public bool DeleteRemoteBranch(string branchName)
        {
            if (!BranchExistsRemotely(branchName))
            {
                throw new InvalidOperationException($"Branch '{branchName}' doesn't exist remotely.");
            }

            if(branchName.Equals(LocalBranchName))
            {
                UI.Print($"'{branchName}' is your current local branch. Are you sure that you want to delete ir from the remote repository? (y/n) ");
                var key = UI.ReadKey(ConsoleKey.Y, ConsoleKey.N);
                if (key == ConsoleKey.N) return false;
            }

            Api.DeleteBranch(branchName);
            return true;
        }

        public void SwitchToBranch(string branchName, PullConflictStrategy conflictStrategy)
        {
            string remoteCommitSha = null;
            bool branchIsCommitSha = false;

            if (branchName.LooksLikeSha1Hash())
            {
                if (Api.GetCommitTreeSha(branchName) == null)
                {
                    throw new InvalidOperationException("No commit exists with the specified SHA hash in the remote repsitory");
                }
                else
                {
                    remoteCommitSha = branchName;
                    branchIsCommitSha = true;
                }
            }
            else
            {
                if (!BranchExistsRemotely(branchName))
                {
                    throw new InvalidOperationException($"Branch '{branchName}' doesn't exist remotely, you can create it with 'ngh -n {branchName}'");
                }

                if (branchName.Equals(LocalBranchName, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new InvalidOperationException($"Branch '{branchName}' is already the current local branch, if you want to get the latest remote version do 'ngh pull'.");
                }

                remoteCommitSha = Api.GetBranchCommitSha(branchName);
            }

            var oldLocalBranchName = LocalBranchName;
            if(!branchIsCommitSha)
                LocalBranchName = branchName;

            if(remoteCommitSha == LocalCommitSha)
            {
                UI.PrintLine($"'{oldLocalBranchName}' is up to date with '{branchName}', nothing to pull");
            }
            else
            {
                Pull(conflictStrategy, remoteCommitSha);
            }

            UpdateStateFile();
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
                DeletedFiles = deletedFilenames.ToArray()
            };
        }

        private RepositoryFileReference[] GetRemoteFileReferences(string commitSha = null)
        {
            var remoteTreeSha = Api.GetCommitTreeSha(commitSha ?? this.LocalCommitSha);
            return Api.GetTreeFileReferences(remoteTreeSha);
        }

        private string CreateInitialEmptyCommit(string branchName, string authorName, string authorEmail)
        {
            const string EmptyTreeSha = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";

            Api.CreateFileInRemoteRepository("dummy", branchName, "dummy commit", new byte[] { 0 });
            var commitSha = Api.CreateCommit("Initial commit", EmptyTreeSha, null, authorName, authorEmail);
            Api.SetBranchCommitSha(branchName, commitSha, force: true);
            return commitSha;
        }

        public bool ExistsRemotely()
        {
            return Api.RepositoryExists();
        }

        public bool BranchExistsRemotely(string branchName = null)
        {
            return Api.GetBranchCommitSha(branchName ?? LocalBranchName) != null;
        }

        public bool IsUpToDateWithRemote()
        {
            var remoteCommitSha = Api.GetBranchCommitSha(LocalBranchName);
            return remoteCommitSha == LocalCommitSha;
        }

        public void Merge(string sourceBranch, string baseBranch, string commitMessage)
        {
            if(!BranchExistsRemotely(sourceBranch))
                throw new InvalidOperationException($"Branch '{sourceBranch}' doesn't exist remotely.");

            if (!BranchExistsRemotely(baseBranch))
                throw new InvalidOperationException($"Branch '{baseBranch}' doesn't exist remotely.");

            Api.MergeBranches(sourceBranch, baseBranch, commitMessage);
        }

        private RepositoryFileReference[] ParseTreeFile()
        {
            return Directory
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
            Directory.CreateFile($"{FullRepositoryName}\r\n{LocalBranchName}\r\n{LocalCommitSha}\r\n", stateFilePath);
        }

        private void UpdateTreeFile(RepositoryFileReference[] fileReferences)
        {
            var fileLines = fileReferences.Select(fr => $"{fr.BlobSha} {fr.Size} {fr.Path}");

            Directory.CreateFile(fileLines.JoinInLines(), treeFilePath);
        }

        private void ParseStateFile()
        {
            var stateFileContents = Directory.ReadTextFile(stateFilePath);
            var stateFileParts = stateFileContents.SplitInLines(removeEmptyEntries: false);
            this.FullRepositoryName = stateFileParts[0];
            this.LocalBranchName = stateFileParts[1];
            this.LocalCommitSha = stateFileParts[2];
        }

        public RepositoryState GetLocalState()
        {
            var remoteFiles = Directory.FileExists(treeFilePath)
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
                else if (Directory.IsModified(file))
                    modifiedFiles.Add(file);
            });

            var deletedFiles = remoteFiles.Except(allLocalFiles);

            return new RepositoryState()
            {
                AddedFiles = addedFiles.ToArray(),
                ModifiedFiles = modifiedFiles.ToArray(),
                DeletedFiles = deletedFiles.ToArray()
            };
        }

        public void ResetFiles(string pathspec = null)
        {
            var localChanges = GetLocalState();
            var treeFilesByName = ParseTreeFile().ToDictionary(r => r.Path);

            void ResetFile(string path)
            {
                if(localChanges.AddedFiles.Contains(path))
                {
                    UI.PrintLine($"Deleting {path} ...");
                    Directory.DeleteFile(path);
                }
                else if(localChanges.ModifiedFiles.Contains(path) || localChanges.DeletedFiles.Contains(path))
                {
                    UI.PrintLine($"Restoring {path} ...");
                    var blobSha = treeFilesByName[path].BlobSha;
                    var fileContents = Api.GetBlob(blobSha);
                    Directory.CreateFile(fileContents, path);
                    Directory.SetUnmodified(path);
                }
            }

            if (pathspec == null)
            {
                if(!localChanges.HasChanges)
                {
                    UI.PrintLine("*** No changes match the supplied pathspec, nothing to do");
                    return;
                }

                treeFilesByName = ParseTreeFile().ToDictionary(r => r.Path);
                foreach (var file in localChanges.AllChangedFiles)
                    ResetFile(file);
            }
            else
            {
                var matchedFiles = Directory.FindFiles(pathspec).Select(f => Directory.RelativePathOf(f));
                var filesToProcess = matchedFiles.Intersect(localChanges.AllChangedFiles).ToArray();
                if (filesToProcess.Length == 0)
                {
                    var deletedFile = localChanges.DeletedFiles.FirstOrDefault(f => f.Equals(pathspec, StringComparison.InvariantCultureIgnoreCase));
                    if (deletedFile == null)
                    {
                        UI.PrintLine("*** No changes match the supplied pathspec, nothing to do");
                        return;
                    }
                    filesToProcess = new[] { deletedFile };
                }

                foreach (var file in filesToProcess)
                    ResetFile(file);
            }
        }

        private void DoForAllLocalFiles(Action<string> action)
        {
            Directory.DoForAllFiles(file =>
            {
                if (!file.StartsWith(databaseDirectoryWithSlash))
                    action(file);

                return true;
            });
        }
    }
}
