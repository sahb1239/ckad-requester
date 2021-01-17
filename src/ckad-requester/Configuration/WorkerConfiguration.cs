using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ckad_requester.Configuration
{
    public class WorkerConfiguration
    {
        public IEnumerable<WebsiteConfiguration> Websites { get; set; }
    }
}
