
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ionicZip = Ionic.Zip;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Core;

namespace ConsoleApp1
{
    public class FileModel
    {
        public string Name { get; set; }
        public string Path { get; set; }
        //Access timestamp
        public DateTime Atime { get; set; }
        //Modified content timestamp
        public DateTime Mtime { get; set; }
        //Change metadata  timestamp
        public DateTime Ctime { get; set; }

        public override string ToString()
        {
            return $"Name: {Path} | {Mtime}";
        }
    }
    class Program
    {
        //change file mtime
        //touch -t [[CC]YY]MMDDhhmm[.SS]
        /*
        CC – Especifica os dois primeiros dígitos do ano
        YY – Especifica os dois últimos dígitos do ano. Se o valor de YY estiver entre 70 e 99,
        o valor dos dígitos CC será considerado 19. Se o valor de YY estiver entre 00 e 37, 
        o valor dos dígitos CC será considerado 20.
        não é possível definir a data para além de 18 de janeiro de 2038.
        MM – Especifica o mês
        DD – Especifica a data
        hh – Especifica a hora
        mm – Especifica o minuto
        SS – Especifica os segundos
        */
        //touch -a -m -t 202204121555.09 tgs.txt
        static void Main(string[] args)
        {

            //192.168.2.77 
            var user = "root";
            var senha = "MDhkZXNlY3QwNQ==";
            var host = "192.168.133.13";
            var connectionInfo = new ConnectionInfo(host, user, new PasswordAuthenticationMethod(user, Convert.FromBase64String(senha)));
            connectionInfo.Encoding = Encoding.Latin1;
            var client = new SftpClient(connectionInfo);
            client.Connect();
            // /var/www/dart/intranetbrowser /var/www/teste /var/www/html /var/www/html/teste/marcos/poo

            // DownloadDirectoryAsZip2(client, "/var/www/teste", @"C:\MyCsharpProjects\fsbackup\download.zip");
            /*var local = ListZipDirectory2(@"C:\MyCsharpProjects\fsbackup\download.zip");
            Console.WriteLine($"local:\r\n{string.Join("\r\n", local.Select(x => x.ToString()).ToList())} ");

            var remote = ListRemoteDirectory(client, "/var/www/teste" );
            Console.WriteLine($"remote:\r\n{string.Join("\r\n", remote.Select(x => x.ToString()).ToList())} ");

            var diff = ListDiff(local, remote);
            Console.WriteLine($"diff:\r\n{string.Join("\r\n", diff.Select(x => x.ToString()).ToList())} ");

            DownloadFileListAsZip2(client, diff, @"C:\MyCsharpProjects\fsbackup\download_diff.zip");*/
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            DownloadDirectoryAsTar(client, "/var/www/teste", @"C:\MyCsharpProjects\fsbackup\download.tar.gz");
            watch.Stop();
            Console.WriteLine($"End Download Execution Time: {watch.Elapsed} ms");
            client.Dispose();

        }
        public static string PATHSEPARATOR = "/";

        public static void DownloadDirectory(SftpClient sftpClient, string sourcePath, string destinationPath)
        {
            //Directory.CreateDirectory(destLocalPath);
            var fileAndFolderList = sftpClient.ListDirectory(sourcePath);
            // Iterate through list of folder content
            foreach (var item in fileAndFolderList)
            {
                var dp = destinationPath + PATHSEPARATOR + item.Name;
                var sp = sourcePath + PATHSEPARATOR + item.Name;
                // Check if it is a file (not a directory).
                if (!item.IsDirectory)
                {
                    Stream fileStream = File.Create(dp);
                    sftpClient.DownloadFile(sp, fileStream);
                    fileStream.Close();
                    fileStream.Dispose();

                }
                else if (!(".".Equals(item.Name) || "..".Equals(item.Name)))
                {
                    Directory.CreateDirectory(dp);
                    DownloadDirectory(sftpClient, sp, dp);
                }

                /*if ((item.Name != ".") && (item.Name != ".."))
                {
                    string sourceFilePath = sourceRemotePath + "/" + item.Name;
                    string destFilePath = Path.Combine(destLocalPath, item.Name);
                    if (item.IsDirectory)
                    {
                        DownloadDirectory(sftpClient, sourceFilePath, destFilePath);
                    }
                    else
                    {
                        using (Stream fileStream = File.Create(destFilePath))
                        {
                            sftpClient.DownloadFile(sourceFilePath, fileStream);
                        }
                    }
                }*/
            }
        }

