using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

using HIT.REST.Client.config;
using HIT.REST.Client.Hit;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;



namespace HIT.REST.Client {



  class Program {
//--------------------------------------------------------------------

    static String testSecret = Guid.NewGuid().ToString().Substring(9, 14);      // Mittelteil aus GUID nehmen   z.B. "45f1-affe-87f3"

    internal static HitSettingsSection    staticConfig;

    internal static TextWriter            staticLogfile;



//--------------------------------------------------------------------

    static void Main(string[] args) {

      //URI objTest = new URI();
      //objTest.Host = "localhost";
      //objTest.BasePath = "der/hier/ist/unveränderlich";
      //objTest.RestPath = "ZUGANG";
      //objTest.Query.Add("bnr","09 000 000 0001");
      //objTest.Query.Add("pin","900001");
      //objTest.Query.Add("timeout","11");

      //log(objTest);
      //pressEnterTo("exit");
      //return;

      try {
        // eigene Section aus app.config einlesen
        staticConfig = (HitSettingsSection)ConfigurationManager.GetSection("hitSettings");

        log("Available base URLs:");
        foreach (BaseUrlElement path in staticConfig.BaseUrls) log("* "+path.SchemeAndDomainUrl);
        log("Base path          : "+staticConfig.BasePath.path);
        log("Certificate warning: "+(staticConfig.CertificateWarning.ignore ? "suppress" : "alert"));
        log();

        if (staticConfig.CertificateWarning.ignore) {
          // richte Callback ein, der bei der ServerCertificateValidation
          // immer true liefert (also auch, wenn das Zertifikat einen Fehler liefert)
          ServicePointManager.ServerCertificateValidationCallback +=  (sender,cert,chain,sslPolicyErrors) => true;
        }

        // die eigentliche Aufgabe erledigen:
        // die als Parameter angegebenen JSON-Dateien einlesen und HIT-REST-Anfragen stellen
        run(args);
      }
      catch (Exception e) {
//Console.WriteLine(e.ToString());

        log(e.GetType().FullName+": "+e.Message);
        Exception ie = e.InnerException;
        int intLevel = 0;
        while (ie != null) {
          intLevel+=2;
          log(new String(' ',intLevel)+"^- "+ie.GetType().FullName+": "+ie.Message);
          ie = ie.InnerException;
        }
      }

      closeLog();
      pressEnterTo("exit");
    }



//--------------------------------------------------------------------

    /// <summary>
    /// We only accept job filepaths as CLI parameters.
    /// </summary>
    /// <param name="pstrJobFiles"></param>
    private static void run(String[] pstrJobFiles) {
      foreach (String strJobFile in pstrJobFiles) {
        runJob(strJobFile);
      }
    }



    private static int staticJobCounter = 0;

    /// <summary>
    /// Führe den Job aus, indem seine Tasks der Reihe nach an HIT3-REST gesendet werden.
    /// </summary>
    /// <param name="pstrJobPath"></param>
    public static void runJob(String pstrJobPath) {
      // deserialize Job description
      Job objJob = Job.fromFile(pstrJobPath);

      // vorbereiten
      Stopwatch total = new Stopwatch();

      Credentials objCred = objJob.Credentials;

      staticJobCounter++;
      tee(new String('-',16));
      tee("Job     : #"+staticJobCounter+" mit "+objCred.getUser());
      tee("Login   : "+objCred.getUser());
      tee("AuthMode: "+Enum.GetName(typeof(AuthMode),objCred.AuthenticationMode));
      tee("Tasks   : "+Helper.getForNum(objJob.Tasks.Count,"eine Anfrage","* Anfragen","*"));
      tee(new String('-',16));

      // unseren RestClient vorbereiten, der sich um die Kommunikation mit HIT3-REST kümmert
      RestClient objClient = new RestClient(staticConfig);

      // jetzt einfach einen Task nach dem anderen abarbeiten
      int intTaskCounter = 0;
      foreach (Task task in objJob.Tasks) {
        intTaskCounter++;

        String strDisplay = String.IsNullOrWhiteSpace(task.Description) ? "mit "+task.GetHitCommand()+":"+task.Entity : task.Description;
        tee("");
        tee("Task "+strDisplay);

        switch (task.GetVerb()) {
          case RestClient.Verb.Get:
            processGet(task,objClient,objCred);
            break;
          case RestClient.Verb.Put:
            break;
          case RestClient.Verb.Post:
            break;
          case RestClient.Verb.Delete:
            break;
          default:
            break;
        }



      }
    }


//--------------------------------------------------------------------

    private static void processGet(Task pobjTask,RestClient pobjClient,Credentials pobjCred)  {
      Stopwatch watch = new Stopwatch();
      watch.Start();

      // URI bauen
      URI objRequest = pobjTask.CreateURI(pobjClient,pobjCred); // der Task braucht den Client, damit ggf. ein Secret übernommen werden kann

      // bei GET arbeiten wir die Input-Datei Zeile für Zeile ab und setzen ein RS ab


      // CONTINUE HERE

        HttpResponseMessage objResponse = pobjClient.send(objRequest);
        if (objResponse == null) {
          tee($"-> Senden nach {watch.ElapsedMilliseconds}ms fehlgeschlagen!?");
        }
        else {
          tee($"-> Antwort nach  {watch.ElapsedMilliseconds}ms");

          // analyze objResponse
          // TODO

        }

      watch.Stop();

    }


