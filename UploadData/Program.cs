using System;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;
using Amazon.S3.Transfer;

namespace UploadData
{
    internal class Program
    {
        private const string bucketName = "project-3-auto-bucket";

        //args
        private static string filePath = "";
        private static string location = "";
        private static string dateTime = "";
        private static string type = "";
        static async Task Main(string[] args)
        {
            filePath = args[0];
            location = args[1];
            dateTime = args[2];
            type = args[3];

            // Get credentials to use the authenticate ourselvs to AWS
            AWSCredentials credentials = GetAWSCredentialsByName("default");

            //Get an object that allows us to interact with some AWS service. In this case we want to interact with S3
            using (AmazonS3Client s3Client = new AmazonS3Client(credentials, RegionEndpoint.USEast1))
            {
                var fileTransferUtility = new TransferUtility(s3Client);
                var fileTransferUtilityRequest = new TransferUtilityUploadRequest
                {
                    BucketName = bucketName,
                    FilePath = filePath,
                };
                fileTransferUtilityRequest.Metadata.Add("Location", location);
                fileTransferUtilityRequest.Metadata.Add("DateTime", dateTime);
                fileTransferUtilityRequest.Metadata.Add("Type", type);

                await fileTransferUtility.UploadAsync(fileTransferUtilityRequest);
                Console.WriteLine("Upload 4 completed");
            }
        }

        // Get AWS credentials by profile name
        private static AWSCredentials GetAWSCredentialsByName(string profileName)
        {
            if (String.IsNullOrEmpty(profileName))
            {
                throw new ArgumentNullException("profileName cannot be null or empty");
            }

            SharedCredentialsFile credFile = new SharedCredentialsFile();
            CredentialProfile profile = credFile.ListProfiles().Find(profile => profile.Name.Equals(profileName));
            if (profile == null)
            {
                throw new Exception(String.Format("Profile name {0} not found", profileName));
            }
            return AWSCredentialsFactory.GetAWSCredentials(profile, new SharedCredentialsFile());
        }
    }
}