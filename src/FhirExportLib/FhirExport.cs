using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
        public static async Task<string> AppendQueryToFile(string serverUrl, string query, string fileName, FhirAuthenticator fhirAuth)
        {
            long records = 0;

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(serverUrl);
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + fhirAuth.GetAuthenticationResult().AccessToken);

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

    public class BlobUploader
    {

    }
}
