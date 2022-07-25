using System.Text.Json;
using Amazon;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Amazon.SQS;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace TicketProcessingFunction;

public class Function
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

    private const string upwardQueueUrl = "https://sqs.us-east-1.amazonaws.com/658992319227/UpwardQueue3";

    // Replace sender@example.com with your "From" address.
    // This address must be verified with Amazon SES.
    static readonly string senderAddress = "khoo.jarrel@bellevuecollege.edu";

    // The subject line for the email.
    static readonly string subject = "Project 3 Ticket";


    /// <summary>
    /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
    /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
    /// region the Lambda function is executed in.
    /// </summary>
    public Function()
    {

    }

    private static async Task DeleteMessage(IAmazonSQS sqsClient, SQSEvent.SQSMessage message, string qUrl)
    {
        Console.WriteLine($"\nDeleting message {message.MessageId} from queue...");
        await sqsClient.DeleteMessageAsync(qUrl, message.ReceiptHandle);
    }

    //Method to SendEmail using SES Service, code from AWS SDK Documentation
    private static async Task<Task> SendEmail(string receiverAddress, string textBody)
    {
        // Replace USWest2 with the AWS Region you're using for Amazon SES.
        // Acceptable values are EUWest1, USEast1, and USWest2.
        using (var client = new AmazonSimpleEmailServiceClient(RegionEndpoint.USEast1))
        {
            var sendRequest = new SendEmailRequest
            {
                Source = senderAddress,
                Destination = new Destination
                {
                    ToAddresses =
                    new List<string> { receiverAddress }
                },
                Message = new Message
                {
                    Subject = new Content(subject),
                    Body = new Body
                    {
                        Html = new Content
                        {
                            Charset = "UTF-8",
                            Data = textBody
                        },
                        Text = new Content
                        {
                            Charset = "UTF-8",
                            Data = textBody
                        }
                    }
                },
                // If you are not using a configuration set, comment
                // or remove the following line 
                //ConfigurationSetName = configSet
            };
            try
            {
                Console.WriteLine("Sending email using Amazon SES...");
                var response = await client.SendEmailAsync(sendRequest);
                Console.WriteLine("The email was sent successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("The email was not sent.");
                Console.WriteLine("Error message: " + ex.Message);

            }
        }

        return Task.CompletedTask;
    }


    /// <summary>
    /// This method is called for every Lambda invocation. This method takes in an SQS event object and can be used 
    /// to respond to SQS messages.
    /// </summary>
    /// <param name="evnt"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
    {
        foreach (var message in evnt.Records)
        {
            await ProcessMessageAsync(message, context);
        }
    }

    private async Task ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context)
    {
        context.Logger.LogInformation($"Processed message: {message.Body}");

        // TODO: Do interesting work based on the new message
        await Task.CompletedTask;

        //Try extracting from UpwardQueue
        var sqsClient = new AmazonSQSClient();

        //Make new Vehicle object template
        Vehicle vehicle1 = new Vehicle();

        //For each element inside the message body
        foreach (var item in message.Body)
        {

            vehicle1 = JsonSerializer.Deserialize<Vehicle>(message.Body);
        }

        //Set an empty ticket Amount
        string ticketAmount = "";

        //Ticket Price Logic Switch Cases
        switch (vehicle1.Type)
        {
            case "no_stop":
                ticketAmount = "$300.00";
                break;

            case "no_full_stop_on_right":
                ticketAmount = "$75.00";
                break;

            case "no_right_on_red":
                ticketAmount = "125.00";
                break;
        }

        //Variables to assign and formulate the Email Message
        string vehicleInfo = "Vehicle: " + vehicle1.Color + " " + vehicle1.Make + " " + vehicle1.Model;
        string licensePlate = "License plate: " + vehicle1.PlateNumber;
        string date = "Date: " + vehicle1.DateTime;
        string violationAddress = "Violation address: " + vehicle1.Location;
        string violationType = "Violation type: " + vehicle1.Type;
        string violationPrice = "Ticket amount: " + ticketAmount;
        string preferredLanguage = vehicle1.PreferredLanguage;
        string ownerName = vehicle1.Name;
        string receiverAddress = vehicle1.Contact;

        //Blue section Translated Texts
        string english = "Your vehicle was involved in a traffic violation. Please pay the specified ticket amount by 30 days:";
        string russian = "Vash avtomobil' uchastvoval v narushenii pravil dorozhnogo dvizheniya. Pozhaluysta, oplatite ukazannuyu summu bileta do 30 dney:";
        string spanish = "Su vehículo estuvo involucrado en una infracción de tránsito. Pague el monto del boleto especificado antes de 30 días:";
        string french = "Votre véhicule a été impliqué dans une infraction au code de la route. Vous avez 30 jours pour payer le billet suivant:";

        //Green section where message details is in English
        string messageBody = vehicleInfo + "<br>" + licensePlate + "<br>" + date + "<br>" + violationAddress + "<br>" + violationType + "<br>" + violationPrice;

        //Switch case to format preferred Language 
        switch (preferredLanguage)
        {
            case "english":
                messageBody = @"<html><body>" + english + "<br>" + messageBody + "</body></html>";
                break;
            case "russian":
                messageBody = @"<html><body>" + russian + "<br>" + messageBody + "</body></html>";
                break;
            case "spanish":
                messageBody = @"<html><body>" + spanish + "<br>" + messageBody + "</body></html>";
                break;
            case "french":
                messageBody = @"<html><body>" + french + "<br>" + messageBody + "</body></html>";
                break;
        }

        //For Cloudwatch printing
        Console.WriteLine("Body: {0}", messageBody);
        Console.WriteLine("Preferred Language: {0}", preferredLanguage);
        Console.WriteLine("Receiver Email: {0}", receiverAddress);


        await DeleteMessage(sqsClient, message, upwardQueueUrl);

        context.Logger.LogInformation($"Deleted Message");

        await SendEmail(receiverAddress, messageBody);


    }
}