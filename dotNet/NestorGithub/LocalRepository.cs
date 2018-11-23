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
        private string fullRepositoryName;

        public LocalRepository(FilesystemDirectory directory, ApiClient api, string fullRepositoryName)
        {
            this.directory = directory;
            this.api = api;
            this.fullRepositoryName = fullRepositoryName;
        }

        public bool HasContents => directory.HasContents;

        public bool IsInitialized => directory.DirectoryExists(databaseDirectory);

        public void Clone(bool linkOnly)
        {
            directory.CreateIfNotExists();

            if (this.IsInitialized)
                throw new InvalidOperationException($"The repository at '{directory.PhysicalPath}' is already initialized");

            if (!linkOnly && directory.HasContents)
                throw new InvalidOperationException($"The target directory '{directory.PhysicalPath}' is not empty");

            PrintLine("Getting repository information...");

            var branchName = "master";
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
            catch(ApiException ex) when (linkOnly && ex.StatusCode == 409)
            {
                //409 = no commits exist, whe don't care when just linking
            }

            RepositoryFileReference[] treeFiles = null;
            if (branchCommitSha == null)
            {
                UpdateStateFile(branchName, null);
            }
            else
            {
                var treeSha = api.GetCommitData(fullRepositoryName, branchCommitSha);
                treeFiles = api.GetTreeFiles(fullRepositoryName, branchCommitSha);

                UpdateStateFile(branchName, branchCommitSha);
                UpdateTreeFile(treeFiles);
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
                    PrintLine($"  {file.Path} ...");
                    DownloadFile(file);
                }

                PrintLine($"Repository {fullRepositoryName} has been cloned at {directory.PhysicalPath}");
            }
        }

        private void UpdateStateFile(string branchName, string branchSha)
        {
            directory.CreateFile($"{branchName}\r\n{branchSha}\r\n", databaseDirectory, stateFileName);
        }

        private void UpdateTreeFile(RepositoryFileReference[] fileReferences)
        {
            var fileLines = fileReferences.Select(fr => $"{fr.BlobSha} {fr.Path}");

            directory.CreateFile(fileLines.JoinInLines(), databaseDirectory, treeFileName);
        }

        private void DownloadFile(RepositoryFileReference fileReference)
        {
            var fileContents = api.GetBlob(fullRepositoryName, fileReference.BlobSha);
            directory.CreateFile(fileContents, fileReference.Path);
        }

        public static void PrintLine(string value)
        {
            Printer.PrintLine(value);
        }
    }
}
