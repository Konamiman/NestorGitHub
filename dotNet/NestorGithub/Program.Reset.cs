namespace Konamiman.NestorGithub
{
    partial class Program
    {
        #pragma warning disable 414

        static readonly string resetCommandLine = "ngh reset <pathspec>|*";

        static readonly string resetCommandExplanation =
@"Reset the changes done in files matched by <pathspec> since the last commit.
'*' resets the entire local repository.

Deleted files can be restored only by specifying one single file,
exactly as 'ngh status' lists it (but case insensitive),
or by specifying '*'.";

        void ResetCommand(string[] args)
        {
            #pragma warning restore 414

            if (args.Length == 0)
                throw BadParameter("pathspec is required.");

            var pathspec = args[0] == "*" ? null : args[0];
            var localRepository = GetExistingLocalRepository(FilesystemDirectory.AbsoluteDirectoryOf(pathspec));
            localRepository.ResetFiles(pathspec == "*" ? null : pathspec);
        }
    }
}
