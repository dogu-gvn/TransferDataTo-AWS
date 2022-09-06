using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Ionic.Zip;
using System.Configuration;
using TransferData.Services;
using Amazon.S3.Transfer;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using System.Reflection;

namespace TransferData
{
    internal class Program
    {
        private static string _MainContainerPath = ConfigurationManager.AppSettings["MainContainerPath"];
        private static string _TxtFilePath = ConfigurationManager.AppSettings["TxtFilePath"];
        private static string _FileBasePath = ConfigurationManager.AppSettings["FileBasePath"];
        private static string _CompanyName = ConfigurationManager.AppSettings["CompanyName"];
        private static string _LogFilePath = ConfigurationManager.AppSettings["LogFilePath"];
        private static string _DownloadUserID = ConfigurationManager.AppSettings["DownloadUserID"];

        private static readonly RegionEndpoint bucketRegion = RegionEndpoint.EUCentral1;
        private static IAmazonS3 client;
        static void Main(string[] args)
        {
            var CurrentPath = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            string LogDirectoryPath = "Log";

            if (!Directory.Exists(CurrentPath + "\\" + LogDirectoryPath))
                Directory.CreateDirectory(CurrentPath + "\\" + LogDirectoryPath);


            Console.WriteLine("Starting...");

            if (!Directory.Exists(_MainContainerPath))
                Directory.CreateDirectory(_MainContainerPath);

            string[] ReportIDs = Services.Services.Get_ReadAllLinesFromTxt(_TxtFilePath);

            //Dosya yazma
            FileStream fs = new FileStream(_LogFilePath, FileMode.OpenOrCreate, FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs);




            foreach (string ReportID in ReportIDs)
            {
                string ReportDirectoryPath = _MainContainerPath + "\\" + ReportID;
                if (!Directory.Exists(ReportDirectoryPath))
                    Directory.CreateDirectory(ReportDirectoryPath);

                var reportFiles = Services.Services.Get_ReportFiles(Int32.Parse(ReportID));
                string ZipFileName = ReportDirectoryPath + ".zip";

                if (reportFiles != null && reportFiles.Count > 0)
                {
                    foreach (var file in reportFiles)
                    {


                        string FileFullPath = _FileBasePath + file.URL1.TrimStart('~').Replace("/", @"\");

                        string sourceFileName = FileFullPath;
                        string desFileName = ReportDirectoryPath + @"\" + file.ReportFilePath;
                        if (File.Exists(FileFullPath))
                        {
                            try
                            {

                                File.Copy(sourceFileName, desFileName);
                                Console.WriteLine($"{DateTime.Now} ## {file.URL1}");
                            }
                            catch (Exception e)
                            {

                                Console.WriteLine($"Copy Error******{DateTime.Now}  {file.URL1}  {e.Message}");
                                sw.WriteLine($"Copy Error******{DateTime.Now}  {file.URL1}  {e.Message}");
                                continue;
                            }
                        }
                        else
                        {

                            string keyName = file.URL1.Replace("~/Images/Uploads", _CompanyName);


                            var result = Services.Services.DownloadFileFromS3Async(keyName, desFileName);
                            if (result)
                            {
                                Console.WriteLine($"{DateTime.Now} ## {file.URL1}");
                            }
                            else
                            {
                                Console.WriteLine($"{DateTime.Now} File couldn't found at server*** {file.URL1}");                                
                            }
                        }
                    }
                    try
                    {
                        using (ZipFile zip = new ZipFile())
                        {

                            zip.AddDirectory(ReportDirectoryPath);
                            zip.Save(ZipFileName);
                        }
                    }
                    catch (Exception e)
                    {

                        Console.WriteLine($"Zip Error******{DateTime.Now}  {ReportID}  {e.Message}");
                        sw.WriteLine($"Zip Error******{DateTime.Now}  {ReportID}  {e.Message}");
                        continue;
                    }
                    if (File.Exists(ZipFileName))
                    {
                        try
                        {
                            Directory.Delete(ReportDirectoryPath, true);
                        }
                        catch (Exception e)
                        {

                            Console.WriteLine($"Delete Error******{DateTime.Now}  {ReportID}  {e.Message}");
                            sw.WriteLine($"Delete Error******{DateTime.Now}  {ReportID}  {e.Message}");
                            continue;
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Report File Null******{DateTime.Now}  ReportID:{ReportID}  ");
                    sw.WriteLine($"Report File Null******{DateTime.Now}  ReportID:{ReportID}  ");

                }

                if (_CompanyName.ToLower() == "***")
                {
                    try
                    {
                        string query = $"INSERT INTO [ReportFileDownloadLogs] VALUES ({ReportID},4,0,{_DownloadUserID},GETDATE())";
                        string SQLCONN = "Data Source=*****;Initial Catalog=****;Persist Security Info=True;User ID=*****;Password=****;Connect Timeout=300";
                        SqlConnection sqlConnection = new SqlConnection(SQLCONN);
                        SqlCommand command = new SqlCommand(query);

                        command.Connection = sqlConnection;
                        sqlConnection.Open();
                        command.ExecuteNonQuery();

                        sqlConnection.Close();

                    }
                    catch (Exception e)
                    {

                        Console.WriteLine($"Delete Error******{DateTime.Now}  {ReportID}  {e.Message}");
                        sw.WriteLine($"Delete Error******{DateTime.Now}  {ReportID}  {e.Message}");
                    }
                }


            }
            string ZipFile = _MainContainerPath + ".zip";
            using (ZipFile zip = new ZipFile())
            {

                zip.AddDirectory(_MainContainerPath);
                zip.Save(ZipFile);
            }
            if (File.Exists(ZipFile))
            {
                try
                {
                    Directory.Delete(_MainContainerPath, true);
                }
                catch (Exception e)
                {

                    Console.WriteLine($"Delete Error******{DateTime.Now} {e.Message}");
                    sw.WriteLine($"Delete Error******{DateTime.Now} {e.Message}");
                }
            }


            sw.Flush();
            sw.Close();
            fs.Close();

            Console.WriteLine("Job Done...");
            Console.ReadLine();

        }
        static async Task DownloadFileFromS3Async(string FileName)
        {
            string responseBody = _MainContainerPath;
            try
            {
                GetObjectRequest request = new GetObjectRequest
                {
                    BucketName = "gdys",
                    Key = FileName,

                };
                using (GetObjectResponse response = await client.GetObjectAsync(request))
                using (Stream responseStream = response.ResponseStream)
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    string title = response.Metadata["x-amz-meta-title"]; // buraya ne doldurulacak
                    string contentType = response.Headers["Content-Type"];
                    Console.WriteLine("Object metadata, Title: {0}", title);
                    Console.WriteLine("Content type: {0}", contentType);

                    responseBody = reader.ReadToEnd(); // Now you process the response body.               
                }
            }
            catch (AmazonS3Exception e)
            {
                // If bucket or object does not exist
                Console.WriteLine("Error encountered ***. Message:'{0}' when reading object", e.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine("Unknown encountered on server. Message:'{0}' when reading object", e.Message);
            }

        }

    }
}
