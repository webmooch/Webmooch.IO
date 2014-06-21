using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Webmooch.IO;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    [TestClass]
    public class FileExTests
    {
        public TestContext TestContext { get; set; }

        private static string testText = "this here is test text!@#$%^&*()_+{}:\"<>?";
        private static string testTextExpectedMD5 = "EB38185AF62EBBBF35CCE350D394D646";
        private static string testTextExpectedSHA256 = "16A042C958AE01998521A0DB29C3630B02BDCDAB9F24FED0F3CEB09A9FD6CEDD";
        private static string testTextExpectedSHA512 = "87CFFFA4ECBA8C6513C9D9F9C699309929010E6260E929EDD348F441D7E57AD3A53BF55408E8573CA1F4B660FCCF6FB4BC6547997D913F5ECB3E0A84FD1CE4F5";

        [TestMethod]
        public void CompressDecompressVerify_1MB()
        {
            var tempFile = FileEx.GetTemporaryFile();
            var compressedFile = new FileInfo(tempFile.FullName + ".gz");
            var decompressedFile = new FileInfo(tempFile.FullName + ".decompressed");

            File.WriteAllText(tempFile.FullName, Encoding.UTF8.GetString(HelperMethods.GenerateJunkByteArray(1024 * 1024)), Encoding.UTF8);
            
            FileEx.CompressGZip(tempFile, compressedFile).Wait(-1);
            
            FileEx.DecompressGZip(compressedFile, decompressedFile).Wait(-1);

            Assert.IsTrue(FileEx.FilesAreEqualAsync(tempFile, decompressedFile, HashType.MD5).Result);
            Assert.IsTrue(FileEx.FilesAreEqualAsync(tempFile, decompressedFile, HashType.SHA256).Result);
            Assert.IsTrue(FileEx.FilesAreEqualAsync(tempFile, decompressedFile, HashType.SHA512).Result);

            tempFile.Delete();
            compressedFile.Delete();
            decompressedFile.Delete();
        }

        [TestMethod]
        public void CompressAsync_1MB()
        {
            var tempFile = FileEx.GetTemporaryFile();
            File.WriteAllText(tempFile.FullName, Encoding.UTF8.GetString(HelperMethods.GenerateJunkByteArray(1024 * 1024)), Encoding.UTF8);

            var compressedFile = new FileInfo(tempFile.FullName + ".gz");
            FileEx.CompressGZip(tempFile, compressedFile).Wait(-1);

            Assert.IsTrue(compressedFile.Exists);
            Assert.IsTrue(compressedFile.Length > 0);

            tempFile.Delete();
            compressedFile.Delete();
        }

        [TestMethod]
        public void FindAndReplaceContent_CaseInsensitive()
        {
            var tempFile = FileEx.GetTemporaryFile();
            File.WriteAllText(tempFile.FullName, testText, Encoding.UTF8);
            FileEx.FindAndReplaceContent(tempFile, "IS", "oo", StringComparison.OrdinalIgnoreCase);
            Assert.AreEqual(File.ReadAllText(tempFile.FullName, Encoding.UTF8), testText.Replace("is", "oo"));
            tempFile.Delete();
        }

        [TestMethod]
        public void FindAndReplaceContent_CaseSensitive()
        {
            var tempFile = FileEx.GetTemporaryFile();
            File.WriteAllText(tempFile.FullName, testText, Encoding.UTF8);
            FileEx.FindAndReplaceContent(tempFile, "is", "oo", StringComparison.Ordinal);
            Assert.AreEqual(File.ReadAllText(tempFile.FullName, Encoding.UTF8), testText.Replace("is", "oo"));
            tempFile.Delete();
        }

        [TestMethod]
        public void GetOwner_Notepad()
        {
            var notepadPath = FileEx.FindInSystemPathDirectories("notepad.exe");
            Assert.IsFalse(string.IsNullOrWhiteSpace(FileEx.GetOwner(new FileInfo(notepadPath))));
        }

        [TestMethod]
        public void FindInSystemPaths_True()
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(FileEx.FindInSystemPathDirectories("notepad.exe")));
        }

        [TestMethod]
        public void ExistsInSystemPathDirectories_True()
        {
            Assert.IsTrue(FileEx.ExistsInSystemPathDirectories("notepad.exe"));
        }

        [TestMethod]
        public void ExistsInSystemPathDirectories_False()
        {
            Assert.IsFalse(FileEx.ExistsInSystemPathDirectories("Hopefully.this.file.name.does.not.exist.anywhere.in.your.system.path...It.would.be.weird.if.it.did."));
        }

        [TestMethod]
        public void ReadAsync_DifferentEncodings()
        {
            var tempFile = FileEx.GetTemporaryFile();
            FileEx.WriteAsync(tempFile, testText, Encoding.UTF8, FileMode.OpenOrCreate, new CancellationTokenSource().Token).Wait(-1);
            Assert.IsTrue(tempFile.Exists);
            var readData = FileEx.ReadAsync(tempFile, Encoding.UTF32, new CancellationTokenSource().Token).Result;
            Assert.AreNotEqual(readData, testText);
            tempFile.Delete();
        }

        [TestMethod]
        public void ReadAsync_Data()
        {
            var tempFile = FileEx.GetTemporaryFile();
            FileEx.WriteAsync(tempFile, testText, Encoding.UTF8, FileMode.OpenOrCreate, new CancellationTokenSource().Token).Wait(-1);
            Assert.IsTrue(tempFile.Exists);
            var readData = FileEx.ReadAsync(tempFile, Encoding.UTF8, new CancellationTokenSource().Token).Result;
            Assert.AreEqual(readData, testText);
            tempFile.Delete();
        }

        [TestMethod]
        public void WriteAsync_Data()
        {
            var tempFile = FileEx.GetTemporaryFile();
            FileEx.WriteAsync(tempFile, testText, Encoding.UTF8, FileMode.OpenOrCreate, new CancellationTokenSource().Token).Wait(-1);
            Assert.IsTrue(tempFile.Exists);
            Assert.AreEqual(File.ReadAllText(tempFile.FullName, Encoding.UTF8), testText);
            tempFile.Delete();
        }

        [TestMethod]
        [ExpectedException(typeof(AggregateException))]
        public void WriteAsync_CancelAfter()
        {
            var junkData100MB = Encoding.UTF8.GetString(HelperMethods.GenerateJunkByteArray(1024 * 1024 * 100));
            var file = FileEx.GetTemporaryFile();

            var cts = new CancellationTokenSource();
            cts.CancelAfter(1000);

            try
            {
                var task = FileEx.WriteAsync(file, junkData100MB, Encoding.UTF8, FileMode.OpenOrCreate, cts.Token);
                task.Wait(-1);
            }
            catch (AggregateException ae)
            {
                Assert.IsInstanceOfType(ae.InnerException, typeof(TaskCanceledException));
                file.Delete();
                throw;
            }
        }

        [TestMethod]
        public void FilesAreEqualAsync_True()
        {
            var file1 = FileEx.GetTemporaryFile();
            var file2 = FileEx.GetTemporaryFile();

            File.WriteAllText(file1.FullName, testText);
            File.WriteAllText(file2.FullName, testText);

            Assert.IsTrue(FileEx.FilesAreEqualAsync(file1, file2, HashType.MD5).Result);
            Assert.IsTrue(FileEx.FilesAreEqualAsync(file1, file2, HashType.SHA256).Result);
            Assert.IsTrue(FileEx.FilesAreEqualAsync(file1, file2, HashType.SHA512).Result);

            file1.Delete();
            file2.Delete();
        }

        [TestMethod]
        public void FilesAreEqualAsync_False()
        {
            var file1 = FileEx.GetTemporaryFile();
            var file2 = FileEx.GetTemporaryFile();

            File.WriteAllText(file1.FullName, testText + ".");
            File.WriteAllText(file2.FullName, testText);

            Assert.IsFalse(FileEx.FilesAreEqualAsync(file1, file2, HashType.MD5).Result);
            Assert.IsFalse(FileEx.FilesAreEqualAsync(file1, file2, HashType.SHA256).Result);
            Assert.IsFalse(FileEx.FilesAreEqualAsync(file1, file2, HashType.SHA512).Result);

            file1.Delete();
            file2.Delete();
        }

        [TestMethod]
        [ExpectedException(typeof(AggregateException))]
        public void FilesAreEqualAsync_HashTypeNone()
        {
            var fileThatExists = FileEx.GetTemporaryFile();
            File.WriteAllText(fileThatExists.FullName, "");

            try
            {
                var result = FileEx.FilesAreEqualAsync(fileThatExists, fileThatExists, HashType.NONE).Result;
            }
            catch (AggregateException ae)
            {
                Assert.IsInstanceOfType(ae.InnerException, typeof(ArgumentException));
                fileThatExists.Delete();
                throw;
            }
        }

        [TestMethod]
        public void GetTemporaryFile_DoesNotAlreadyExist()
        {
            Assert.IsFalse(FileEx.GetTemporaryFile().Exists);
        }

        [TestMethod]
        public void ComputeHashAsync_MD5()
        {
            Assert.AreEqual(testTextExpectedMD5, GenerateHashOfTemporaryTextFile(testText, HashType.MD5));
        }

        [TestMethod]
        public void ComputeHashAsync_SHA256()
        {
            Assert.AreEqual(testTextExpectedSHA256, GenerateHashOfTemporaryTextFile(testText, HashType.SHA256));
        }

        [TestMethod]
        public void ComputeHashAsync_SHA512()
        {
            Assert.AreEqual(testTextExpectedSHA512, GenerateHashOfTemporaryTextFile(testText, HashType.SHA512));
        }

        private string GenerateHashOfTemporaryTextFile(string textToWrite, HashType hashType)
        {
            var tempFile = FileEx.GetTemporaryFile();
            TestContext.WriteLine("Temporary file: {0}", tempFile.FullName);
            File.WriteAllText(tempFile.FullName, textToWrite);
            var hash = FileEx.ComputeHashAsync(tempFile, hashType).Result;
            File.Delete(tempFile.FullName);
            return hash;
        }
    }
}