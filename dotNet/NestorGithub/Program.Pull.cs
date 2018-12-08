using System;
using System.Linq;

namespace Konamiman.NestorGithub
{
    partial class Program
    {
        #pragma warning disable 414

        static readonly string pullCommandLine = "ngh pull [-d <local directory>] [-a|-l|-o]";

        static readonly string pullCommandExplanation =
@"Pull changes from the remote repository.
-a: Ask what to do on conflict (default)
-l: Keep local changes on conflict
-r: Overwrite local changes on conflict";

        void PullCommand(string[] args)
        {
            #pragma warning restore 414

            string directoryPath = null;
            if (args.Length > 0 && args[0].Equals("-d", StringComparison.InvariantCultureIgnoreCase))
            {
                directoryPath = args[1];
                args = args.Skip(2).ToArray();
            }

            PullConflictStrategy conflictStrategy = PullConflictStrategy.Ask;
            if(args.Length > 0)
            {
                if (args[0].Equals("-l", StringComparison.InvariantCultureIgnoreCase))
                    conflictStrategy = PullConflictStrategy.KeepLocal;
                else if (args[0].Equals("-r", StringComparison.InvariantCultureIgnoreCase))
                    conflictStrategy = PullConflictStrategy.OverWriteWithRemote;
                else if (!args[0].Equals("-a", StringComparison.InvariantCultureIgnoreCase))
                    throw BadParameter($"Unknown parameter '{args[0]}'");
            }

            var directory = new FilesystemDirectory(directoryPath);
            var localRepository = GetExistingLocalRepository(directory);

            localRepository.Pull(conflictStrategy);

            Print($"Your local repository is now up to date with {localRepository.FullRepositoryName}");
        }
    }
}
