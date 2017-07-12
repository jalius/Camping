using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CampsiteSearch
{
    class Park
    {
        public int parkId;
        public Dictionary<int, Campsite> campsites = new Dictionary<int, Campsite>();
        public Park(int _parkId)
        {
            parkId = _parkId;
        }
    }
}
