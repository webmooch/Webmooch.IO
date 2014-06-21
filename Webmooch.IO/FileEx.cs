using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Webmooch.IO
{
    public class FileEx
    {

        // TODO: NTFS file compress / decompress attribute via WMI

        /// <summary>
        /// Creates a decompressed copy of a GZip compressed file.
        /// </summary>
        public static async Task DecompressGZip(FileInfo compressedFile, FileInfo decompressedFileToCreate)
        {
            using (var inFile = compressedFile.OpenRead())
            using (var outFile = decompressedFileToCreate.Open(FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            using (var decompressedStream = new GZipStream(inFile, CompressionMode.Decompress))
            {
                await decompressedStream.CopyToAsync(outFile);
            }
        }

        /// <summary>
        /// Creates compressed copy of file specified using GZip.
        /// </summary>
        public static async Task CompressGZip(FileInfo fileToCompress, FileInfo compressedFileToCreate)
        {
            using (var inFile = fileToCompress.OpenRead())
            using (var outFile = compressedFileToCreate.Open(FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            using (var compressStream = new GZipStream(outFile, CompressionMode.Compress))
            {
                await inFile.CopyToAsync(compressStream);
            }
        }

        /// <summary>
        /// Replaces all occurrences of the specified old string with the new string value.
        /// </summary>
        /// <returns>True if any changes were made.</returns>
        public static bool FindAndReplaceContent(FileInfo file, string oldValue, string newValue, StringComparison comparer)
        {
            var changeMade = false;
            string contents = File.ReadAllText(file.FullName);
            if (contents.Contains(oldValue, comparer))
            {
                contents = contents.Replace(oldValue, newValue, comparer);
                changeMade = true;
            }
            File.WriteAllText(file.FullName, contents);
            return changeMade;
        }

        /// <summary>
        /// Attempts to resolve the NTAccount owner name of the specified file.
        /// </summary>
        /// <returns>Owner's NTAccount name. If any exceptions occur during the NTAccount resolution then the account SID is returned.</returns>
        public static string GetOwner(FileInfo file)
        {
            var security = File.GetAccessControl(file.FullName);
            var sid = security.GetOwner(typeof(SecurityIdentifier));

            try
            {
                return sid.Translate(typeof(NTAccount)).ToString();
            }
            catch (Exception ex)
            {
                if (ex.IsCritical())
                    throw;
                else
                    return sid.ToString();
            }
        }

        /// <summary>
        /// Determines if the filename supplied matches any existing files within all system %PATH% directories.
        /// </summary>
        /// <returns>
        /// True if any match is made, otherwise false.
        /// </returns>
        public static bool ExistsInSystemPathDirectories(string fileName)
        {
            return (!string.IsNullOrWhiteSpace(FindInSystemPathDirectories(fileName)));
        }

        /// <summary>
        /// Determines if the filename supplied matches any existing files within all system %PATH% directories.
        /// </summary>
        /// <returns>
        /// Full path of first match found or null if no match is made.
        /// </returns>
        public static string FindInSystemPathDirectories(string fileName)
        {
            var paths = Environment.GetEnvironmentVariable("PATH");
            foreach (var path in paths.Split(';'))
            {
                var fullPath = Path.Combine(path, fileName);
                if (File.Exists(fullPath))
                    return fullPath;
            }
            return null;
        }

        /// <summary>
        /// Read file to string.
        /// </summary>
        public static async Task<string> ReadAsync(FileInfo file, Encoding encoding, CancellationToken token)
        {
            using (var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
            {
                var sb = new StringBuilder();
                var data = new byte[4096];
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(data, 0, data.Length, token)) != 0)
                {
                    string text = encoding.GetString(data, 0, bytesRead);
                    sb.Append(text);
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Write string data to a file.
        /// </summary>
        public static async Task WriteAsync(FileInfo file, string data, Encoding encoding, FileMode openMode, CancellationToken token)
        {
            var encodedText = encoding.GetBytes(data);
            using (var stream = new FileStream(file.FullName, openMode, FileAccess.Write, FileShare.None, 4096, true))
            {
                await stream.WriteAsync(encodedText, 0, encodedText.Length, token);
            }
        }

        /// <summary>
        /// Determines if two files are equal based on their hash.
        /// </summary>
        public static async Task<bool> FilesAreEqualAsync(FileInfo file1, FileInfo file2, HashType verificationMethod)
        {
            if (file1 == null)
                throw new ArgumentNullException("File1 cannot be null.");

            if (file2 == null)
                throw new ArgumentNullException("File2 cannot be null.");

            if (!file1.Exists)
                throw new FileNotFoundException(string.Format("File1 '{0}' does not exist.", file1.FullName));

            if (!file2.Exists)
                throw new FileNotFoundException(string.Format("File2 '{0}' does not exist.", file2.FullName));

            if (verificationMethod == HashType.NONE)
                throw new ArgumentException("A verification method is required to compare file equality.");

            return await Task.Run<bool>(() =>
            {
                string file1Hash = null;
                string file2Hash = null;

                Parallel.Invoke
                (
                    () => file1Hash = ComputeHashAsync(file1, verificationMethod).Result,
                    () => file2Hash = ComputeHashAsync(file2, verificationMethod).Result
                );

                return string.Equals(file1Hash, file2Hash, StringComparison.OrdinalIgnoreCase);
            });
        }

        /// <summary>
        /// Determines the fullname of a unique temporary file in a theoretically-writable location.
        /// </summary>
        public static FileInfo GetTemporaryFile()
        {
            var fileName = string.Format("{0}.{1}", Guid.NewGuid().ToString(), "tmp");
            var tempFile = Path.Combine(Path.GetTempPath(), fileName);
            while (File.Exists(tempFile))
            {
                return GetTemporaryFile();
            }
            return new FileInfo(tempFile);
        }

        /// <summary>
        /// Computes the specified hash value for the given file.
        /// </summary>
        public static async Task<string> ComputeHashAsync(FileInfo file, HashType hashType)
        {
            switch (hashType)
            {
                case HashType.MD5:
                    return await GenerateMD5Async(file);

                case HashType.SHA256:
                    return await GenerateSHA256Async(file);

                case HashType.SHA512:
                    return await GenerateSHA512Async(file);

                default:
                    throw new ArgumentOutOfRangeException(string.Format("Unsupported HashType: {0}", hashType.ToString()));
            }
        }

        /// <summary>
        /// Computes the MD5 hash value for the given file.
        /// </summary>
        private static async Task<string> GenerateMD5Async(FileInfo file)
        {
            return await Task.Run<string>(() =>
            {
                using (var stream = File.OpenRead(file.FullName))
                using (var md5 = MD5.Create())
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", string.Empty);
                }
            });
        }

        /// <summary>
        /// Computes the SHA256 hash value for the given file.
        /// </summary>
        private static async Task<string> GenerateSHA256Async(FileInfo file)
        {
            return await Task.Run<string>(() =>
            {
                using (var stream = File.OpenRead(file.FullName))
                using (var sha256 = new SHA256Managed())
                {
                    var hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", string.Empty);
                }
            });
        }

        /// <summary>
        /// Computes the SHA512 hash value for the given file.
        /// </summary>
        private static async Task<string> GenerateSHA512Async(FileInfo file)
        {
            return await Task.Run<string>(() =>
            {
                using (var stream = File.OpenRead(file.FullName))
                using (var sha512 = new SHA512Managed())
                {
                    var hash = sha512.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", string.Empty);
                }
            });
        }
    }
}