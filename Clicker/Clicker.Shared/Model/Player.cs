using GalaSoft.MvvmLight;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Clicker.Model
{
    public class Player : ObservableObject
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ProviderId { get; set; }
        public string UserId { get; set; }
        public int OldClicks { get; set; }

        private int _Clicks;
        public int Clicks
        {
            get { return _Clicks; }
            set
            {
                Set<int>(() => Clicks, ref _Clicks, value);
            }
        }

        private int _Rank;
        public int Rank
        {
            get { return _Rank; }
            set
            {
                Set<int>(() => Rank, ref _Rank, value);
            }
        }
    }
}
