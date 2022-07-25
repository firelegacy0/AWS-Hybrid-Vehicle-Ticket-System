using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.Textract;
using Amazon.Textract.Model;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace PlateReaderFunction;

public class Function
{
    public class VehicleMetadata
    {
        public string PlateNumber { get; set; }
        public string Location { get; set; }

        public string Type { get; set; }
        public string DateTime { get; set; }
    }
    IAmazonS3 S3Client { get; set; }
    private const int MaxMessages = 1;
    static List<String> plateData = new List<String>();
    private static string plateNumber = "";
    private const string downqueueUrl = "https://sqs.us-east-1.amazonaws.com/658992319227/DownwardQueue1";

    /// <summary>
    /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
    /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
    /// region the Lambda function is executed in.
    /// </summary>
    public Function()
    {
        S3Client = new AmazonS3Client();
    }

    /// <summary>
    /// Constructs an instance with a preconfigured S3 client. This can be used for testing the outside of the Lambda environment.
    /// </summary>
    /// <param name="s3Client"></param>
    public Function(IAmazonS3 s3Client)
    {
        this.S3Client = s3Client;
    }

    // Method to put a message on a queue
    // Could be expanded to include message attributes, etc., in a SendMessageRequest
    private static async Task SendMessage(
      IAmazonSQS sqsClient, string qUrl, string messageBody)
    {
        SendMessageResponse responseSendMsg =
          await sqsClient.SendMessageAsync(qUrl, messageBody);
        Console.WriteLine($"Message added to queue\n  {qUrl}");
        Console.WriteLine($"HttpStatusCode: {responseSendMsg.HttpStatusCode}");
    }

    // Method to read a message from the given queue
    // In this example, it gets one message at a time
    private static async Task<ReceiveMessageResponse> GetMessage(
      IAmazonSQS sqsClient, string qUrl, int waitTime = 0)
    {
        return await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = qUrl,
            MaxNumberOfMessages = MaxMessages,
            WaitTimeSeconds = waitTime
            // (Could also request attributes, set visibility timeout, etc.)
        });
    }

    // Copy to manual bucket
    private static async Task SendBucket(string bucketName, string objectName)
    {
        IAmazonS3 Client = new AmazonS3Client();

        //Make new Copy Request
        try
        {
            var request = new CopyObjectRequest
            {
                SourceBucket = bucketName,
                SourceKey = objectName,
                DestinationBucket = "project-3-manual-bucket",
                DestinationKey = $"{objectName}",
            };
            var response = await Client.CopyObjectAsync(request);
            Console.WriteLine("Copy Object request: {0}", response.HttpStatusCode);
        }
        catch (AmazonS3Exception ex)
        {
            Console.WriteLine($"Error copying object: '{ex.Message}'");
        }

        //Delete from old auto bucket
        try
        {
            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = bucketName,
                Key = objectName,
            };

            Console.WriteLine("Deleting an object");
            var deleteResponse = await Client.DeleteObjectAsync(deleteRequest);
            Console.WriteLine("Delete Object Request: OK");
        }
        catch (AmazonS3Exception e)
        {
            Console.WriteLine("Error encountered on server. Message:'{0}' when deleting an object", e.Message);

        }
        catch (Exception e)
        {
            Console.WriteLine("Unknown encountered on server. Message:'{0}' when deleting an object", e.Message);

        }
    }

    private static async Task ProcessImageAsync(string bucketName, string objectKey)
    {
        //Make a new TextractClient
        var textractClient = new AmazonTextractClient(RegionEndpoint.USEast1);

        //Make a new Textract Request
        var detectDocumentTextRequest = new DetectDocumentTextRequest()
        {
            Document = new Document
            {
                S3Object = new Amazon.Textract.Model.S3Object()
                {
                    Name = objectKey,
                    Bucket = bucketName,
                },
            },
        };

        //Do something with the TextractResponse
        try
        {
            DetectDocumentTextResponse detectDocumentTextResponse = await textractClient.DetectDocumentTextAsync(detectDocumentTextRequest);
            Console.WriteLine("Detect Document Text");

            //Outer For-Each to loop through the TextResponse
            foreach (var block in detectDocumentTextResponse.Blocks)
            {
                // Null check
                if (block.Text != null)
                {
                    plateData.Add(block.Text);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }

        Console.WriteLine(plateData.Count);

        if (plateData.Contains("California"))
        {
            //use Regex to extract the Plate Number
            string pattern = "^(?=.*[0-9])(?=.*[a-zA-Z])([a-zA-Z0-9]+)$";
            // Instantiate the regular expression object.
            Regex r = new Regex(pattern, RegexOptions.IgnoreCase);

            //Use Regex to match any strings that are 7 character long in the List
            foreach (var info in plateData)
            {

                if (info.Length == 7)
                {
                    // Match the regular expression pattern against a text string.
                    Match m = r.Match(info);
                    if (m.Success)
                    {
                        plateNumber = m.ToString();
                        return;
                    }
                }

            }
        }
        else
        {
            // Push to other bucket
            Console.WriteLine("Pushing to Manual Bucket...");
            await SendBucket(bucketName, objectKey);
        }
    }

    /// <summary>
    /// This method is called for every Lambda invocation. This method takes in an S3 event object and can be used 
    /// to respond to S3 notifications.
    /// </summary>
    /// <param name="evnt"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task<string?> FunctionHandler(S3Event evnt, ILambdaContext context)
    {
        var s3Event = evnt.Records?[0].S3;
        if (s3Event == null)
        {
            return null;
        }

        //Try listening to S3 Bucket for item
        try
        {
            var response = await this.S3Client.GetObjectMetadataAsync(s3Event.Bucket.Name, s3Event.Object.Key);

            Console.WriteLine("Bucket: {0}", s3Event.Bucket.Name);
            Console.WriteLine("File: {0}", s3Event.Object.Key);

            string bucketName = s3Event.Bucket.Name;
            string objectKey = s3Event.Object.Key;

            //To extract MetaData from UploadData.exe
            MetadataCollection userMetadataMap = response.Metadata;

            //3 Metadata fields
            string location = userMetadataMap["location"];
            string dateTime = userMetadataMap["datetime"];
            string type = userMetadataMap["type"];
            Console.WriteLine(location);
            Console.WriteLine(dateTime);
            Console.WriteLine(type);

            //Call ProcessImage for Textract
            await ProcessImageAsync(bucketName, objectKey);

            //Make a new VehicleMetaData Object (Vehicle) that has PlateNumber and MetaData
            VehicleMetadata vehicleMetaData = new VehicleMetadata();

            vehicleMetaData.Location = location;
            vehicleMetaData.DateTime = dateTime;
            vehicleMetaData.Type = type;
            vehicleMetaData.PlateNumber = plateNumber;

            //Prepare the JSON Message
            string vehicleMessage = JsonSerializer.Serialize(vehicleMetaData);

            var sqsClient = new AmazonSQSClient();
            await SendMessage(sqsClient, downqueueUrl, vehicleMessage);

            //Empty plateNumber and plateData
            plateNumber = "";
            location = "";
            dateTime = "";
            type = "";
            plateData.Clear();

            return response.Headers.ContentType;
        }
        catch (Exception e)
        {
            context.Logger.LogInformation($"Error getting object {s3Event.Object.Key} from bucket {s3Event.Bucket.Name}. Make sure they exist and your bucket is in the same region as this function.");
            context.Logger.LogInformation(e.Message);
            context.Logger.LogInformation(e.StackTrace);
            throw;
        }
    }
}