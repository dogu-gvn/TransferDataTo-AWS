using TransferData.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.S3.Transfer;
using Amazon;
using Amazon.S3.Model;
using Amazon.S3;
using System.IO;
using Amazon.S3.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

namespace TransferData.Services
{
    public class Services
    {
        private static string _LogFilePath = ConfigurationManager.AppSettings["LogFilePath"];
        private static string _ConnStr = ConfigurationManager.AppSettings["ConnStr"];

        #region AWS
        private const string AWSKeyID = "************";
        private const string AWSAccessKey = "*****/*****";
        private static readonly RegionEndpoint _BucketRegion = RegionEndpoint.EUCentral1;
        private const string _BucketName = "***";
        private static IAmazonS3 _S3Client = new AmazonS3Client(AWSKeyID, AWSAccessKey, _BucketRegion);
        #endregion

        #region BULUTISTAN
        private const string BulutistanKeyID = "*********";
        private const string BulutistanAccessKey = "**************";
        private const string BulutisatnBucketName = "*******";
        private const string EndPoint = "https://s3.bulutis*******/";
        private const string AuthenticationRegion = "****";

        #endregion

        private static string _MainContainerPath = ConfigurationManager.AppSettings["MainContainerPath"];




        public static string[] Get_ReadAllLinesFromTxt(string path)
        {
            try
            {
                return System.IO.File.ReadAllLines(path);
            }
            catch
            {

                return null;
            }
        }



        public static List<ReportFiles> Get_ReportFiles(int ReportID)
        {

            string CommandText = $"SELECT ReportFileID,ReportID,ReportFilePath,URL1,URL2,isFromAzure FROM ReportFiles WHERE ReportID={ReportID}";
            List<ReportFiles> reportFiles = new List<ReportFiles>();

            SqlConnection sqlConnection = new SqlConnection(_ConnStr);
            SqlCommand cmd = new SqlCommand();

            SqlDataReader reader;
            cmd.CommandText = CommandText;
            cmd.CommandType = CommandType.Text;
            cmd.Connection = sqlConnection;
            sqlConnection.Open();
            reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                ReportFiles file = new ReportFiles();

                file.ReportFileID = reader.GetInt32(0);
                file.ReportID = reader.GetInt32(1);
                file.ReportFilePath = reader.GetString(2);
                file.URL1 = reader.GetString(3);
                file.URL2 = reader.GetString(4);
                file.isFromAzure = reader.GetBoolean(5);

                reportFiles.Add(file);
            }

            reader.Close();
            sqlConnection.Close();


            return reportFiles;
        }

        public static bool DownloadFileFromS3Async(string keyName, string desFileName)
        {
            bool result = false;
            FileStream fs = new FileStream(_LogFilePath, FileMode.OpenOrCreate, FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs);

            S3FileInfo s3FileInfo = new Amazon.S3.IO.S3FileInfo(_S3Client, _BucketName, keyName);
            if (s3FileInfo.Exists)
            {
                try
                {
                    using (TransferUtility transferUtility = new Amazon.S3.Transfer.TransferUtility(AWSKeyID, AWSAccessKey, _BucketRegion))
                    {
                        TransferUtilityDownloadRequest downloadRequest = new TransferUtilityDownloadRequest
                        {
                            BucketName = _BucketName,
                            Key = keyName,//indirilecek dosyanın uzantısı
                            FilePath = desFileName,// indirilecek yer
                        };
                        transferUtility.Download(downloadRequest);

                        result = true;
                    }

                }
                catch (AmazonS3Exception e)
                {
                    // If bucket or object does not exist
                    Console.WriteLine("AWS Error ***. Message:'{0}' when reading object", e.Message);
                    sw.WriteLine("AWS Error ***. Message:'{0}' when reading object", e.Message);
                }
                catch (Exception e)
                {
                    Console.WriteLine("AWS Error***Unknown encountered on server. Message:'{0}' when reading object", e.Message);
                    sw.WriteLine("AWS Error***Unknown encountered on server. Message:'{0}' when reading object", e.Message);

                }
            }
            else
            {
                ServicePointManager.ServerCertificateValidationCallback +=
                        delegate (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
                        { return true; };

                var s3ClientConfig = new AmazonS3Config
                {
                    ServiceURL = EndPoint,
                    SignatureVersion = "4",
                    UseHttp = true,
                    AuthenticationRegion = AuthenticationRegion
                };

                IAmazonS3 _s3BulutistanClient = new AmazonS3Client(BulutistanKeyID, BulutistanAccessKey, s3ClientConfig);


                S3FileInfo s3FileInfoB = new Amazon.S3.IO.S3FileInfo(_s3BulutistanClient, BulutisatnBucketName, keyName);

                if (s3FileInfoB.Exists)
                {
                    try
                    {
                        using (TransferUtility transferUtility = new Amazon.S3.Transfer.TransferUtility(_s3BulutistanClient))
                        {
                            TransferUtilityDownloadRequest downloadRequest = new TransferUtilityDownloadRequest
                            {
                                BucketName = BulutisatnBucketName,
                                Key = keyName,
                                FilePath = desFileName,
                            };
                            transferUtility.Download(downloadRequest);
                            

                            result = true;
                        }
                    }
                    catch (AmazonS3Exception e)
                    {                        
                        Console.WriteLine("AWS Error ***. Message:'{0}' when reading object", e.Message);
                        sw.WriteLine("AWS Error ***. Message:'{0}' when reading object", e.Message);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("AWS Error***Unknown encountered on server. Message:'{0}' when reading object", e.Message);
                        sw.WriteLine("AWS Error***Unknown encountered on server. Message:'{0}' when reading object", e.Message);
                    }
                }
            }

            return result;
        }


    }
}
