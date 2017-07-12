using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CampsiteSearch
{
    class Campsite
    {
        public int siteId;
        public Dictionary<DateTime, bool> availibility = new Dictionary<DateTime, bool>();
        public Campsite(int _siteId)
        {
            siteId = _siteId;
        }
    }
}
