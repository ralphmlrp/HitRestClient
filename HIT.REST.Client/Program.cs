using HIT.REST.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
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
    static ApiInformation apiInfo;

    static void Main(string[] args) {
      try {
        // optional, wenn JSON-Struktur geändert, und Beispiel-Datei neu erzeugt werden soll
        // CreateJobSampleJson();

        apiInfo = ReadApiInfo();
        if (apiInfo.SuppressCertificateWarning) {
          ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
        }

        Stopwatch watch = new Stopwatch();
        watch.Start();
        int idx = 0;
        foreach (string jobFileNames in args) {
          Console.WriteLine($"{args[idx++]} args processed");

          JobInfo job = ReadJobInfo(jobFileNames);
          Console.WriteLine(job.Credentials.Betriebsnummer);
          HttpClient client = GetHttpClient(ClientMode.AuthenticationHeader, job.Credentials);
          watch.Stop();
          Console.WriteLine($"HttpClient aufgebaut: {watch.ElapsedMilliseconds} ms");
          if (client == null) {
            Console.WriteLine("Keine Verbindung möglich!");
          }
          else {
            DoTaks(client, job.TaskInfos);
          }
        }

      }
      catch (Exception ex) {
        Console.WriteLine(ex.Message);
        // TODO InnerExceptions rekursiv
        Console.WriteLine(ex.InnerException?.InnerException?.Message);
      }
      Console.WriteLine($"{args.Length} args processed");
      pressEnterTo("close console");
    }

    private static void DoTaks(HttpClient client, List<TaskInfo> taskInfos) {
      Stopwatch watch = new Stopwatch();
      foreach (TaskInfo task in taskInfos) {
        watch.Reset();
        watch.Start();
        switch (task.Action.ToUpper()) {
          case "GET":
            string condition = File.ReadAllText(task.FileName);
            GetEntity(client, task.Entity, condition);
            break;
          default:
            Console.WriteLine("Not yet implemented!");
            break;
        }
        Console.WriteLine($"Zeit für job {task.Action} - {task.Entity}: {watch.ElapsedMilliseconds} ms");
        watch.Stop();
      }
    }

    static ApiInformation ReadApiInfo() {
      ApiInformation info = (ApiInformation)ConfigurationManager.GetSection("hitSettings");
      return info;
    }

    static JobInfo ReadJobInfo(string jobFilename) {
      string text = "";
      using (StreamReader reader = new StreamReader(jobFilename)) {
        text = reader.ReadToEnd();
      }
      JobInfo jobInfo = JsonConvert.DeserializeObject<JobInfo>(text);
      return jobInfo;
    }

    //--------------------------------------------------------------------

    static void CreateJobSampleJson() {
      JobInfo info = new JobInfo();
      info.Credentials.Betriebsnummer = "09 000 000 0015";
      info.Credentials.MitBenutzer = "0";
      info.Credentials.PIN = "900015";
      info.Credentials.Timeout = 20;

      info.TaskInfos.Add(new TaskInfo() {
        Action = "IS",
        Entity = "Geburt",
        FileName = "Daten\\GeburtInsert.json"
      });

      info.TaskInfos.Add(new TaskInfo() {
        Action = "IS",
        Entity = "Zugang",
        FileName = "Daten\\ZugangInsert.json"
      });

      string jsonJob = JsonConvert.SerializeObject(info);
      File.WriteAllText("Daten\\JobSample.json", jsonJob);
    }

    
    static void CreateApiInfoPartXml() {
      ApiInformation info = new ApiInformation();
      info.BasePath = "/api/mlrp";
      info.SuppressCertificateWarning = true;
      info.BaseUrls = new List<string>()
      {
          "https://www.hi-tier.bybn.de/",
          "https://www-dev.hi-tier.bybn.de/",
          "http://localhost:5592/"
      };

      XmlSerializer serializer = new XmlSerializer(typeof(ApiInformation));
      using (FileStream fs = new FileStream("C:\\temp\\apiInfo.txt", System.IO.FileMode.Create)) {
        serializer.Serialize(fs, info);
      }
    }



    //--------------------------------------------------------------------

    //private static void start(string[] args)
    //{
    //    HttpClient client;

    //    // Test ohne Auth:
    //    client = GetHttpClient(ClientMode.NoAuthorization);

    //    GetGeburten(client);

    //    pressEnterTo("continue with Authentication header");

    //    // Test mit Auth-Header:
    //    client = GetHttpClient(ClientMode.AuthenticationHeader);

    //    GetGeburten(client);

    //    pressEnterTo("continue with selfmade header");

    //    // Test ohne Auth:
    //    client = GetHttpClient(ClientMode.SelfmadeHeader);

    //    GetGeburten(client);
    //}



    //--------------------------------------------------------------------


    private static void GetEntity(HttpClient client, string entity, string condition) {
      // condition =bnr15;=;090000000001
      HttpResponseMessage message = client.GetAsync($"{apiInfo.BasePath}/{entity}?condition={condition}").Result;
      try {
        if (wasSuccessfulResponse(message)) {

          // Console.WriteLine(message.Content.ReadAsStringAsync().Result);

          // es kommt zwar als HitDataTree zurück, kann aber nicht als solches deserialisiert werden
          // Daher klassisch mit Dictionary:
          Dictionary<String, Object> response = message.Content.ReadAsAsync<Dictionary<String, Object>>().Result;

          // ein vergebenes/übergebenes Secret dem Client geben
          client.DefaultRequestHeaders.Remove("hit-secret");                              // Name siehe   MlrpRestController.HTTP_HEADER_AUTH_SECRET
          client.DefaultRequestHeaders.Add("hit-secret", (String)response["cache_secret"]);

          foreach (KeyValuePair<String, object> item in response) {
            Console.WriteLine("Type " + item.Key + "\t=> " + item.Value?.GetType().FullName);
            //      Console.WriteLine($"{item.Value["LOM"]} - {item["BNR15"]} - {item["GEB_DATR"]} - {item["SYS_BIS"]}");
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
      catch (Exception ex) {
        Console.WriteLine(ex);
      }
    }

    private static bool wasSuccessfulResponse(HttpResponseMessage message) {
      Console.WriteLine("Request: " + message.RequestMessage.RequestUri.AbsoluteUri);
      string responseString = message.Content.ReadAsStringAsync().Result;
      if (message.IsSuccessStatusCode) {
        //        Console.WriteLine("-> "+responseString);
      }
      else {
        Console.WriteLine("FAILED -> " + responseString);
      }
      return message.IsSuccessStatusCode;
    }


    //--------------------------------------------------------------------

    private static HttpClient GetHttpClient(ClientMode penumClient, Credentials credentials) {
      foreach (var url in apiInfo.BaseUrls) {
        Console.WriteLine($"Connect to Url: {url} BasePath: {apiInfo.BasePath}");

        HttpClient client = new HttpClient();
        client.BaseAddress = new Uri(url);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        switch (penumClient) {
          case ClientMode.AuthenticationHeader:
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("basic", $"{credentials.Betriebsnummer}:{credentials.MitBenutzer}:{credentials.PIN}");
            client.DefaultRequestHeaders.Add("hit-timeout", credentials.Timeout.ToString());
            break;

          case ClientMode.SelfmadeHeader:
            client.DefaultRequestHeaders.Add("hit-bnr", credentials.Betriebsnummer);
            client.DefaultRequestHeaders.Add("hit-mbn", credentials.MitBenutzer);
            client.DefaultRequestHeaders.Add("hit-pin", credentials.PIN);
            client.DefaultRequestHeaders.Add("hit-secret", testSecret);
            client.DefaultRequestHeaders.Add("hit-timeout", credentials.Timeout.ToString());
            break;

          case ClientMode.NoAuthorization:
          default:
            // keine extra Header
            break;
        }

        try {
          HttpResponseMessage message = client.GetAsync(apiInfo.BasePath).Result;
          message.EnsureSuccessStatusCode();
          if (message.Content.ReadAsAsync<bool>().Result) {
            System.Diagnostics.Debug.WriteLine("HttpClient:\n" + client.DefaultRequestHeaders.ToString());
            return client;
          }
        }
        catch (Exception) {
        }
      }
      return null;
    }

    private static void pressEnterTo(String action) {
      Console.WriteLine("Press <Enter> to " + action + " ...");
      Console.ReadLine();
    }

    //--------------------------------------------------------------------
  }
}