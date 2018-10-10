﻿using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using FhirExport;

namespace FhirExport
{
    class Program
    {
        public static IConfiguration Configuration { get; set; }
        private static String OutputFile { get; set; }

        private static FhirAuthenticator FhirAuth { get; set;}
        static void Main(string[] args)
        {
            Task.Run(() => MainAsync(args)).Wait();
        }

        static async Task MainAsync(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .AddUserSecrets<Program>();

            Configuration = builder.Build();
            OutputFile = String.IsNullOrEmpty(Configuration["OutputFile"]) ? "export.json" : Configuration["OutputFile"];

            if (File.Exists(OutputFile))
            {
                File.Delete(OutputFile);
            }

            Console.WriteLine($"FHIR Resource Path      : {Configuration["FhirQuery"]}");
            Console.WriteLine($"FHIR Server URL         : {Configuration["FhirServerUrl"]}");
            Console.WriteLine($"Azure AD Authority      : {Configuration["AzureAD_Authority"]}");
            Console.WriteLine($"Azure AD Client ID      : {Configuration["AzureAD_ClientId"]}");
            Console.WriteLine($"Azure AD Client Secret  : {Configuration["AzureAD_ClientSecret"]}");
            Console.WriteLine($"Azure AD Audience       : {Configuration["AzureAD_Audience"]}");
            Console.WriteLine($"Output File             : {OutputFile}");
            Console.WriteLine($"Blob connection string  : {Configuration["StorageConnectionString"]}");
            Console.WriteLine($"Blob folder             : {Configuration["BlobFolder"]}");


            FhirAuth = new FhirAuthenticator(Configuration["AzureAD_Authority"], 
                                             Configuration["AzureAD_ClientId"], 
                                             Configuration["AzureAD_ClientSecret"], 
                                             Configuration["AzureAD_Audience"]);

            string query = Configuration["FhirQuery"];
            while (!String.IsNullOrEmpty(query)) 
            {
                query = await FhirQuery.AppendQueryToFile(Configuration["FhirServerUrl"], query, OutputFile, FhirAuth);
            }

            if (!String.IsNullOrEmpty(Configuration["StorageConnectionString"]) &&
                !String.IsNullOrEmpty(Configuration["BlobFolder"]))
            {
                await BlobUploader.UploadFileToBlob(Configuration["StorageConnectionString"],
                                                    Configuration["BlobFolder"],
                                                    OutputFile);
            }
        }
    }
}
