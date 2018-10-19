using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Net.Http.Headers;


namespace FhirExport
{
    public class FhirAuthenticator
    {
        
        private static AuthenticationContext AuthContext { get; set; }
        private static ClientCredential ClientCredential { get; set; }
        private static String Audience { get; set; }

        public FhirAuthenticator(string authority,
                                string clientId,
                                string clientSecret,
                                string audience)
        {
            AuthContext = new AuthenticationContext(authority);
            ClientCredential = new ClientCredential(clientId, clientSecret);
            Audience = audience;
        }

        public AuthenticationResult GetAuthenticationResult()
        {
            return AuthContext.AcquireTokenAsync(Audience, ClientCredential).Result;
        }
    }

    public class FhirQuery
    {

        private Mutex FileMutex { get; set; }
        private string ServerUrl { get; set; }
        private string FileName { get; set; }
        private FhirAuthenticator FhirAuth { get; set; }
        private Anonymizer Anom { get; set; }
        private ActionBlock<string> QueryQueue { get; set; }

        public FhirQuery(string serverUrl, string outFileName, FhirAuthenticator fhirAuthenticator, Anonymizer anonymizer = null, int parallel = 4)
        {
            FileMutex = new Mutex();
            ServerUrl = serverUrl;
            FileName = outFileName;
            FhirAuth = fhirAuthenticator;
            Anom = anonymizer;

            QueryQueue = new ActionBlock<string>(s => AppendQueryToFileWorker(s), 
                new ExecutionDataflowBlockOptions {
                    MaxDegreeOfParallelism = parallel
            });
        }

        public void AppendQueryToFile(string query)
        {
            QueryQueue.Post(query);
            QueryQueue.Completion.Wait();
        }

        public async Task AppendQueryToFileWorker(string query)
        {
            long records = 0;

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(ServerUrl);
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + FhirAuth.GetAuthenticationResult().AccessToken);

                HttpResponseMessage getResult = getResult = await client.GetAsync(query);

                JObject bundle = JObject.Parse(await getResult.Content.ReadAsStringAsync());
                JArray entries = (JArray)bundle["entry"];

                JArray links = (JArray)bundle["link"];
                string nextQuery = "";
                for (int i = 0; i < links.Count; i++)
                {
                    string link_type = (string)(bundle["link"][i]["relation"]);
                    string link_url = (string)(bundle["link"][i]["url"]);

                    if (link_type == "next")
                    {
                        Uri nextUri = new Uri(link_url);
                        nextQuery = nextUri.PathAndQuery;
                        break;
                    }
                }

                if (!String.IsNullOrEmpty(nextQuery))
                {
                    QueryQueue.Post(nextQuery);
                }

                if (entries != null) 
                {
                    for (int i = 0; i < entries.Count; i++)
                    {
                        string entry_json = (((JObject)entries[i])["resource"]).ToString(Formatting.None);
                        if (Anom != null)
                        {
                            entry_json = await Anom.AnonymizeDataAsync(entry_json);
                        }
                        AppendToFile(FileName, entry_json);
                        records++;
                    }
                }

                if (String.IsNullOrEmpty(nextQuery))
                {
                    QueryQueue.Complete();
                }
            }
        }

        private void AppendToFile(string fileName, string appendString)
        {
            FileMutex.WaitOne();
            using (StreamWriter w = File.AppendText(fileName))
            {
                w.WriteLine(appendString);
            }
            FileMutex.ReleaseMutex();
        }
    }

    public class BlobUploader
    {
        public static async Task UploadFileToBlob(string blobConnectionString,
                                            string blobFolderPath,
                                            string filePath)
        {
            CloudStorageAccount outputStorageAccount = CloudStorageAccount.Parse(blobConnectionString);
            CloudBlobClient cloudBlobClient = outputStorageAccount.CreateCloudBlobClient();

            var cloudBlobContainer = cloudBlobClient.GetContainerReference(blobFolderPath);
            await cloudBlobContainer.CreateIfNotExistsAsync();

            var fileName = Path.GetFileName(filePath);
            var outputBlobUri = new Uri(outputStorageAccount.BlobEndpoint + blobFolderPath + "/" + fileName);
            var outputBlob = new CloudBlockBlob(outputBlobUri, outputStorageAccount.Credentials);
            using (var fileStream = System.IO.File.OpenRead(filePath))
            {
                await outputBlob.UploadFromStreamAsync(fileStream);
            }
        }
    }

    public class Anonymizer
    {
        private static string PresidioUrl { get; set; } 
        private static string AnalyzeTemplateId { get; set; } 
        private static string AnonymizeTemplateId { get; set; } 

        public Anonymizer(string presidioUrl, string analyzeTemplateId, string anonimizeTemplateId)
        {
            PresidioUrl = presidioUrl;
            AnalyzeTemplateId = analyzeTemplateId;
            AnonymizeTemplateId = anonimizeTemplateId;
        }

        public async Task<string> AnonymizeDataAsync(string textToAnonymize)
        {
            using (var client = new HttpClient())
            {
                var presidioObjectString = JsonConvert.SerializeObject(new PresidioObject(AnalyzeTemplateId, AnonymizeTemplateId, textToAnonymize));
                var content = new StringContent(presidioObjectString, Encoding.UTF8, "application/json");
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                HttpResponseMessage postResult = await client.PostAsync(PresidioUrl, content);
                PresidioObject retObj = JsonConvert.DeserializeObject<PresidioObject>(await postResult.Content.ReadAsStringAsync());
                return retObj.text;
            }
        }
    }

    public class PresidioObject
    {
        public string analyzeTemplateId { get; set; }
        public string anonymizeTemplateId { get; set; }
        public string text { get; set; }

        public PresidioObject(string analyzeTemplateId, string anonymizeTemplateId, string text)
        {
            this.analyzeTemplateId = analyzeTemplateId;
            this.anonymizeTemplateId = anonymizeTemplateId;
            this.text = text;
        }
    }
}
