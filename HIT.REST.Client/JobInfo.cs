using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HIT.REST.Client
{
  public class JobInfo
  {
    public Credentials Credentials { get; set; }
    public List<TaskInfo> TaskInfos { get; set; }

    public JobInfo() {
      Credentials = new Credentials();
      TaskInfos = new List<TaskInfo>();
    }
  }

  public class Credentials
  {
    public string Betriebsnummer { get; set; }
    public string MitBenutzer { get; set; }
    public string PIN { get; set; }
    public int Timeout { get; set; }
  }

  public class TaskInfo
  {
    public string Action { get; set; }
    public string Entity { get; set; }
    public string FileName { get; set; }
  }
}
