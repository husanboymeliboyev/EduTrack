using System.Text;
using EduTrack.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace EduTrack.Tests
{
    // Yuklangan fayllar qayerda saqlanishi (deploy'dan keyin ham qolishi) va
    // path-traversal himoyasi uchun testlar.
    public class UploadStorageTests
    {
        private static IConfiguration Config(params (string Key, string Value)[] values)
        {
            var dict = values.ToDictionary(v => v.Key, v => (string?)v.Value);
            return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        }

        private static StubEnvironment Env(string contentRoot, string webRoot) => new()
        {
            ContentRootPath = contentRoot,
            WebRootPath = webRoot
        };

        [Fact]
        public void Uploads_root_is_next_to_sqlite_database()
        {
            var contentRoot = Path.Combine(Path.GetTempPath(), "edutrack-app");
            var dbPath = Path.Combine(Path.GetTempPath(), "edutrack-data", "EduTrack.db");

            var root = UploadStorage.ResolveRoot(
                Env(contentRoot, Path.Combine(contentRoot, "wwwroot")),
                Config(("ConnectionStrings:DefaultConnection", $"Data Source={dbPath}")));

            Assert.Equal(
                Path.Combine(Path.GetDirectoryName(dbPath)!, "uploads"),
                root.TrimEnd(Path.DirectorySeparatorChar));
        }

        [Fact]
        public void Uploads_root_can_be_configured_explicitly()
        {
            var custom = Path.Combine(Path.GetTempPath(), "edutrack-custom-uploads");

            var root = UploadStorage.ResolveRoot(
                Env(Path.Combine(Path.GetTempPath(), "edutrack-app"), Path.Combine(Path.GetTempPath(), "edutrack-app", "wwwroot")),
                Config((UploadStorage.ConfigurationKey, custom)));

            Assert.Equal(custom, root.TrimEnd(Path.DirectorySeparatorChar));
        }

        [Fact]
        public void Relative_uploads_root_is_resolved_against_content_root()
        {
            var contentRoot = Path.Combine(Path.GetTempPath(), "edutrack-app");

            var root = UploadStorage.ResolveRoot(
                Env(contentRoot, Path.Combine(contentRoot, "wwwroot")),
                Config((UploadStorage.ConfigurationKey, "persistent/uploads")));

            Assert.Equal(
                Path.Combine(contentRoot, "persistent", "uploads"),
                root.TrimEnd(Path.DirectorySeparatorChar));
        }

        [Fact]
        public void In_memory_database_falls_back_to_webroot_uploads()
        {
            var contentRoot = Path.Combine(Path.GetTempPath(), "edutrack-app");
            var webRoot = Path.Combine(contentRoot, "wwwroot");

            var root = UploadStorage.ResolveRoot(
                Env(contentRoot, webRoot),
                Config(("ConnectionStrings:DefaultConnection", "Data Source=:memory:")));

            Assert.Equal(Path.Combine(webRoot, "uploads"), root.TrimEnd(Path.DirectorySeparatorChar));
        }

        [Fact]
        public void Stored_uploads_prefix_is_stripped()
        {
            var root = Path.Combine(Path.GetTempPath(), "edutrack-uploads");

            var resolved = UploadStorage.ResolveFile(root, "uploads/abc_file.pdf");

            Assert.Equal(Path.Combine(root, "abc_file.pdf"), resolved);
        }

        [Theory]
        [InlineData("../secret.txt")]
        [InlineData("uploads/../../secret.txt")]
        [InlineData("..\\..\\secret.txt")]
        public void Path_traversal_is_rejected(string relativePath)
        {
            var root = Path.Combine(Path.GetTempPath(), "edutrack-uploads");

            Assert.Null(UploadStorage.ResolveFile(root, relativePath));
        }

        [Fact]
        public void Empty_or_missing_path_is_rejected()
        {
            var root = Path.Combine(Path.GetTempPath(), "edutrack-uploads");

            Assert.Null(UploadStorage.ResolveFile(root, null));
            Assert.Null(UploadStorage.ResolveFile(root, "   "));
        }

        [Fact]
        public async Task Upload_writes_into_configured_root_and_exists_returns_true()
        {
            var contentRoot = Path.Combine(Path.GetTempPath(), "edutrack-upload-test");
            var webRoot = Path.Combine(contentRoot, "wwwroot");
            var storageRoot = Path.Combine(contentRoot, "data", "uploads");

            var service = new FileUploadService(
                Env(contentRoot, webRoot),
                Config((UploadStorage.ConfigurationKey, storageRoot)));

            var bytes = Encoding.UTF8.GetBytes("def solve():\n    return 42\n");
            var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "yechim.py");

            var result = await service.UploadAsync(file, 10 * 1024 * 1024);

            try
            {
                Assert.True(result.Success);
                Assert.Equal("yechim.py", result.OriginalFileName);

                var physicalPath = service.ResolvePath(result.RelativePath);
                Assert.NotNull(physicalPath);
                Assert.StartsWith(storageRoot, physicalPath!);
                Assert.True(File.Exists(physicalPath));
                Assert.True(service.Exists(result.RelativePath));

                // Eski ko'rinishdagi ("uploads/...") yo'l ham topilishi kerak.
                Assert.True(service.Exists("uploads/" + Path.GetFileName(physicalPath)));

                await service.DeleteAsync(result.RelativePath);
                Assert.False(service.Exists(result.RelativePath));
            }
            finally
            {
                if (Directory.Exists(contentRoot))
                {
                    Directory.Delete(contentRoot, recursive: true);
                }
            }
        }

        [Fact]
        public async Task Unknown_extension_is_rejected()
        {
            var contentRoot = Path.Combine(Path.GetTempPath(), "edutrack-upload-test2");
            var service = new FileUploadService(
                Env(contentRoot, Path.Combine(contentRoot, "wwwroot")),
                Config((UploadStorage.ConfigurationKey, Path.Combine(contentRoot, "uploads"))));

            var bytes = new byte[] { 1, 2, 3 };
            var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "virus.exe");

            try
            {
                var result = await service.UploadAsync(file, 10 * 1024 * 1024);

                Assert.False(result.Success);
                Assert.Contains("Ruxsat etilmagan fayl turi", result.ErrorMessage);
            }
            finally
            {
                if (Directory.Exists(contentRoot))
                {
                    Directory.Delete(contentRoot, recursive: true);
                }
            }
        }

        private sealed class StubEnvironment : IWebHostEnvironment
        {
            public string ApplicationName { get; set; } = "EduTrack";
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string WebRootPath { get; set; } = string.Empty;
            public string EnvironmentName { get; set; } = "Test";
            public string ContentRootPath { get; set; } = string.Empty;
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }
    }
}