        public static void DownloadFileListAsZip(SftpClient sftpClient, List<FileModel> filesToDownload, string destPath)
        {
            var zip = new ionicZip.ZipFile();
            zip.UseZip64WhenSaving = ionicZip.Zip64Option.Always;
            foreach (var file in filesToDownload)
            {
                var memoryStream = File.Create(Path.GetTempFileName(), 4096, FileOptions.DeleteOnClose);
                try
                {
                    sftpClient.DownloadFile(file.Path, memoryStream);
                    memoryStream.Position = 0;
                    zip.AddEntry(file.Path, memoryStream);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Download File failed: {file.Path} | Error:{e}");
                }
            }
            zip.Save(destPath);
            zip.Dispose();
        }

        public static void DownloadFileListAsZip2(SftpClient sftpClient, List<FileModel> filesToDownload, string destPath)
        {
            using (var zipFile = new FileStream(destPath, FileMode.OpenOrCreate))
            {
                using (var archive = new ZipArchive(zipFile, ZipArchiveMode.Create))//,leaveOpen:true
                {
                    foreach (var file in filesToDownload)
                    {

                        var entry = archive.CreateEntry(file.Path);
                        Stream stream = entry.Open();
                        try
                        {
                            sftpClient.DownloadFile(file.Path, stream);

                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Download File failed: {file.Path} | Error:{e}");
                        }
                        finally
                        {
                            stream.Close();
                            stream.Dispose();
                        }
                    }
                }
            }

        }

