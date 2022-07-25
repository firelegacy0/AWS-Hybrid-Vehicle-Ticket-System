using Amazon;
using Amazon.Textract;
using Amazon.Textract.Model;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ConsoleApp
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            List<String> plateData = new List<String>();

            //cal_plate1_english.jpg
            //cal_plate2_spanish.jpg
            //cal_plate3_russian.jpg
            //cal_plate4_french.jpg
            //cal_plate5_english.jpg
            //michigan_plate6.jpg
            string photo = "cal_plate3_russian.jpg";

            string bucket = "cs455-project3"; // "bucket";

            //Make a new TextractClient
            var textractClient = new AmazonTextractClient(RegionEndpoint.USEast1);

            //Make a new Textract Request
            var detectDocumentTextRequest = new DetectDocumentTextRequest()
            {
                Document = new Document
                {
                    S3Object = new Amazon.Textract.Model.S3Object()
                    {
                        Name = photo,
                        Bucket = bucket,
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

            if (plateData.Contains("California"))
            {
                string pattern = "^(?=.*[0-9])(?=.*[a-zA-Z])([a-zA-Z0-9]+)$";
                // Instantiate the regular expression object.
                Regex r = new Regex(pattern, RegexOptions.IgnoreCase);

                foreach (var info in plateData)
                {
                    //Check for match against XML
                    //if (info.Equals("7TRR812"))
                    //{
                    //    Console.WriteLine("Found");
                    //    return;
                    //}

                    if (info.Length == 7)
                    {
                        // Match the regular expression pattern against a text string.
                        Match m = r.Match(info);
                        if (m.Success)
                        {
                            Console.WriteLine("Found: {0}", m);
                            return;
                        }
                    }

                    //Console.WriteLine("Info: {0}", info);
                }
            }
            else
            {
                // Push to other bucket
                Console.WriteLine("Nothing");
            }
        }
    }
}