using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DMVService
{
    public class Vehicle
    {
        public string Color { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }
        public string PlateNumber { get; set; }
        public string PreferredLanguage { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public string Type { get; set; }
        public string DateTime { get; set; }
        public string Contact { get; set; }

    }


    public class Worker : BackgroundService
    {
        private const string upwardQueueUrl = "https://sqs.us-east-1.amazonaws.com/658992319227/UpwardQueue3";
        private const string downqueueUrl = "https://sqs.us-east-1.amazonaws.com/658992319227/DownwardQueue1";
        private const string logPath = @"C:\Temp\Worker.xml";

        //Variables for querying DMVDatabase (vehicle plate info)
        //public static string incomingID = "";

        private readonly ILogger<Worker> _logger;

        //AWS Credentials
        //Update own credentials 
        private const string accessKey = "";
        private const string secretKey = "";

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        //Query the DMVDatabase.Xml file
        public static void QueryDB(Vehicle vehicle1)
        {
            //Create an XMLDocument object
            XmlDocument xmlDoc = new XmlDocument();

            //Update your own local database filepath
            xmlDoc.Load("C:\\CS455CloudComputing\\Project3\\DMVDatabase.xml");

            //Root is <dmv>
            XmlElement root = xmlDoc.DocumentElement;

            //Grab Vehicle from XML
            //Query DMVDatabase.Xml using the ID passed from Queue
            XmlNode vehicle = root.SelectSingleNode("vehicle[@plate=\"" + vehicle1.PlateNumber + "\"]");

            //If plate number is not null
            //Use XPath to query by selecting nodes
            if (vehicle != null)
            {
                //vehicle1.PlateNumber = incomingID;

                XmlNode make = vehicle.SelectSingleNode("make");
                if (make != null)
                {
                    vehicle1.Make = make.InnerText;
                }

                XmlNode model = vehicle.SelectSingleNode("model");
                if (model != null)
                {
                    vehicle1.Model = model.InnerText;
                }

                XmlNode color = vehicle.SelectSingleNode("color");
                if (color != null)
                {
                    vehicle1.Color = color.InnerText;
                }

                XmlNode ownerLanguage = vehicle.SelectSingleNode("owner/@preferredLanguage");
                if (ownerLanguage != null)
                {
                    vehicle1.PreferredLanguage = ownerLanguage.Value;
                }

                XmlNode ownerName = vehicle.SelectSingleNode("owner/name");
                if (ownerName != null)
                {
                    vehicle1.Name = ownerName.InnerText;
                }

                XmlNode ownerContact = vehicle.SelectSingleNode("owner/contact");
                if (ownerContact != null)
                {
                    vehicle1.Contact = ownerContact.InnerText;
                }
            }
        }

        //Execute the Task:
        //Long-Poll the DownwardQueue (TestQueue1)
        //Check response, parse incoming JSON message
        //QueryDB
        //Delete from Queue
        //Send to UpwardQueue
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //Start a connection using credentials, please remember to update it
            using (AmazonSQSClient sqsClient = new AmazonSQSClient(accessKey, secretKey, Amazon.RegionEndpoint.USEast1))
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    //If DownwardQueue is empty, doesnt run
                    try
                    {
                        //We set 20s for Long-Polling
                        //Max number of messages each request is 1
                        ReceiveMessageRequest request = new ReceiveMessageRequest()
                        {
                            QueueUrl = downqueueUrl,
                            MaxNumberOfMessages = 1,
                            WaitTimeSeconds = 20
                        };

                        //Await for response, before proceeding
                        ReceiveMessageResponse response = await sqsClient.ReceiveMessageAsync(request);

                        //Check if there's any messages to read from Response
                        if (response.Messages.Any())
                        {
                            //Make a new Vehicle Object
                            Vehicle vehicle1 = new Vehicle();

                            //Loop through the response and extract JSON body
                            foreach (var message in response.Messages)
                            {
                                //WriteToLog("Received: " + message.Body);

                                //Make a new Patient Object to Parse JSON
                                Vehicle incomingVehicle = new Vehicle();
                                incomingVehicle = JsonSerializer.Deserialize<Vehicle>(message.Body);

                                ////assign the deserialized JSON's fields into Vehicle1 object
                                //incomingID = incomingVehicle.PlateNumber;
                                vehicle1.PlateNumber = incomingVehicle.PlateNumber;
                                vehicle1.Type = incomingVehicle.Type;
                                vehicle1.DateTime = incomingVehicle.DateTime;
                                vehicle1.Location = incomingVehicle.Location;

                                WriteToLog(" Read message: " + message.Body);


                                //Try to search the DB
                                try
                                {
                                    QueryDB(vehicle1);
                                }
                                catch (Exception ex)
                                {
                                    WriteToLog(" Died while Querying XML: " + ex.Message);
                                }

                                //Delete message from DownwardQueue
                                DeleteMessageRequest deleteRequest = new DeleteMessageRequest()
                                {
                                    QueueUrl = downqueueUrl,
                                    ReceiptHandle = message.ReceiptHandle,
                                };

                                //Wait for DeleteResponse
                                DeleteMessageResponse deleteResponse = await sqsClient.DeleteMessageAsync(deleteRequest);
                                WriteToLog(" Delete message from DownwardQueue: " + downqueueUrl);
                                WriteToLog(" HttpStatusCode: " + deleteResponse.HttpStatusCode);
                            }

                            //WriteToLog("Sanity Check: " + incomingID + " " + readPolicyNumber + " " + readProviderName);

                            //Now send to UpwardQueue TicketProcessingFunction
                            if (vehicle1.PlateNumber != "")
                            {
                                string jsonString = JsonSerializer.Serialize(vehicle1);

                                //string result = "Patient with ID " + incomingID + ": policyNumber=" + readPolicyNumber + ", provider=" + readProviderName;
                                //string jsonString = JsonSerializer.Serialize(result);
                                SendMessageRequest sendMessageRequest = new SendMessageRequest()
                                {
                                    QueueUrl = upwardQueueUrl,
                                    MessageBody = jsonString
                                };

                                //Wait for sendMessageResponse to confirm
                                SendMessageResponse sendMessageResponse = await sqsClient.SendMessageAsync(sendMessageRequest);

                                WriteToLog(" Posted message: " + jsonString);
                            }


                        }
                    }
                    catch (Exception ex)
                    {
                        WriteToLog("Outer Try: " + ex.Message.ToString());
                    }
                }
            }
        }

        //Windows Side of things
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await base.StartAsync(cancellationToken);

        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);
        }

        //Write to logPath
        //WorkerP2.xml
        public void WriteToLog(string message)
        {
            string text = String.Format("{0}:{1}", DateTime.Now, message);
            using (StreamWriter writer = new StreamWriter(logPath, append: true))
            {
                writer.WriteLine(text);
            }
        }
    }
}
