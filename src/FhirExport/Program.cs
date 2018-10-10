using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using FhirExport;

namespace FhirExport
{
    class Program
    {
        public static IConfiguration Configuration { get; set; }
        private static AuthenticationContext AuthContext { get; set; }
        private static ClientCredential ClientCredential { get; set; }
        private static String Audience { get; set; }
        private static String OutputFile { get; set; }

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

            AuthContext = new AuthenticationContext(Configuration["AzureAD_Authority"]);
            ClientCredential = new ClientCredential(Configuration["AzureAD_ClientId"], Configuration["AzureAD_ClientSecret"]); ;
            Audience = Configuration["AzureAD_Audience"];

            string query = Configuration["FhirQuery"];
            while (!String.IsNullOrEmpty(query)) 
            {
                query = await AppendQueryToFile(Configuration["FhirServerUrl"], query, OutputFile);
            }
        }

        private static AuthenticationResult GetAuthenticationResult()
        {
            return AuthContext.AcquireTokenAsync(Audience, ClientCredential).Result;
        }

        private static async Task<string> AppendQueryToFile(string serverUrl, string query, string fileName)
        {
            long records = 0;

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(serverUrl);
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + GetAuthenticationResult().AccessToken);

                HttpResponseMessage getResult = getResult = await client.GetAsync(query);

                JObject bundle = JObject.Parse(await getResult.Content.ReadAsStringAsync());
                JArray entries = (JArray)bundle["entry"];

                for (int i = 0; i < entries.Count; i++)
                {
                    string entry_json = (((JObject)entries[i])["resource"]).ToString(Formatting.None);
                    AppendToFile(fileName, entry_json);
                    records++;
                }

                JArray links = (JArray)bundle["link"];
                for (int i = 0; i < links.Count; i++)
                {
                    string link_type = (string)(bundle["link"][i]["relation"]);
                    string link_url = (string)(bundle["link"][i]["url"]);

                    if (link_type == "next")
                    {
                        Uri nextUri = new Uri(link_url);
                        return nextUri.PathAndQuery;
                    }
                }

                return "";
            }
        }

        private static void AppendToFile(string fileName, string appendString)
        {
            using (StreamWriter w = File.AppendText(fileName))
            {
                w.WriteLine(appendString);
            }
        }
    }
}
