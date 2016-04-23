using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;

namespace SubExt
{
    class Payload
    {
        public StorageFile Video { get; set; }

        public Rect Region { get; set; }
    }
}
