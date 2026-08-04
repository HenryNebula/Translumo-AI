using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Translumo.Infrastructure.Constants;

namespace Translumo.Infrastructure.Components
{
    /// <summary>
    /// Downloads and extracts the bundled OCR components (embedded Python runtime, EasyOCR models,
    /// Tesseract tessdata) on demand. The release ships WITHOUT these (only the small prediction
    /// model is bundled); when a user enables Tesseract or EasyOCR the relevant component is fetched
    /// from the shared upstream component zip, cached locally, and the needed subfolder copied into
    /// place — the runtime equivalent of the prebuild <c>binaries_extract.bat</c>, but per component
    /// and only when actually needed. Windows OCR (and the vision feature) never call this.
    /// </summary>
    public static class ComponentsProvider
    {
        private const string COMPONENTS_ZIP_URL =
            "https://github.com/ramjke/Translumo/releases/download/v.0.8.5/_components_v.1.0.0.zip";

        private static readonly HttpClient HttpClient = new HttpClient();

        /// <summary>The OCR component bundles shipped inside the component zip.</summary>
        public enum ComponentKind
        {
            Python,
            EasyOcr,
            Tessdata
        }

        /// <summary>Maps a component to its entry path inside the zip and its target directory on disk.</summary>
        public static (string ZipEntry, string TargetDirectory) GetComponentPaths(ComponentKind kind)
        {
            return kind switch
            {
                ComponentKind.Python => ("python", Global.PythonPath),
                ComponentKind.EasyOcr => ("models/easyocr", Path.Combine(Global.ModelsPath, "easyocr")),
                ComponentKind.Tessdata => ("models/tessdata", Path.Combine(Global.ModelsPath, "tessdata")),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }

        /// <summary>True when the component's target directory already exists and is populated.</summary>
        public static bool IsComponentPresent(ComponentKind kind)
        {
            var (_, targetDir) = GetComponentPaths(kind);
            return IsDirectoryPopulated(targetDir);
        }

        /// <summary>
        /// Ensures the given component is present on disk: returns immediately if its target is
        /// already populated; otherwise downloads the shared components zip (cached), extracts it
        /// once, and copies the requested component into place.
        /// </summary>
        public static async Task EnsureComponentAsync(ComponentKind kind, CancellationToken cancellationToken = default)
        {
            if (IsComponentPresent(kind))
            {
                return;
            }

            var (zipEntry, targetDir) = GetComponentPaths(kind);
            var zipPath = await EnsureComponentsZipAsync(cancellationToken).ConfigureAwait(false);
            var extractedRoot = EnsureComponentsExtracted(zipPath);

            CopyEntry(extractedRoot, zipEntry, targetDir);
        }

        private static async Task<string> EnsureComponentsZipAsync(CancellationToken cancellationToken)
        {
            var cacheDir = Path.Combine(Global.AppPath, "ext_components");
            Directory.CreateDirectory(cacheDir);
            var zipPath = Path.Combine(cacheDir, "components.zip");

            if (IsFilePopulated(zipPath))
            {
                return zipPath;
            }

            // Streamed download so the ~400 MB zip is never held fully in memory.
            using var response = await HttpClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, COMPONENTS_ZIP_URL),
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var target = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            }

            return zipPath;
        }

        private static string EnsureComponentsExtracted(string zipPath)
        {
            var extractedRoot = Path.Combine(Global.AppPath, "ext_components", "extracted");
            if (IsDirectoryPopulated(extractedRoot))
            {
                return extractedRoot;
            }

            if (Directory.Exists(extractedRoot))
            {
                Directory.Delete(extractedRoot, recursive: true);
            }

            using var archive = new ZipArchive(File.OpenRead(zipPath), ZipArchiveMode.Read);
            archive.ExtractToDirectory(extractedRoot);

            return extractedRoot;
        }

        private static void CopyEntry(string extractedRoot, string zipEntry, string targetDir)
        {
            var sourceDir = Path.Combine(extractedRoot, zipEntry.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(sourceDir))
            {
                throw new DirectoryNotFoundException($"Component '{zipEntry}' was not found in the extracted bundle.");
            }

            Directory.CreateDirectory(targetDir);
            CopyDirectoryRecursive(sourceDir, targetDir);
        }

        private static void CopyDirectoryRecursive(string source, string target)
        {
            foreach (var file in Directory.EnumerateFiles(source))
            {
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
            }

            foreach (var dir in Directory.EnumerateDirectories(source))
            {
                var sub = Path.Combine(target, Path.GetFileName(dir));
                Directory.CreateDirectory(sub);
                CopyDirectoryRecursive(dir, sub);
            }
        }

        private static bool IsDirectoryPopulated(string path) =>
            Directory.Exists(path)
            && (Directory.GetFiles(path).Length > 0 || Directory.GetDirectories(path).Length > 0);

        private static bool IsFilePopulated(string path) =>
            File.Exists(path) && new FileInfo(path).Length > 0;
    }
}