        public static void DownloadDirectoryAsZip(SftpClient sftpClient, string sourceRemotePath, string destLocalPath)
        {
            var zip = new ionicZip.ZipFile();
            zip.UseZip64WhenSaving = ionicZip.Zip64Option.Always;
            // zip.AlternateEncodingUsage = ZipOption.AsNecessary;
            DownloadDirectoryAsZipRec(zip, sftpClient, sourceRemotePath);
            //the UseZip64WhenSaving property on the ZipFile 
            zip.Save(destLocalPath);
            zip.Dispose();
        }
        private static void DownloadDirectoryAsZipRec(ionicZip.ZipFile zip, SftpClient sftpClient, string sourceRemotePath)
        {
            try
            {
                var files = sftpClient.ListDirectory(sourceRemotePath);
                foreach (SftpFile file in files)
                {
                    if ((file.Name != ".") && (file.Name != ".."))
                    {
                        string sourceFilePath = sourceRemotePath + "/" + file.Name;
                        // string destFilePath = Path.Combine(destLocalPath, file.Name);
                        if (file.IsDirectory)
                        {
                            DownloadDirectoryAsZipRec(zip, sftpClient, sourceFilePath);
                        }
                        else
                        {
                            //var memoryStream = new MemoryStream();
                            var memoryStream = File.Create(Path.GetTempFileName(), 4096, FileOptions.DeleteOnClose);
                            try
                            {
                                //var memoryStream = sftpClient.OpenRead(sourceFilePath);
                                sftpClient.DownloadFile(sourceFilePath, memoryStream);
                                memoryStream.Position = 0;
                                zip.AddEntry(sourceFilePath, memoryStream);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"Download File failed: {sourceFilePath} | Error:{e}");
                            }

                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Download Directory failed: {sourceRemotePath} | Error:{e}");
            }
        }

        public static void DownloadDirectoryAsZip2(SftpClient sftpClient, string sourceRemotePath, string destLocalPath)
        {
            using (var zipFile = new FileStream(destLocalPath, FileMode.OpenOrCreate))
            {
                using (var archive = new ZipArchive(zipFile, ZipArchiveMode.Create))//,leaveOpen:true
                {
                    DownloadDirectoryAsZipRec2(archive, sftpClient, sourceRemotePath);
                }
            }
        }
        private static void DownloadDirectoryAsZipRec2(ZipArchive archive, SftpClient sftpClient, string sourceRemotePath)
        {
            try
            {
                var files = sftpClient.ListDirectory(sourceRemotePath);
                foreach (var file in files)
                {
                    if ((file.Name != ".") && (file.Name != ".."))
                    {
                        var sourceFilePath = sourceRemotePath + "/" + file.Name;

                        if (file.IsDirectory)
                        {
                            DownloadDirectoryAsZipRec2(archive, sftpClient, sourceFilePath);
                        }
                        else
                        {
                            var entry = archive.CreateEntry(sourceFilePath);
                            Stream stream = entry.Open();
                            try
                            {
                                sftpClient.DownloadFile(sourceFilePath, stream);
                                // entry.LastWriteTime = file.LastWriteTime;                               
                                // using (var entryStream = entry.Open())
                                // using (var fileStream = sftpClient.OpenRead(sourceFilePath))
                                // {
                                //fileStream.CopyTo(entryStream);
                                //}
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"Download File failed: {sourceFilePath} | Error:{e}");
                            }
                            finally
                            {
                                stream.Close();
                                stream.Dispose();
                            }

                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Download Directory failed: {sourceRemotePath} | Error:{e}");
            }
        }

        public static void DownloadDirectoryAsTar(SftpClient sftpClient, string sourceRemotePath, string destLocalPath)
        {
            using (var zipFile = new FileStream(destLocalPath, FileMode.OpenOrCreate))
            {
                using (var gzoStream = new GZipOutputStream(zipFile))
                using (var tarOutputStream = new TarOutputStream(gzoStream, null))
                using (var tarArchive = TarArchive.CreateOutputTarArchive(tarOutputStream))
                {
                    tarArchive.RootPath = sourceRemotePath;                   
                    DownloadDirectoryAsTarRec(tarOutputStream, sftpClient, sourceRemotePath);
                    tarArchive.Close();
                }
            }
        }
        private static void DownloadDirectoryAsTarRec(TarOutputStream tarOut, SftpClient sftpClient, string sourceRemotePath)
        {
            try
            {
                var files = sftpClient.ListDirectory(sourceRemotePath);
                foreach (var file in files)
                {
                    if ((file.Name != ".") && (file.Name != ".."))
                    {
                        var sourceFilePath = sourceRemotePath + "/" + file.Name;

                        if (file.IsDirectory)
                        {
                            DownloadDirectoryAsTarRec(tarOut, sftpClient, sourceFilePath);
                        }
                        else
                        {
                            //Stream stream = new MemoryStream();
                            try
                            {

                                //sftpClient.DownloadFile(sourceFilePath, stream);
                                using (Stream stream = sftpClient.OpenRead(sourceFilePath))
                                {
                                    var entry = TarEntry.CreateTarEntry(sourceFilePath);
                                    // var header = new TarHeader();
                                    entry.TarHeader.LinkName = String.Empty;
                                    entry.TarHeader.Mode = ExtractPermissions(file);//33216;
                                    entry.TarHeader.TypeFlag = TarHeader.LF_NORMAL;// 48;
                                    entry.TarHeader.Size = file.Attributes.Size;
                                    entry.TarHeader.ModTime = DateTime.Now;
                                    entry.TarHeader.DevMajor = 0;
                                    entry.TarHeader.DevMinor = 0;
                                    // archive.WriteEntry(entry,true);
                                    tarOut.PutNextEntry(entry);
                                    StreamUtils.Copy(stream, tarOut, new byte[4096]);
                                    //tarOut.Write(array, 0, num);
                                    tarOut.CloseEntry();
                                }

                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"Download File failed: {sourceFilePath} | Error:{e}");
                            }
                            finally
                            {
                                // stream.Close();
                                // stream.Dispose();
                            }

                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Download Directory failed: {sourceRemotePath} | Error:{e}");
            }
        }

        public static int ExtractPermissions(SftpFile file)
        {
            uint num = 0u;
            /*  if (_isBitFiledsBitSet)
              {
                  num |= 0xF000;
              }*/

            if (file.IsSocket)
            {
                num |= 0xC000;
            }

            if (file.IsSymbolicLink)
            {
                num |= 0xA000;
            }

            if (file.IsRegularFile)
            {
                num |= 0x8000;
            }

            if (file.IsBlockDevice)
            {
                num |= 0x6000;
            }

            if (file.IsDirectory)
            {
                num |= 0x4000;
            }

            if (file.IsCharacterDevice)
            {
                num |= 0x2000;
            }

            if (file.IsNamedPipe)
            {
                num |= 0x1000;
            }

            /*if (_isUIDBitSet)
            {
                num |= 0x800;
            }

            if (file._isGroupIDBitSet)
            {
                num |= 0x400;
            }

            if (file._isStickyBitSet)
            {
                num |= 0x200;
            }*/

            if (file.OwnerCanRead)
            {
                num |= 0x100;
            }

            if (file.OwnerCanWrite)
            {
                num |= 0x80;
            }

            if (file.OwnerCanExecute)
            {
                num |= 0x40;
            }

            if (file.GroupCanRead)
            {
                num |= 0x20;
            }

            if (file.GroupCanWrite)
            {
                num |= 0x10;
            }

            if (file.GroupCanExecute)
            {
                num |= 8;
            }

            if (file.OthersCanRead)
            {
                num |= 4;
            }

            if (file.OthersCanWrite)
            {
                num |= 2;
            }

            if (file.OthersCanExecute)
            {
                num |= 1;
            }

            return (int)num  ;

        }

        public static List<FileModel> ListRemoteDirectory(SftpClient sftpClient, string sourceRemotePath)
        {
            var results = new List<FileModel>();
            ListRemoteDirectoryRec(sftpClient, sourceRemotePath, results);
            return results;
        }

        public static void ListRemoteDirectoryRec(SftpClient sftpClient, string sourceRemotePath, List<FileModel> results)
        {
            var files = sftpClient.ListDirectory(sourceRemotePath);
            foreach (SftpFile file in files)
            {
                if ((file.Name != ".") && (file.Name != ".."))
                {
                    string sourceFilePath = sourceRemotePath + "/" + file.Name;

                    if (file.IsDirectory)
                    {
                        ListRemoteDirectoryRec(sftpClient, sourceFilePath, results);
                    }
                    else
                    {
                        results.Add(new FileModel()
                        {
                            Name = file.Name,
                            Path = file.FullName,
                            Atime = file.LastAccessTime,
                            Mtime = file.LastWriteTime,
                        });
                    }
                }
            }
        }

        public static List<FileModel> ListZipDirectory(string zipFilePath)
        {
            var results = new List<FileModel>();
            using (var zip = ionicZip.ZipFile.Read(zipFilePath))
            {
                int totalEntries = zip.Entries.Count;
                foreach (var file in zip.Entries)
                {
                    results.Add(new FileModel()
                    {
                        Name = file.FileName,
                        Path = file.FileName,
                        Mtime = file.LastModified,
                    });
                }
            }
            return results;
        }

        public static List<FileModel> ListZipDirectory2(string zipFilePath)
        {
            var results = new List<FileModel>();

            using (ZipArchive zip = ZipFile.OpenRead(zipFilePath))
            {
                foreach (ZipArchiveEntry file in zip.Entries)
                {
                    results.Add(new FileModel()
                    {
                        Name = file.Name,
                        Path = file.FullName,
                        Mtime = file.LastWriteTime.DateTime,
                    });
                }
            }
            return results;
        }

        public static List<FileModel> ListDiff(List<FileModel> local, List<FileModel> remote)
        {
            var results = new List<FileModel>();

            foreach (FileModel fileRemote in remote)
            {
                var containFile = ListContainFile(fileRemote, local);
                if (containFile != null)
                {
                    if (fileRemote.Mtime > containFile.Mtime)
                    {
                        results.Add(fileRemote);
                    }
                }
                else
                {
                    results.Add(fileRemote);
                }
            }
            return results;
        }

        public static FileModel ListContainFile(FileModel fileToCheck, List<FileModel> list)
        {
            foreach (FileModel file in list)
            {
                if (fileToCheck.Path.Equals(file.Path, StringComparison.OrdinalIgnoreCase))
                {
                    return file;
                }
            }
            return null;
        }
    }
}

