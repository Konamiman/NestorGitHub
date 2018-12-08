using System;
using System.Linq;

namespace Konamiman.NestorGithub
{
    partial class Program
    {
        #pragma warning disable 414

        static readonly string commitCommandLine = "ngh commit [-d <local directory>] <message>";

        static readonly string commitCommandExplanation = "Commits and pushes local changes.";

        void CommitCommand(string[] args)
        {
            #pragma warning restore 414

            string directoryPath = null;
            if(args.FirstOrDefault().Equals("-d", StringComparison.InvariantCultureIgnoreCase))
            {
                directoryPath = args[1];
                args = args.Skip(2).ToArray();
            }

            if (args.Length > 1)
                throw BadParameter("Please specify the commit message in quotes if it contains spaces");

            var commitMessage = args[0];
            var directory = new FilesystemDirectory(directoryPath);
            var localRepository = GetExistingLocalRepository(directory);

            localRepository.Commit(Configuration.AuthorName, Configuration.AuthorEmail, commitMessage);

            Print("Commit completed successfully.");
        }
    }
}
