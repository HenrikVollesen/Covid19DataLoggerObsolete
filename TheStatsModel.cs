using System;
using System.Collections.Generic;
using System.Text;

namespace Covid19DataLogger
{
    public class TheStatsModel 
    {
    }

    public class Location
    {
        public object @long { get; set; }
        public object countryOrRegion { get; set; }
        public object provinceOrState { get; set; }
        public object county { get; set; }
        public object isoCode { get; set; }
        public object lat { get; set; }
    }

    public class History
    {
        public DateTime date { get; set; }
        public int confirmed { get; set; }
        public int deaths { get; set; }
        public int recovered { get; set; }
    }

    //public class Location2
    //{
    //    public double? @long { get; set; }
    //    public string countryOrRegion { get; set; }
    //    public object provinceOrState { get; set; }
    //    public object county { get; set; }
    //    public string isoCode { get; set; }
    //    public double? lat { get; set; }
    //}

    public class Breakdown
    {
        public Location location { get; set; }
        public int totalConfirmedCases { get; set; }
        public int newlyConfirmedCases { get; set; }
        public int totalDeaths { get; set; }
        public int newDeaths { get; set; }
        public int totalRecoveredCases { get; set; }
        public int newlyRecoveredCases { get; set; }
    }

    public class Stats
    {
        public int totalConfirmedCases { get; set; }
        public int newlyConfirmedCases { get; set; }
        public int totalDeaths { get; set; }
        public int newDeaths { get; set; }
        public int totalRecoveredCases { get; set; }
        public int newlyRecoveredCases { get; set; }
        public List<History> history { get; set; }
        public List<Breakdown> breakdowns { get; set; }
    }

    public class RootObject_Stats
    {
        public Location location { get; set; }
        public DateTime updatedDateTime { get; set; }
        public Stats stats { get; set; }
    }
}
