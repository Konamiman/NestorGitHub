using System.Linq;

namespace Konamiman.NestorGithub
{
    partial class Program
    {
        #pragma warning disable 414

        static readonly string cloneCommandLine = "ngh clone [<owner>/]<repository name> [<local directory>]";

        static readonly string cloneCommandExplanation =
@"Creates and links a local repository from the contents of a remote repository.
Default owner is the configured GitHub user name.
Default local directory is the current directory. If it exists it must be empty,
if not it will be created.";
  
        void CloneCommand(string[] args) => Clone(args, false);

        static readonly string linkCommandLine = "ngh link [<owner>/]<repository name> [<local directory>]";

        static readonly string linkCommandExplanation =
@"Same as clone, but the local directory doesn't need to be empty
and no files are downloaded.";

        void LinkCommand(string[] args) => Clone(args, true);

        #pragma warning restore  414

        void Clone(string[] args, bool linkOnly)
        {
            if (args.Length == 0)
                throw BadParameter("Repository name is required");

            var suppliedRepositoryName = FullRepositoryName(args[0]);
            var directory = new FilesystemDirectory(args.ElementAtOrDefault(1));
            var api = GetApi(suppliedRepositoryName);

            var repositoryName = api.GetProperlyCasedRepositoryName();
            if(repositoryName == null)
                throw BadParameter($"There's no repository named {suppliedRepositoryName} in GitHub (visible to you, at least)");

            var localRepository = new LocalRepository(directory, api, allowNonLinkedDirectory: true);

            if (linkOnly)
            {
                localRepository.Link(repositoryName);
                Print($"{directory.PhysicalPath} has been linked to {repositoryName} on branch {localRepository.BranchName}");
            }
            else
            {
                localRepository.Clone(repositoryName);
                Print($"\r\nRepository {repositoryName} has been cloned at {directory.PhysicalPath}");
            }
        }
    }
}
