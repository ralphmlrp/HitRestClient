using HIT.REST.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Xml.Serialization;

namespace HIT.REST.Client
{

    enum ClientMode
    {
        NoAuthorization,
        AuthenticationHeader,
        SelfmadeHeader,
    }

    class Program
    {
        //--------------------------------------------------------------------

        static String testSecret = Guid.NewGuid().ToString().Substring(9, 14);      // Mittelteil aus GUID nehmen   z.B. "45f1-affe-87f3"

        static void Main(string[] args)
        {            
            try
            {
                //CreateJson();
                ApiInformation info = ReadApiInfo();
                foreach (var item in args)
                {

                    TaskInfo job = ReadTaskInfo(item);
                    Console.WriteLine(job.Credentials.Betriebsnummer);
                    //DOJob
                }

                Console.ReadLine();
            }
            catch (Exception ex)
            {

                Console.WriteLine(ex.Message);
                // TODO InnerExceptions rekursiv
                Console.WriteLine(ex.InnerException?.InnerException?.Message);
            }
        }


        static ApiInformation ReadApiInfo()
        {
            ApiInformation info = (ApiInformation)ConfigurationManager.GetSection("hitSettings");
            return info;
        }

        static TaskInfo ReadTaskInfo(string jobFilename)
        {
            string text = "";

            using (StreamReader reader = new StreamReader(jobFilename))
            {
                text = reader.ReadToEnd();
            }

            TaskInfo info = JsonConvert.DeserializeObject<TaskInfo>(text);
            return info;            
        }

        //--------------------------------------------------------------------

        static void CreateJson()
        {
            TaskInfo info = new TaskInfo();
            info.Credentials.Betriebsnummer = "09 000 000 0015";
            info.Credentials.MitBenutzer = "0";
            info.Credentials.PIN = "900015";

            info.JobInfos.Add(new JobInfo()
            {
                Action = "IS",
                Entity = "Geburt",
                FileName = "TaskInfos\\GeburtInsert.json"
            });

            info.JobInfos.Add(new JobInfo()
            {
                Action = "IS",
                Entity = "Zugang",
                FileName = "TaskInfos\\ZugangInsert.json"
            });

            string jsonJob = JsonConvert.SerializeObject(info);
            File.WriteAllText("TaskInfos\\jobSample.json", jsonJob);
        }

        static void CreateXml(string[] args)
        {
            ApiInformation info = new ApiInformation();
            info.Prefix = "/api/mlrp";
            info.BaseUrls = new List<string>()
            {
                "https://www.hi-tier.bybn.de/",
                "https://www-dev.hi-tier.bybn.de/"
            };

            XmlSerializer serializer = new XmlSerializer(typeof(ApiInformation));
            using (FileStream fs = new FileStream("C:\\temp\\apiInfo.txt", System.IO.FileMode.CreateNew))
            {
                serializer.Serialize(fs, info);
            }



            //start(args);


            pressEnterTo("quit");
        }



        //--------------------------------------------------------------------

        private static void start(string[] args)
        {
            HttpClient client;

            // Test ohne Auth:
            client = GetHttpClient(ClientMode.NoAuthorization);

            GetGeburten(client);

            pressEnterTo("continue with Authentication header");

            // Test mit Auth-Header:
            client = GetHttpClient(ClientMode.AuthenticationHeader);

            GetGeburten(client);

            pressEnterTo("continue with selfmade header");

            // Test ohne Auth:
            client = GetHttpClient(ClientMode.SelfmadeHeader);

            GetGeburten(client);

        }



        //--------------------------------------------------------------------


        private static void GetGeburten(HttpClient client)
        {
            HttpResponseMessage message = client.GetAsync("/api/mlrp/Geburt?condition=bnr15;=;090000000001").Result;
            try
            {
                if (wasSuccessfulResponse(message))
                {
                    // es kommt zwar als HitDataTree zurück, kann aber nicht als solches deserialisiert werden
                    // Daher klassisch mit Dictionary:
                    Dictionary<String, Object> response = message.Content.ReadAsAsync<Dictionary<String, Object>>().Result;

                    // ein vergebenes/übergebenes Secret dem Client geben
                    client.DefaultRequestHeaders.Remove("hit-secret");                              // Name siehe   MlrpRestController.HTTP_HEADER_AUTH_SECRET
                    client.DefaultRequestHeaders.Add("hit-secret", (String)response["cache_secret"]);

                    foreach (KeyValuePair<String, object> item in response)
                    {
                        Console.WriteLine("Type " + item.Key + "\t=> " + item.Value?.GetType().FullName);
                        //            Console.WriteLine($"{item.Value["LOM"]} - {item["BNR15"]} - {item["GEB_DATR"]} - {item["SYS_BIS"]}");
                    }
                    //Console.WriteLine($"{response["daten"]} Datenzeile(n) erhalten nach {response.DurationFmt}");

                    //Request: http://localhost:5592/api/mlrp/Geburt?condition=bnr15;=;090000000001
                    //Type cache_secret       => System.String
                    //Type anfrage    => Newtonsoft.Json.Linq.JObject
                    //Type statistik  => Newtonsoft.Json.Linq.JObject
                    //Type antworten  => Newtonsoft.Json.Linq.JArray
                    //Type daten      => Newtonsoft.Json.Linq.JObject

                    //foreach (Fehler item in response.Hinweise) {
                    //  Console.WriteLine($"{item.Schwere}/{item.Plausinummer}/{item.Message}");
                    //}
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static bool wasSuccessfulResponse(HttpResponseMessage message)
        {
            Console.WriteLine("Request: " + message.RequestMessage.RequestUri.AbsoluteUri);

            string responseString = message.Content.ReadAsStringAsync().Result;
            if (message.IsSuccessStatusCode)
            {
                //        Console.WriteLine("-> "+responseString);
            }
            else
            {
                Console.WriteLine("FAILED -> " + responseString);
            }

            return message.IsSuccessStatusCode;
        }



        //--------------------------------------------------------------------

        private static HttpClient GetHttpClient(ClientMode penumClient)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri("http://localhost:5592/");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            switch (penumClient)
            {
                case ClientMode.AuthenticationHeader:
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("basic", "090000000015::900015");
                    client.DefaultRequestHeaders.Add("hit-timeout", "20");
                    break;

                case ClientMode.SelfmadeHeader:
                    client.DefaultRequestHeaders.Add("hit-bnr", "090000000015");
                    client.DefaultRequestHeaders.Add("hit-mbn", "");
                    client.DefaultRequestHeaders.Add("hit-pin", "900015");
                    client.DefaultRequestHeaders.Add("hit-secret", testSecret);
                    client.DefaultRequestHeaders.Add("hit-timeout", "20");
                    break;

                case ClientMode.NoAuthorization:
                default:
                    // keine extra Header
                    break;
            }
            System.Diagnostics.Debug.WriteLine("HttpClient:\n" + client.DefaultRequestHeaders.ToString());
            return client;
        }

        private static void pressEnterTo(String action)
        {
            Console.WriteLine("Press <Enter> to " + action + " ...");
            Console.ReadLine();
        }



        //--------------------------------------------------------------------
    }
}
