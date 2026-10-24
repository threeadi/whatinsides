using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace WhatInsides.Services;

public static class DirectLockService
{
    public const string SAFE_EXTENSION = ".wisafe";

    /// <summary>
    /// Encrypts an individual file into a .wisafe file.
    /// </summary>
    public static async Task<string> EncryptDirectFileAsync(
        string sourcePath,
        string password,
        bool shredOriginal,
        int shredPasses = 1,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("File tidak ditemukan", sourcePath);

        string outputPath = sourcePath + SAFE_EXTENSION;
        if (File.Exists(outputPath))
        {
            outputPath = Path.Combine(
                Path.GetDirectoryName(sourcePath)!,
                $"{Path.GetFileName(sourcePath)}_{DateTime.Now:yyyyMMdd_HHmmss}{SAFE_EXTENSION}");
        }

        using (var inStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var outStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await CryptoEngine.EncryptStreamAsync(inStream, outStream, password, progress, cancellationToken);
        }

        if (shredOriginal)
        {
            await FileShredder.ShredFileAsync(sourcePath, shredPasses, cancellationToken);
        }

        return outputPath;
    }

    /// <summary>
    /// Packages a directory into a zip archive and encrypts it into a .wisafe file.
    /// </summary>
    public static async Task<string> EncryptDirectFolderAsync(
        string sourceDirPath,
        string password,
        bool shredOriginal,
        int shredPasses = 1,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(sourceDirPath))
            throw new DirectoryNotFoundException("Folder tidak ditemukan.");

        string parentDir = Directory.GetParent(sourceDirPath)?.FullName ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string folderName = new DirectoryInfo(sourceDirPath).Name;
        string outputPath = Path.Combine(parentDir, $"{folderName}.folder{SAFE_EXTENSION}");

        string tempZip = Path.Combine(parentDir, $".__wi_tmp_{Guid.NewGuid():N}.tmp");
        try
        {
            await Task.Run(() => ZipFile.CreateFromDirectory(sourceDirPath, tempZip, CompressionLevel.Fastest, false), cancellationToken);

            using (var inStream = new FileStream(tempZip, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var outStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await CryptoEngine.EncryptStreamAsync(inStream, outStream, password, progress, cancellationToken);
            }
        }
        finally
        {
            if (File.Exists(tempZip))
            {
                try { File.Delete(tempZip); } catch { }
            }
        }

        if (shredOriginal)
        {
            await FileShredder.ShredDirectoryAsync(sourceDirPath, shredPasses, cancellationToken);
        }

        return outputPath;
    }

    /// <summary>
    /// Decrypts a .wisafe file back to its original file or folder.
    /// </summary>
    public static async Task<string> DecryptDirectAsync(
        string encryptedFilePath,
        string password,
        string? outputDirectory = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(encryptedFilePath))
            throw new FileNotFoundException("File terenkripsi tidak ditemukan.");

        string dir = outputDirectory ?? Path.GetDirectoryName(encryptedFilePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string fileName = Path.GetFileName(encryptedFilePath);

        // Strip .wisafe
        string baseName = fileName.EndsWith(SAFE_EXTENSION, StringComparison.OrdinalIgnoreCase)
            ? fileName.Substring(0, fileName.Length - SAFE_EXTENSION.Length)
            : fileName + ".decrypted";

        bool isFolder = baseName.EndsWith(".folder", StringComparison.OrdinalIgnoreCase);
        if (isFolder)
        {
            baseName = baseName.Substring(0, baseName.Length - ".folder".Length);
            string destinationFolder = Path.Combine(dir, baseName);
            string tempZip = Path.Combine(dir, $".__wi_dec_{Guid.NewGuid():N}.tmp");

            try
            {
                using (var inStream = new FileStream(encryptedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var outStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await CryptoEngine.DecryptStreamAsync(inStream, outStream, password, progress, cancellationToken);
                }

                if (Directory.Exists(destinationFolder))
                {
                    destinationFolder = Path.Combine(dir, $"{baseName}_unlocked_{DateTime.Now:yyyyMMdd_HHmmss}");
                }

                await Task.Run(() => ZipFile.ExtractToDirectory(tempZip, destinationFolder), cancellationToken);
                return destinationFolder;
            }
            finally
            {
                if (File.Exists(tempZip)) try { File.Delete(tempZip); } catch { }
            }
        }
        else
        {
            string destinationFile = Path.Combine(dir, baseName);
            if (File.Exists(destinationFile))
            {
                string stem = Path.GetFileNameWithoutExtension(baseName);
                string ext = Path.GetExtension(baseName);
                destinationFile = Path.Combine(dir, $"{stem}_unlocked_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
            }

            using (var inStream = new FileStream(encryptedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var outStream = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await CryptoEngine.DecryptStreamAsync(inStream, outStream, password, progress, cancellationToken);
            }

            return destinationFile;
        }
    }
}
