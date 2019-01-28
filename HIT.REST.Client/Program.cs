using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Xml.Serialization;

using HIT.REST.Client.config;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;



namespace HIT.REST.Client {

  /// <summary>Art der Autorisierung</summary>
  enum ClientMode {
    /// <summary>Anfrage ohne Autorisierung</summary>
    NoAuthorization,
    /// <summary>Anfrage mit "basic"-Autorisierungskopfzeile in der Form "bnr:mbn:pin"</summary>
    AuthenticationHeader,
    /// <summary>Anfrage mit eigenen HTTP-Kopfzeilen wie "hit-bnr" etc</summary>
    SelfmadeHeader,
  }



  class Program {
//--------------------------------------------------------------------

    static String testSecret = Guid.NewGuid().ToString().Substring(9, 14);      // Mittelteil aus GUID nehmen   z.B. "45f1-affe-87f3"

    static HitSettingsSection   config;

//--------------------------------------------------------------------

    static void Main(string[] args) {
      try {
        //CreateJson();

        // app.config verarbeiten:
        config = (HitSettingsSection)ConfigurationManager.GetSection("hitSettings");

        if (config.CertificateWarning.ignore) {
          // richte Callback ein, der bei der ServerCertificateValidation
          // immer true liefert (also auch, wenn das Zertifikat einen Fehler liefert)
          ServicePointManager.ServerCertificateValidationCallback +=  (sender,cert,chain,sslPolicyErrors) => true;
        }

        Console.WriteLine("Base path: "+config.BasePath.path);

        // die eigentliche Aufgabe erledigen:
        // die als Parameter angegebenen JSON-Dateien einlesen und HIT-REST-Anfragen stellen
//        run(args);
      }
      catch (Exception e) {
Console.WriteLine(e.ToString());

        Console.WriteLine(e.GetType().FullName+": "+e.Message);
        Exception ie = e.InnerException;
        int intLevel = 0;
        while (ie != null)  {
          intLevel+=2;
          Console.WriteLine(new String(' ',intLevel)+"^- "+ie.GetType().FullName+": "+ie.Message);
          ie = ie.InnerException;
        }
      }

      pressEnterTo("exit");
    }



//--------------------------------------------------------------------

    private static void run(String[] pstrJobFiles) {
      Stopwatch watch = new Stopwatch();

      foreach (var item in pstrJobFiles) {
        watch.Start();
        Job job = ReadJob(item);
        Console.WriteLine(job.Credentials.Betriebsnummer);
        HttpClient client = GetHttpClient(ClientMode.AuthenticationHeader, job.Credentials);
        watch.Stop();
        if (client == null) {
          Console.WriteLine($"Keine Verbindung möglich!? (nach {watch.ElapsedMilliseconds}ms)");
        }
        else {
          Console.WriteLine($"HttpClient in {watch.ElapsedMilliseconds}ms aufgebaut");
          watch.Start();
          DoTasks(client,job);
          watch.Stop();
          Console.WriteLine(Helper.getForNum(job.Tasks.Count,"Ein Task","* Tasks","*")+$" in {watch.ElapsedMilliseconds}ms verarbeitet");
        }
      }
    }

    private static void DoTasks(HttpClient client,Job pobjJob) {
      List<Task> tasks = pobjJob.Tasks;
      Stopwatch watch = new Stopwatch();
      foreach (Task task in tasks) {
        watch.Reset();
        watch.Start();
        switch (task.Action.ToUpper()) {
          case "RS":
          case "RT":
          case "RB":
            string condition = File.ReadAllText(task.FileName);
            GetEntity(client,task.Entity,condition);
            break;
          default:
            Console.WriteLine("Not yet implemented!");
            break;
        }
        Console.WriteLine($"Zeit für job \"{task.Action} - {task.Entity}\": {watch.ElapsedMilliseconds}ms");
        watch.Stop();
      }
    }

    static Job ReadJob(string jobFilename) {
      string text = "";

      using (StreamReader reader = new StreamReader(jobFilename)) {
        text = reader.ReadToEnd();
      }

      Job info = JsonConvert.DeserializeObject<Job>(text);
      return info;
    }

//--------------------------------------------------------------------

