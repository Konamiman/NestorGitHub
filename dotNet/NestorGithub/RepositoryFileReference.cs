namespace Konamiman.NestorGithub
{
    class RepositoryFileReference
    {
        public string Path { get; set; }
        public long Size { get; set; }
        public string BlobSha { get; set; }

        public override string ToString()
        {
            return $"{Path} - {BlobSha}";
        }
    }
}