    private static void doQuery(HttpClient client,Credentials pobjLogin,Task pobjTask) {
      // erst mal alle Zeilen aus der Inputdatei lesen
      List<String> astrLines = new List<String>(File.ReadAllLines(pobjTask.InputPath));
      // es müssen mind. 2 sein
      if (astrLines.Count < 2) {
        writeFailure(pobjTask,"Datei '"+pobjTask.InputPath+"' für Abfrage enthält zuwenig Zeilen!");
        return;
      }

      string strCond =  astrLines[0]; astrLines.RemoveAt(0);

      // URL
      //      String strURL = po

      GetEntity(client,pobjTask.Entity,strCond);



    }



//--------------------------------------------------------------------

    /// <summary>
    /// Schreibe eine Fehlermeldung in die gegebene Ausgabedatei oder
    /// falls <see cref="Task"/> unbekannt oder Ausgabedatei nicht
    /// schreibbar, in die Console.
    /// </summary>
    private static void writeFailure(Task pobjTask,String pstrError) {
      if (pobjTask != null) {
        // bereite Fehlermeldung als JSON vor
        String strContent = JsonConvert.SerializeObject(new { message = pstrError });
        // schreibe sie in OutputFile
        try {
          File.WriteAllText(pobjTask.OutputPath,strContent);
          return;
        }
        catch (Exception) {
          // fehlgeschlagen, unten weitermachen
        }
      }

      // hier angekommen war Schreiben nicht möglich, also ab in die Console
      Console.WriteLine("FEHLER: "+pstrError);
    }

    /*
          private static void start(string[] args) {
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
    */

//--------------------------------------------------------------------


    private static void GetEntity(HttpClient client,string entity,string condition) {
      // condition =bnr15;=;090000000001
      String strUrl = $"{staticConfig.BasePath}{entity}?condition={condition}";
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
/*
    private static HttpClient GetHttpClient(Credentials credentials) {
      foreach (BaseUrlElement element in staticConfig.BaseUrls) {
        Console.WriteLine($"GetHttpClient() mit {element.SchemeAndDomainUrl} ...");

        HttpClient client = new HttpClient();
        client.BaseAddress = new Uri(element.SchemeAndDomainUrl);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        switch (credentials.AuthenticationMode) {
          case AuthMode.AuthenticationHeader:
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("basic",$"{credentials.Betriebsnummer}:{credentials.Mitbenutzer}:{credentials.PIN}");
            client.DefaultRequestHeaders.Add("hit-timeout",credentials.Timeout.ToString());
            break;

          case AuthMode.SelfmadeHeader:
            client.DefaultRequestHeaders.Add("hit-bnr",credentials.Betriebsnummer);
            client.DefaultRequestHeaders.Add("hit-mbn",credentials.Mitbenutzer);
            client.DefaultRequestHeaders.Add("hit-pin",credentials.PIN);
            client.DefaultRequestHeaders.Add("hit-secret",testSecret);
            client.DefaultRequestHeaders.Add("hit-timeout",credentials.Timeout.ToString());
            break;

          case AuthMode.NoAuth:
          default:
            // keine extra Header
            // bei AuthMode.QueryString müssen die zusätzlich zur Anfrage
            break;
        }

        try {
          HttpResponseMessage message = client.GetAsync(staticConfig.BasePath.path).Result;
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
*/


//--------------------------------------------------------------------

    public static void log() {
      log("");
    }
    public static void log(object anything) {
      log(anything == null ? "null" : anything.ToString());
    }
    public static void log(String pstrMsg,bool pboolFileOnly = false) {
      if (staticLogfile == null) {
        String strDelim = new String('-',60);
        try {
          staticLogfile = new StreamWriter(staticConfig.LogFile.path,staticConfig.LogFile.append,Encoding.UTF8);
          staticLogfile.WriteLine(strDelim);
          staticLogfile.WriteLine("Log start: "+DateTime.Now.ToString("ddd, dd-MMM-yyyy HH:mm:ss"));
          staticLogfile.WriteLine(strDelim);
        }
        catch (Exception e) {
          staticLogfile = Console.Error;
          staticLogfile.WriteLine(strDelim);
          staticLogfile.WriteLine("Could not write to Logfile '"+staticConfig?.LogFile?.path+"'! ("+e+")");
          staticLogfile.WriteLine(strDelim);
        }
      }

      // write
      if (staticLogfile == Console.Error) {
        // auf Console nur, wenn gewünscht
        if (!pboolFileOnly) staticLogfile.WriteLine(pstrMsg);
      }
      else {
        // immer in's Logfile
        staticLogfile.WriteLine(pstrMsg);
      }
    }

    public static void tee(String pstrMsg) {
      Console.Out.WriteLine(pstrMsg);
      log(pstrMsg,true);
    }



    private static void closeLog() {
      if (staticLogfile == null) return;

      if (staticLogfile != Console.Error) {
        staticLogfile.Flush();
        staticLogfile.Close();
      }
      staticLogfile = null;
    }



    private static void pressEnterTo(String action) {
      Console.WriteLine("Press <Enter> to " + action + " ...");
      Console.ReadLine();
    }



//--------------------------------------------------------------------
  }
}
