using Microsoft.WindowsAzure.Mobile.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Clicker.MobileService.DataObjects
{
    public class Player : EntityData
    {
        public string Name { get; set; }
        public string ProviderId { get; set; }
        public string UserId { get; set; }
        public int Clicks { get; set; }
        public int OldClicks { get; set; }
        public int Rank { get; set; }
    }
}