namespace Konamiman.NestorGithub
{
    partial class Program
    {
        const string explanation =
@"Usage:

  ngh new [-p] <repository name> [<repository description>]

Creates a new repository in your GitHub account.
-p creates a private repository, you need a paid GitHub acount for that.


  ngh destroy [<owner>/]<repository name>

Destroys a repository in your GitHub account.
Be careful, this can't be undone!


  ngh clone [<owner>/]<repository name> [<local directory>]

Creates and links a local repository from the contents of a remote repository.
Default owner is the configured GitHub user name.
Default local directory is the current directory. If it exists it must be empty,
if not it will be created.


  ngh link [<owner>/]<repository name> [<local directory>]

Same as clone, but the local directory doesn't need to be empty
and no files are downloaded.


  ngh unlink [<local directory>]

Unlinks the local directory from the remote repository.
The directory contents are kept untouched.
";
    }
}
