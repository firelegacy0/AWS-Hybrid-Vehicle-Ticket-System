# AWS-Hybrid-Vehicle-Ticket-System

This is a 2 member group-based hybrid cloud project from Cloud Computing class.

## Technologies Used
- AWS Textract to scan and extract information from images
- AWS Cloud Services (S3, Lambda, SQS, SES)
- Windows Worker Service

This project uses a Hybrid Architecture (local database XML file) and Serverless Architecture to process information. 

## Use-Case Flow
- An image is sent to S3 Bucket
- A Lambda Function (PlateReaderFunction) processes the image and check if it's California plate or not, then extracts the information
- Information is sent to DownwardQueue
- A Windows Worker Service (DMVService) is constantly running in background that polls the DownwardQueue for new incoming messages
- DMVService queries the local database XML file for vehicle information, then sends it to UpwardQueue
- A Lambda Function (TicketProcessingFunction) processes information from UpwardQueue, formulates the final message and notify user via AWS SES

##### Note: License Plates and Vehicle Owners used are fictional

## System Workflow Diagram of Project
![system workflow diagram](https://user-images.githubusercontent.com/55813746/180680184-9e4e3e9c-5885-470b-a489-e25f1a19951d.jpg)


# 
