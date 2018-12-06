using System.Collections.Generic;
using System.Threading.Tasks;



namespace HIT.REST.Client {

  public class Job {
    public Credentials  Credentials { get; set; }
    public List<Task>   Tasks       { get; set; }

    public Job() {
      Credentials = new Credentials();
      Tasks       = new List<Task>();
    }
  }

  public class Credentials  {
    public string Betriebsnummer  { get; set; }
    public string MitBenutzer     { get; set; }
    public string PIN             { get; set; }
    public int    Timeout         { get; set; }
  }

  public class Task {
    /// <summary>Aktion ist der HITP "command", nicht der REST-Verb!</summary>
    public string Action    { get; set; }
    public string Entity    { get; set; }
    public string FileName  { get; set; }
  }

}
