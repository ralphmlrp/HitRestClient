using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HIT.REST.Client
{
    public class TaskInfo
    {
        public TaskInfo()
        {
            Credentials = new Credentials();
            JobInfos = new List<JobInfo>();
        }

        public Credentials Credentials { get; set; }

        public List<JobInfo> JobInfos { get; set; }
    }

    public class Credentials
    {
        public string Betriebsnummer { get; set; }
        public string MitBenutzer { get; set; }
        public string PIN { get; set; }
    }

    public class JobInfo
    {
        public string Action { get; set; }
        public string Entity { get; set; }
        public string FileName { get; set; }

    }
}
