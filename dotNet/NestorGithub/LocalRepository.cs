using System;
using System.Linq;

namespace Konamiman.NestorGithub
{
    class LocalRepository
    {
        const string databaseDirectory = ".ngh";
        const string stateFileName = "state";
        const string treeFileName = "tree";

        private readonly FilesystemDirectory directory;
        private readonly ApiClient api;
        private readonly string stateFilePath;
        private readonly string treeFilePath;

        private string fullRepositoryName;
        private string branchName;
        private string commitSha;

        public LocalRepository(FilesystemDirectory directory, ApiClient api)
        {
            this.directory = directory;
            this.api = api;

            this.stateFilePath = FilesystemDirectory.CombinePath(databaseDirectory, stateFileName);
            this.treeFilePath = FilesystemDirectory.CombinePath(databaseDirectory, treeFileName);
        }

        public bool HasContents => directory.HasContents;

        public bool IsInitialized => directory.DirectoryExists(databaseDirectory);

        public void Clone(string fullRepositoryName, bool linkOnly)
        {
            this.fullRepositoryName = fullRepositoryName;
            directory.CreateIfNotExists();

            bool alreadyInitialized = false;
            if (this.IsInitialized)
            {
                ParseStateFile();
                if (linkOnly || (fullRepositoryName != this.fullRepositoryName))
                {
                    throw new InvalidOperationException($"The repository at '{directory.PhysicalPath}' is already initialized for repository {this.fullRepositoryName}");
                }
                alreadyInitialized = true;
            }
            else if (!linkOnly && directory.HasContents)
            {
                throw new InvalidOperationException($"The target directory '{directory.PhysicalPath}' is not empty");
            }

            RepositoryFileReference[] treeFiles = null;

            if (alreadyInitialized)
            {
                PrintLine($"Respository {fullRepositoryName} already initialized, continuing the clone process\r\n");
                treeFiles = ParseTreeFile();
            }
            else
            {
                PrintLine("Getting repository information...");

                branchName = "master";
                string branchCommitSha = null;

                try
                {
                    branchCommitSha = api.GetBranchCommitSha(fullRepositoryName, branchName);
                    if (branchCommitSha == null)
                    {
                        var branches = api.GetBranchNames(fullRepositoryName);
                        branchName = branches.First();
                        branchCommitSha = api.GetBranchCommitSha(fullRepositoryName, branchName);
                    }
                }
                catch (ApiException ex) when (linkOnly && ex.StatusCode == 409)
                {
                    //409 = no commits exist, whe don't care when just linking
                }

                if (branchCommitSha == null)
                {
                    UpdateStateFile(fullRepositoryName, branchName, null);
                }
                else
                {
                    var treeSha = api.GetCommitData(fullRepositoryName, branchCommitSha);
                    treeFiles = api.GetTreeFiles(fullRepositoryName, branchCommitSha);

                    UpdateStateFile(fullRepositoryName, branchName, branchCommitSha);
                    UpdateTreeFile(treeFiles);
                }
            }

            if (linkOnly)
            {
                PrintLine($"{directory.PhysicalPath} has been linked to {fullRepositoryName} on branch {branchName}");
            }
            else
            {
                PrintLine($"Getting files for branch {branchName}");
                foreach (var file in treeFiles)
                {
                    if (directory.FileExists(file.Path))
                    {
                        PrintLine($"  {file.Path} - already exists, skipping");
                    }
                    else
                    {
                        PrintLine($"  {file.Path} ...");
                        DownloadFile(file);
                    }
                }

                PrintLine($"\r\nRepository {fullRepositoryName} has been cloned at {directory.PhysicalPath}");
            }
        }

        RepositoryFileReference[] ParseTreeFile()
        {
            return directory
                .ReadTextFile(treeFilePath)
                .SplitInLines()
                .Select(line => {
                    var parts = line.SplitBySpace();
                    return new RepositoryFileReference { BlobSha = parts[0], Path = parts[1] };
                })
                .ToArray();
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
            PrintLine($"Directory {directory.PhysicalPath} has been unlinked from remote repository {fullRepositoryName}");
        }

        private void UpdateStateFile(string fullRespositoryName, string branchName, string branchSha)
        {
            directory.CreateFile($"{fullRespositoryName}\r\n{branchName}\r\n{branchSha}\r\n", stateFilePath);
        }

        private void UpdateTreeFile(RepositoryFileReference[] fileReferences)
        {
            var fileLines = fileReferences.Select(fr => $"{fr.BlobSha} {fr.Path}");

            directory.CreateFile(fileLines.JoinInLines(), treeFilePath);
        }

        private void DownloadFile(RepositoryFileReference fileReference)
        {
            var fileContents = api.GetBlob(fullRepositoryName, fileReference.BlobSha);
            directory.CreateFile(fileContents, fileReference.Path);
        }

        private void ParseStateFile()
        {
            var stateFileContents = directory.ReadTextFile(stateFilePath);
            var stateFileParts = stateFileContents.SplitInLines(removeEmptyEntries: false);
            this.fullRepositoryName = stateFileParts[0];
            this.branchName = stateFileParts[1];
            this.commitSha = stateFileParts[2];
        }

        public static void PrintLine(string value)
        {
            Printer.PrintLine(value);
        }
    }
}
