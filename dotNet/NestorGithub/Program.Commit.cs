namespace Konamiman.NestorGithub
{
    partial class Program
    {
#pragma warning disable 414

        static readonly string commitCommandLine = "ngh commit <message>";

        static readonly string commitCommandExplanation =
@"Commits and pushes local changes.
Also resets the 'Archive' bit of all the files, so they are considered unmodified.";

        void CommitCommand(string[] args)
        {
#pragma warning restore 414

            if (args.Length > 1)
                throw BadParameter("Please specify the commit message in quotes if it contains spaces");

            var commitMessage = args[0];
            var localRepository = GetExistingLocalRepository();

            localRepository.Commit(Configuration.AuthorName, Configuration.AuthorEmail, commitMessage);

            UI.Print("Commit completed successfully.");
        }
    }
}
