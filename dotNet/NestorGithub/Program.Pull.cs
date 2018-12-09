using System;
using System.Linq;

namespace Konamiman.NestorGithub
{
    partial class Program
    {
        #pragma warning disable 414

        static readonly string pullCommandLine = "ngh pull [-a|-l|-o]";

        static readonly string pullCommandExplanation =
@"Pull changes from the remote repository.
-a: Ask what to do on conflict (default)
-l: Keep local changes on conflict
-r: Overwrite local changes on conflict";

        void PullCommand(string[] args)
        {
            #pragma warning restore 414

            PullConflictStrategy conflictStrategy = PullConflictStrategy.Ask;
            if(args.Length > 0)
            {
                if (IsFlagParam(args[0], "l"))
                    conflictStrategy = PullConflictStrategy.KeepLocal;
                else if (IsFlagParam(args[0], "r"))
                    conflictStrategy = PullConflictStrategy.OverWriteWithRemote;
                else if (IsFlagParam(args[0], "a"))
                    throw BadParameter($"Unknown parameter '{args[0]}'");
            }

            var localRepository = GetExistingLocalRepository();

            localRepository.Pull(conflictStrategy);

            UI.Print($"Your local repository is now up to date with {localRepository.FullRepositoryName}");
        }
    }
}
