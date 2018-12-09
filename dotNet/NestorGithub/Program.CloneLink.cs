using System.Linq;

namespace Konamiman.NestorGithub
{
    partial class Program
    {
        #pragma warning disable 414

        static readonly string cloneCommandLine = "ngh clone [<owner>/]<repository name>";

        static readonly string cloneCommandExplanation =
@"Creates and links a local repository from the contents of a remote repository in the current local directory.
Default owner is the configured GitHub user name.";
  
        void CloneCommand(string[] args) => Clone(args, false);

        static readonly string linkCommandLine = "ngh link [<owner>/]<repository name>";

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
            var api = GetApi(suppliedRepositoryName);

            var repositoryName = api.GetProperlyCasedRepositoryName();
            if(repositoryName == null)
                throw BadParameter($"There's no repository named {suppliedRepositoryName} in GitHub (visible to you, at least)");

            var localRepository = new LocalRepository(null, api, allowNonLinkedDirectory: true);

            if (linkOnly)
            {
                localRepository.Link(repositoryName);
                UI.Print($"{localRepository.LocalPath} has been linked to {repositoryName} on branch {localRepository.LocalBranchName}");
            }
            else
            {
                localRepository.Clone(repositoryName);
                UI.Print($"\r\nRepository {repositoryName} has been cloned at {localRepository.LocalPath}");
            }
        }
    }
}
