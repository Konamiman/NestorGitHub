# NestorGitHub

NestorGithub implements a (very crude and limited but functional) local Git repository without database, relying exclusively on [the GitHub API](https://developer.github.com/v3) for syncrhonization with the remote repository. It's a console application implemented in C# targetting .NET Framework 4.5.

## Warning!

This project is a toy and has been created just for fun. It's not only very limited in functionality, also it hasn't been tested beyond "seems to work on my machine". You are crazy if you use this for anything serious. Use a real Git client instead, for [Nishi](https://twitter.com/konamiman_en/status/1020258772072071168)'s sake.

## But... why?

I was reading the GitHub API reference because why not, when I saw this in [the Git Data section](https://developer.github.com/v3/git):

> This basically allows you to reimplement a lot of Git functionality over our API - by creating raw objects directly into the database and updating branch references you could technically do just about anything that Git can do without having Git installed.

So I thought... challenge accepted, let's see how true is that / how far can I actually go.

## What's implemented?

The absolute minimum functionality that allows keeping a local directory syncrhonized with a remote repository in GitHub:

- Create or destroy a repository (you need to manually clone or link after creation).
- Clone or link a repository. Clone is the same as in normal Git, link is the same but nothing is downloaded and all the local files are marked as modified (intended for brand new repositories).
- Commit changes. This also automatically pushes the commit to the remote repository.
- Pull the latest commit from the remote repository. In case of conflicts you have just two options: keep local changes, or overwrite them with the remote version.
- Create/destroy a branch (remotely), swicth to an existing remote branch. Switching also pulls, with the same limitations on conflict. Oh, and you can list remote branches too.
- Switch the local repository to a specific commit.
- Merge a branch into another - but only if there isn't any conflict.
- Reset modified files to their original states (as they were in the last commit).

## What's NOT implemented?

Everything else, including:

- Display local changes, or display any diff whatsoever
- Commit without pushing
- Manage remotes (the only remote is GitHub, of course)
- List commits ("git log")
- Merge conflict management
- Staging (you commit _all the things_)
- .gitignore (I said **all the things**)

Also no file comparisons are done and no SHA hashes are calculated at all: a local file is considered to be modified if it simply has the "Archive" bit set.

## Getting started

1. Go to [Settings - Developer Settings - Personal access token in your GitHub account](https://github.com/settings/tokens) and create a new token with _repo_ scope (and optionally, also with _delete_repo_ scope). Take note of the token.

2. Edit the `ngh.exe.config` file by filling the `appSettings` section. Use the newly created token for `GithubPasswordOrToken`. `AuthorName` and `AuthorEmail` are optional, the authenticated user details will be used by default for commits.

3. Run `ngh.exe` without parameters. You will see a list of the available commands. Run `ngh help <command>` for detailed help.

4. Create a new directory anywhere in your system, `cd` into it.

5. Choose any of the repositories in your account and run `ngh clone <repository name>`. Note that the clone will occur directly in the current directory, no subdirectory will be created as with `git clone`.

6. Verify that the repository has been actually cloned and that a `.ngh` folder has been created.

7. Do some changes in your local repository (create a file, delete another file, modify yet another file), then run `ngh status` and verify that all the changes are recognized.

8. Run `ngh commit "Commit from NestorGithub"`

9. Browse the repository in GitHub and verify that the commit is there.

You can also clone repositories from other accounts (specify the repository as `owner/repository` then), but of course you won't be able to commit to them unless you are granted permission to do so.

## Local state

Although no local Git database is created when a repository is cloned or linked, some local state is stored in the repository folder for NestorGithub to know "where we are". This state consists of two files inside the `.ngh` folder:

* `state`: contains the repository name, the current branch name and the current commit SHA.
* `tree`: contains a copy of the tree for the current commit. There's one line for each file that contains the file name, the file size and the blob SHA.

The tree file is generated for convenience, so that the GitHub API "get tree for commit" endpoint is not hit every time the commit contents needs to be compared against the local repository (e.g. for `ngh status`) or against the remote state of the branch (e.g. for `ngh pull`).

## Last but not least...

...if you like this project **[please consider donating!](http://www.konamiman.com/msx/msx-e.html#donate)** My kids need moar shoes!
