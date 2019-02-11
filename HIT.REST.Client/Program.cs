﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
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
        foreach (BaseUrlElement path in staticConfig.BaseUrls) log("* "+path.BaseUrl);
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
Console.WriteLine(e.ToString());

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
      if (!testServices())  return;

      foreach (String strJobFile in pstrJobFiles) {
        runJob(strJobFile);
      }
    }



    /// <summary>
    /// Finde den HIT3-REST-Service, der auf unsere Anfragen reagieren soll
    /// </summary>
    /// <returns></returns>
    private static bool testServices() {

      // unseren RestClient vorbereiten, der sich um die Kommunikation mit HIT3-REST kümmert
      RestClient          objClient   = new RestClient(staticConfig);
      HttpResponseMessage objResponse = null;

      // Setzen von objClient.Credentials nicht nötig

      while (objResponse == null) {
        // der Client gibt die Basis-URL je nach AuthenticationMode vor
        URI objRequest = objClient.CreateURI();
        // gibt's keine URI, ist kein Client (und somit Service) verfügbar
        if (objRequest == null) break;

        // die URI reicht schon für eine Test-Anfrage
        log("Anfrage an "+objRequest+":");
        Dictionary<String,Object> objContent = objClient.sendGET(objRequest,out objResponse);
        if (objResponse == null)  {
          // der Service reagierte nicht, den nächsten versuchen
          log("-> keine Reaktion!");
          objClient.forceNextHost();
          continue;
        }

        // es kam eine Antwort zurück
        if (objResponse.IsSuccessStatusCode)  {
          // Kommt da auch die Versionsnummer zurück?
          try {
            JArray  fehlerliste = (JArray)objContent["Fehlerliste"];
            JObject firstError  = (JObject)fehlerliste[0];
            String  strMsg      = (String)firstError["Message"];
            if (strMsg.StartsWith("Version "))  {
              // sieht nach korrekter Antwort aus
              log("-> HIT3 REST "+strMsg+" gefunden");
              continue;
            }
          }
          catch (Exception) {}
        }
        else  {
          log("-> falscher Request - HTTP Status "+((int)objResponse.StatusCode)+" "+objResponse.ReasonPhrase);
          objClient.forceNextHost();
        }

        objResponse = null; // bedeutet: nächsten Host probieren
      }

      return (objResponse != null);
    }





    private static int staticJobCounter = 0;

    /// <summary>
    /// Führe den Job aus, indem seine Tasks der Reihe nach an HIT3-REST gesendet werden.
    /// </summary>
    /// <param name="pstrJobPath"></param>
    public static void runJob(String pstrJobPath) {
      // deserialize Job description
      Job objJob = Job.fromFile(pstrJobPath);
      if (objJob == null) return; // Fehlermeldung ist bereits protokolliert

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

      // der Client braucht die Credentials, also
      objClient.Credentials  = objCred;

      // jetzt einfach einen Task nach dem anderen abarbeiten
      int intTaskCounter = 0;
      foreach (Task task in objJob.Tasks) {
        intTaskCounter++;

        tee("");
        tee("Task "+task.Display);
        tee("-> Verb "+task.GetVerb());

        switch (task.GetVerb()) {
          case RestClient.Verb.Get:     // Abfragen RS
            processQuery(task,objClient,objCred);
            break;
          case RestClient.Verb.Put:
          case RestClient.Verb.Post:
          case RestClient.Verb.Delete:
            processSendData(task,objClient,objCred);
            break;
          default:
            break;
        }
      }

      // am Ende zusätzlich ein explizites Beenden der REST-Sitzung durchführen
      // (sofern eine Sitzung existiert)
      if (objClient.Secret != null) {
        URI objRequest = objClient.CreateURI(true);
        //objRequest.Query["bnr"] = objCred.Betriebsnummer;
        //if (!String.IsNullOrWhiteSpace(objCred.Mitbenutzer))  {
        //  objRequest.Query["mbn"] = objCred.Mitbenutzer;
        //}
        if (objCred.AuthenticationMode == AuthMode.QueryString) {
          objRequest.Query["cacheTimeout"]  = "-1";
          objRequest.Query["cacheSecret"]   = objClient.Secret;             
        }
        objRequest.RestPath = "session";    // eigene Entität für die Beendigung der Session
        HttpResponseMessage objResponse;
        Dictionary<String,Object> objContent = objClient.sendDELETE(objRequest,null,out objResponse);
        if (objResponse == null) {
          tee($"-> Senden mit Timeout=-1 fehlgeschlagen!?");
        }
        else {
          tee($"-> Abgemeldet.");
          log("Status: "+((int)objResponse.StatusCode)+" "+objResponse.ReasonPhrase);
        }
      }
    }