    static void CreateJson() {
      Job info = new Job();
      info.Credentials.Betriebsnummer = "09 000 000 0015";
      info.Credentials.MitBenutzer    = "0";
      info.Credentials.PIN            = "900015";
      info.Credentials.Timeout        = 20;

      info.Tasks.Add(new Task() {
        Action    = "RS",
        Entity    = "Geburt",
        FileName  = "Daten\\GeburtRequest.txt"
      });

      info.Tasks.Add(new Task() {
        Action    = "RS",
        Entity    = "Zugang",
        FileName  = "Daten\\ZugangRequest.txt"
      });

      string jsonJob = JsonConvert.SerializeObject(info);
      File.WriteAllText("Daten\\jobSample.json",jsonJob);
    }

    static void CreateXml() {
      ApiInformation info = new ApiInformation();
      info.BasePath = "/api/mlrp/";
      info.SuppressCertificateWarning = true;
      info.BaseUrls = new List<string>()  {
        "https://www.hi-tier.bybn.de/",
        "https://www-dev.hi-tier.bybn.de/",
        "http://localhost:5592/"
      };

      XmlSerializer serializer = new XmlSerializer(typeof(ApiInformation));
      using (FileStream fs = new FileStream("C:\\temp\\apiInfo.txt",System.IO.FileMode.Create)) {
        serializer.Serialize(fs,info);
      }



      //start(args);


      pressEnterTo("quit");
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


    private static void GetEntity(HttpClient client,string entity,string condition) {
      // condition =bnr15;=;090000000001
      String strUrl = $"{config.BasePath}{entity}?condition={condition}";
      HttpResponseMessage message = client.GetAsync(strUrl).Result;
      try {
        if (wasSuccessfulResponse(message)) {

          // Console.WriteLine(message.Content.ReadAsStringAsync().Result);


          // es kommt zwar als HitDataTree zurück, kann aber nicht als solches deserialisiert werden
          // Daher klassisch mit Dictionary:
          Dictionary<String, Object> response = message.Content.ReadAsAsync<Dictionary<String, Object>>().Result;

          // ein vergebenes/übergebenes Secret dem Client geben
          client.DefaultRequestHeaders.Remove("hit-secret");                              // Name siehe   MlrpRestController.HTTP_HEADER_AUTH_SECRET
          client.DefaultRequestHeaders.Add("hit-secret",(String)response["cache_secret"]);
          /*
          foreach (KeyValuePair<String,object> item in response) {
            Console.WriteLine("Type " + item.Key + "\t=> " + item.Value?.GetType().FullName);
            //  Console.WriteLine($"{item.Value["LOM"]} - {item["BNR15"]} - {item["GEB_DATR"]} - {item["SYS_BIS"]}");
          }
          */
          JObject objDaten = (JObject)response["daten"];
          //Console.WriteLine(objDaten);
          JArray objDatenDerEntity = (JArray)objDaten[entity];

          Console.WriteLine($"{objDatenDerEntity.Count} Datenzeile(n) für {entity} erhalten nach {((JObject)response["statistik"])["dauer_fmt"]}");

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

    private static HttpClient GetHttpClient(ClientMode penumClient,Credentials credentials) {
      foreach (BaseUrlElement element in config.BaseUrls) {
        Console.WriteLine($"GetHttpClient() mit {element.SchemeAndDomainUrl} ...");

        HttpClient client = new HttpClient();
        client.BaseAddress = new Uri(element.SchemeAndDomainUrl);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        switch (penumClient) {
          case ClientMode.AuthenticationHeader:
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("basic",$"{credentials.Betriebsnummer}:{credentials.MitBenutzer}:{credentials.PIN}");
            client.DefaultRequestHeaders.Add("hit-timeout",credentials.Timeout.ToString());
            break;

          case ClientMode.SelfmadeHeader:
            client.DefaultRequestHeaders.Add("hit-bnr",credentials.Betriebsnummer);
            client.DefaultRequestHeaders.Add("hit-mbn",credentials.MitBenutzer);
            client.DefaultRequestHeaders.Add("hit-pin",credentials.PIN);
            client.DefaultRequestHeaders.Add("hit-secret",testSecret);
            client.DefaultRequestHeaders.Add("hit-timeout",credentials.Timeout.ToString());
            break;

          case ClientMode.NoAuthorization:
          default:
            // keine extra Header
            break;
        }

        try {
          HttpResponseMessage message = client.GetAsync(config.BasePath.path).Result;
          message.EnsureSuccessStatusCode();
          if (message.Content.ReadAsAsync<bool>().Result) {
            Console.WriteLine("-> verbunden!");
            return client;
          }
        }
        catch (Exception e) {
            Console.WriteLine("-> Fehler: "+e.Message);
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