//--------------------------------------------------------------------

    /// <summary>
    /// Verarbeite ein Batch-ähnliches Abfragen von HIT.
    /// Es wird dabei für den gegebenen Task und dazugehörige Anmeldedaten
    /// eine Datei ausgelesen, die in der ersten Zeile die gewünschten
    /// Ausgabespalten und in den folgenden Zeilen die Abfragen (im CSV-Format)
    /// enthält. Die Ergebnisse werden in die beim Task angegebene Datei
    /// weggeschrieben.
    /// </summary>
    /// <param name="pobjTask"></param>
    /// <param name="pobjClient"></param>
    /// <param name="pobjCred">Für die URI, um ggf. Anmeldeinformationen mitgeben zu können</param>
    private static void processQuery(Task pobjTask,RestClient pobjClient,Credentials pobjCred)  {
      if (!pobjTask.IsQuery()) throw new ArgumentException(nameof(pobjTask),"Nur ein Task für eine Abfrage ist zulässig.");

      Stopwatch watch = new Stopwatch();

      // Input-Datei öffnen
      using (TextReader objIn = new StreamReader(pobjTask.InputPath)) {

        // die erste Zeile enthält die Ausgabespalten:
        String strColumns = objIn.ReadLine();
        log("GET '"+pobjTask.Display+"': columns="+strColumns);
        bool headerWritten = false;

        // wir schreiben eine Ausgabedatei mit den Datenzeilen
        using (TextWriter objOut = new StreamWriter(pobjTask.OutputPath,true,new UTF8Encoding(false))) {    // we always append in this demo client

          // alle weiteren Zeilen enthalten je eine Abfragebedingung
          String strLine;
          int    intLines = 0;
          while ((strLine = objIn.ReadLine()) != null)  {
            ++intLines;
            watch.Reset();
            watch.Start();

            // die Zeile ist die Bedingung, also zusammenstellen:
            URI objRequest = pobjTask.CreateURI(pobjClient,pobjCred); // der Task braucht den Client, damit ggf. ein Secret übernommen werden kann
            objRequest.Query.Add("columns",   strColumns);
            objRequest.Query.Add("condition", strLine);

            // Senden
            HttpResponseMessage objResponse;
            Dictionary<String,Object> objContent = pobjClient.sendGET(objRequest,out objResponse);
            // objContent enthält in C# ein JObject mit mehreren Schlüsseln und Werten, die wiederum JObjects und JArrays sind
            // der send() hat einen erzeugten Secret bereits extrahiert, d.h. man muss sich nicht mehr kümmern
            watch.Stop();
            if (objResponse == null) {
              tee($"-> Senden nach {watch.ElapsedMilliseconds}ms fehlgeschlagen!?");
            }
            else {
              tee($"-> Antwort nach  {watch.ElapsedMilliseconds}ms");

              log("Status: "+((int)objResponse.StatusCode)+" "+objResponse.ReasonPhrase);
              if (objResponse.IsSuccessStatusCode)  {
                // aus der Antwort das relevante extrahieren: Daten und Antwortzeilen
                JObject objEntityDaten  = (JObject)objContent["daten"       ];
                // objEntityDaten enthält Basisentität -> Liste von Datenzeilen
                // da wir hier nur eine Entität abfragen, bekommen wir auch nur eine Datenliste:
                JArray  objDaten        = (JArray )objEntityDaten[pobjTask.Entity];
                JArray  objAntworten    = (JArray )objContent["antworten"   ];
                // objDaten enthält Liste von Antwortzeilen

                // die Daten kommen in die Ausgabedatei
                if (!headerWritten && objDaten.Count > 0) {
                  // wir haben noch keinen Header geschrieben, also:
                  bool first = true;
                  foreach (JProperty objCols in objDaten[0])  {
                    if (first) first = false; else objOut.Write(";");
                    objOut.Write(objCols.Name);
                  }
                  objOut.WriteLine(""); // EndOfLine
                }

                // Zeile für Zeile die Daten
                foreach (JToken objRows in objDaten) {
                  bool first = true;
                  foreach (JProperty objCols in objRows)  {
                    if (first) first = false; else objOut.Write(";");
                    objOut.Write(objCols.Value.ToString());
                  }
                  objOut.WriteLine(""); // EndOfLine
                }
              }
              else  {
                // Fehler holen:
                String strErr = (String)objContent["Message"];
                tee("Fehler: "+strErr);
                break;
              }
            }
          }

          tee("-> "+Helper.getForNum(intLines,"Eine Abfrage","* Abfragen","*")+" verarbeitet");
        }

        // wenn Ausgabedatei leer, dann löschen
        try {
          if (new FileInfo(pobjTask.OutputPath).Length == 0)  {
            File.Delete(pobjTask.OutputPath);
            log("Gelöscht: "+pobjTask.OutputPath);
          }
        }
        catch (Exception) { log("Could not delete empty "+pobjTask.OutputPath); }
      }

    }



    /// <summary>
    /// Verarbeite ein Batch-ähnliches Melden von Daten an HIT.
    /// Es wird dabei für den gegebenen Task und dazugehörige Anmeldedaten
    /// eine Datei ausgelesen, die in der ersten Zeile die
    /// Spaltennamem und in den folgenden Zeilen die Datenspalten (im CSV-Format)
    /// enthält. Spalten und Daten werden in ein JSON-Format gebracht und als
    /// HttpContent gesendet.
    /// </summary>
    /// <param name="pobjTask"></param>
    /// <param name="pobjClient"></param>
    /// <param name="pobjCred">Für die URI, um ggf. Anmeldeinformationen mitgeben zu können</param>
    private static void processSendData(Task pobjTask,RestClient pobjClient,Credentials pobjCred) {
      if (pobjTask.IsQuery()) throw new ArgumentException(nameof(pobjTask),"Nur ein Task für Meldungen ist zulässig.");


      RestClient.Verb enumVerb =  pobjTask.GetVerb();

      // Input-Datei öffnen
      using (TextReader objIn = new StreamReader(pobjTask.InputPath)) {

        // die erste Zeile enthält die Datenspalten:
        String strColumns = objIn.ReadLine();
        log(enumVerb+" '"+pobjTask.Display+"' with columns "+strColumns);

        String[] astrCols = strColumns.Split(';');

        // alle weiteren Zeilen enthalten je eine Datenzeile für PUT/POST/DELETE
        // wird Blocksatz benutzt, dann werden erst mal Zeilenweise so lange Daten gesammelt
        // und gespeichert, bis man sie als Ganzes ausgibt
        String strLine;
        int    intLines = 0;
        List<Dictionary<String,String>> objBlock = null;
        while ((strLine = objIn.ReadLine()) != null)  {
          ++intLines;

          String[] astrDatas = strLine.Split(';');
          if (astrCols.Length != astrDatas.Length)  {
            throw new ArgumentException("Anzahl Spaltennamen (="+astrCols.Length+") und Daten (="+astrDatas.Length+") sind nicht identisch!");
          }

          // Datensatz bilden:
          // da wir ein JObject benötigen, bauen wir eines aus dieser Zeile via Dictionary
          Dictionary<String,String> objJObject = new Dictionary<String,String>();
          for (int i=0; i<astrCols.Length; ++i) {
            objJObject[astrCols[i]] = astrDatas[i];
          }

          // der HttpContent legt den Inhalt der Anfrage fest
          HttpContent objHttpPost;
          if (pobjTask.Blocksize != null && pobjTask.Blocksize > 1) {
            if (objBlock == null) objBlock = new List<Dictionary<String,String>>();

            // Datensatz in Liste
            objBlock.Add(objJObject);

            // ist die Liste so lang wie Blocksize, dann HttpContent vorbereiten
            if (objBlock.Count == pobjTask.Blocksize) {
              // konvertiere in JSON
              objHttpPost = new StringContent(JsonConvert.SerializeObject(objBlock),Encoding.UTF8,"application/json");
              // Liste von JObjects leeren für nächsten Block
              objBlock.Clear();
            }
            else  {
              // wenn Block noch nicht voll, dann verarbeite nächste Zeile
              continue;
            }
          }
          else  {
            // wir haben keinen Blocksatz, also nur den Datensatz als solches senden
            objHttpPost = new StringContent(JsonConvert.SerializeObject(objJObject),Encoding.UTF8,"application/json");
          }

          if (!sendDataAndHandleResult(pobjTask,objHttpPost,pobjClient,pobjCred)) {
            // brich ab, wenn Senden und Empfangen fehlschlug
            break;
          }

        }

        // hat der Block noch Datensätze, dann abschließend senden
        if (objBlock.Count > 0) {
          // konvertiere in JSON
          HttpContent objHttpPost = new StringContent(JsonConvert.SerializeObject(objBlock),Encoding.UTF8,"application/json");
          sendDataAndHandleResult(pobjTask,objHttpPost,pobjClient,pobjCred);
        }         

        tee("-> "+Helper.getForNum(intLines,"Eine Abfrage","* Abfragen","*")+" verarbeitet");
      }
    }




    private static bool sendDataAndHandleResult(Task pobjTask,HttpContent pobjContent,RestClient pobjClient,Credentials pobjCred) {
      RestClient.Verb enumVerb =  pobjTask.GetVerb();

      Stopwatch watch = new Stopwatch();
      watch.Start();

      // die Zeile ist die Bedingung, also zusammenstellen:
      URI objRequest = pobjTask.CreateURI(pobjClient,pobjCred); // der Task braucht den Client, damit ggf. ein Secret übernommen werden kann

      // Senden per zum Task passenden Verb
      HttpResponseMessage       objResponse;
      Dictionary<String,Object> objContent  = null;
      switch (enumVerb) {
        case RestClient.Verb.Put:
          objContent = pobjClient.sendPUT(objRequest,pobjContent,out objResponse);
          break;
        case RestClient.Verb.Post:
          objContent = pobjClient.sendPOST(objRequest,pobjContent,out objResponse);
          break;
        case RestClient.Verb.Delete:
          objContent = pobjClient.sendDELETE(objRequest,pobjContent,out objResponse);
          break;

        default:
          tee("Falsches Verb "+enumVerb+"!");
          return false;
      }

      // die Antwort, die wir erhalten, enthält nur Fehlermeldungen;
      // die schreiben wir einfach nur ins Logfile
      watch.Stop();
      if (objResponse == null) {
        tee($"-> Senden nach {watch.ElapsedMilliseconds}ms fehlgeschlagen!?");
        return false;
      }


      tee($"-> Antwort nach  {watch.ElapsedMilliseconds}ms");

      log("Status: "+((int)objResponse.StatusCode)+" "+objResponse.ReasonPhrase);
      if (objResponse.IsSuccessStatusCode)  {
        // aus der Antwort das relevante extrahieren: Daten und Antwortzeilen
        JObject objEntityDaten  = (JObject)objContent["daten"       ];
        // objEntityDaten enthält Basisentität -> Liste von Datenzeilen
        // da wir hier nur eine Entität abfragen, bekommen wir auch nur eine Datenliste:
        JArray  objDaten        = (JArray )objEntityDaten[pobjTask.Entity];
        JArray  objAntworten    = (JArray )objContent["antworten"   ];
        // objDaten enthält Liste von Antwortzeilen

        // die Antworten ins Log
        log(objContent.ToString());
      }
      else  {
        // Fehler holen:
        String strErr = (String)objContent["Message"];
        tee("Fehler: "+strErr);


        return false;
      }

      return true;
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

    /// <summary>
    /// Text in Logdatei protokollieren und zugleich auf Console ausgeben
    /// </summary>
    /// <param name="pstrMsg"></param>
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
