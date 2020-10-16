using RestSharp;
using RestSharp.Serialization.Json;
using System.IO;
using System.Windows;
using Microsoft.Data.SqlClient;
using System.Data;
using System;
using System.Diagnostics;
using System.Threading;

namespace Covid19DataLogger
{
    enum LogType { GLOBAL, COUNTRY, STATE, COUNTRY_STATE, UNKNOWN };

    class ProgramDataLogger
    {
        //The API key will be read from the local Settings file. 
        //To use this program, you must get your own API key from https://developer.smartable.ai/
        private string APIKey = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";

        private RestClient client = new RestClient("https://api.smartable.ai/coronavirus/stats/");
        //https://api.smartable.ai/coronavirus/stats/{location}

        private RestRequest request = null;
        private IRestResponse<RootObject_Stats> response_Stats = null;

        private SqlConnectionStringBuilder sConnB;

        private string DataFolder = @"D:\Data\coronavirus\"; // Base folder for storage of coronavirus data

        private string Filepath_GlobalStats;
        private string Filepath_CountryStats;
        private string Filepath_StateStats;
        private string Filename_Stats = "LatestStats_";

        private string SettingsPath = @"Settings.json";

        int Delay = 10000;

        //private string filepathNews = DataFolder + @"news";
        //private string jsonContentsNews;
        //"D:\Data\coronavirus\Covid19DataLogger for Hildur\Settings.json"
        static void Main(string[] args)
        {
            LogType Log_Type = LogType.UNKNOWN;

            if (args.Length > 0)
            {
                Console.WriteLine("Covid19DataLogger (c) 2020\n");
                string arg0 = args[0].ToLower().Trim();

                if (arg0 == "global")
                    Log_Type = LogType.GLOBAL;
                else if (arg0 == "country")
                    Log_Type = LogType.COUNTRY;
                else if (arg0 == "state")
                    Log_Type = LogType.STATE;
                else if (arg0 == "country_state")
                    Log_Type = LogType.COUNTRY_STATE;

                if (Log_Type != LogType.UNKNOWN)
                {
                    int Action = 0;
                    string SettingsPath = @"Settings.json";

                    if (args.Length > 1)
                    {
                        string arg1 = args[1].ToLower().Trim();
                        if (arg1 == "-storeonly")
                            Action = 1;
                        else if (arg1 == "-noreplace")
                            Action = 2;

                        if (arg1 == "-settingsfile")
                        {
                            if (args.Length > 2)
                            {
                                SettingsPath = args[2];
                            }
                        }
                        else if (args.Length > 2)
                        {
                            string arg2 = args[2].ToLower().Trim();
                            if (arg2 == "-settingsfile")
                            {
                                if (args.Length > 3)
                                {
                                    SettingsPath = args[3];
                                }
                            }
                        }
                    }

                    if (!File.Exists(SettingsPath))
                    {
                        Console.WriteLine("Settings file: " + SettingsPath + " not found or invalid! Press any key to stop...");
                        Console.ReadKey();
                        Environment.Exit(0);
                    }

                    ProgramDataLogger theLogger = new ProgramDataLogger(SettingsPath);
                    Console.WriteLine("Logtype: " + arg0);
                    theLogger.Get_Stats(Log_Type, Action);
                    theLogger.Save_Stats(Log_Type);
                }
            }
            else
            {
                Console.WriteLine("Invalid command line arguments.\n");
                Console.WriteLine("Usage: Covid19DataLogger ( global | country | state | country_state ) [ -storeonly | -noreplace ] [ -settingsfile <path> ]");
            }
        }

        public ProgramDataLogger(string SettingsPath)
        {
            /*
             * Read settings from Settings.json
            */
            IRestResponse Settings;
            JsonDeserializer jd;
            dynamic dyn1;
            dynamic dyn2;
            string DataSourceFile;
            string InitialCatalogFile;
            string UserIDFile;
            string PasswordFile;

            this.SettingsPath = SettingsPath;

            Settings = new RestResponse()
            {
                Content = File.ReadAllText(SettingsPath)
            };

            jd = new JsonDeserializer();
            dyn1 = jd.Deserialize<dynamic>(Settings);
            dyn2 = dyn1["DataFolder"];
            DataFolder = dyn2;
            if (!Directory.Exists(DataFolder))
            {
                Console.WriteLine("Path: " + DataFolder + " does not exist! Press any key to stop...");
                Console.ReadKey();
                Environment.Exit(0);
            }
            Filepath_CountryStats = DataFolder + @"stats\CountryStats\";
            if (!Directory.Exists(Filepath_CountryStats))
            {
                Directory.CreateDirectory(Filepath_CountryStats);
            }
            Filepath_StateStats = DataFolder + @"stats\StateStats\";
            if (!Directory.Exists(Filepath_StateStats))
            {
                Directory.CreateDirectory(Filepath_StateStats);
            }
            Filepath_GlobalStats = DataFolder + @"stats\LatestStats_Global.json";

            dyn2 = dyn1["APIKey"];
            APIKey = dyn2;
            dyn2 = dyn1["DataSource"];
            DataSourceFile = dyn2;
            dyn2 = dyn1["InitialCatalog"];
            InitialCatalogFile = dyn2;
            dyn2 = dyn1["UserID"];
            UserIDFile = dyn2;
            dyn2 = dyn1["Password"];
            PasswordFile = dyn2;


            client.AddDefaultHeader("Subscription-Key", APIKey);

            sConnB = new SqlConnectionStringBuilder()
            {
                DataSource = DataSourceFile,
                InitialCatalog = InitialCatalogFile,
                UserID = UserIDFile,
                Password = PasswordFile
            };
        }

        private void Get_Stats(LogType ltype, int action)
        {
            switch (ltype)
            {
                case LogType.GLOBAL:
                    Get_GlobalStats(action);
                    break;
                case LogType.COUNTRY:
                case LogType.STATE:
                case LogType.COUNTRY_STATE:
                    Get_CountryOrStateStats(ltype, action);
                    break;
                default:
                    break;
            }
        }

        private void Save_Stats(LogType ltype)
        {
            switch (ltype)
            {
                case LogType.GLOBAL:
                    Save_GlobalStats();
                    break;
                case LogType.COUNTRY:
                case LogType.STATE:
                case LogType.COUNTRY_STATE:
                    Save_CountryOrStateStats(ltype);
                    break;
                default:
                    break;
            }
        }
        private void Get_GlobalStats(int action)
        {
            if (action == 1)
                return;
            else if (action == 2)
            {
                if (File.Exists(Filepath_GlobalStats))
                    return;
            }

            string jsonContentsStatsGlobal;

            request = new RestRequest("global/");
            response_Stats = client.Execute<RootObject_Stats>(request);
            jsonContentsStatsGlobal = response_Stats.Content;
            Console.WriteLine("Saving file: " + Filepath_GlobalStats);
            File.WriteAllText(Filepath_GlobalStats, jsonContentsStatsGlobal);
        }

        private void Save_GlobalStats()
        {
            IRestResponse Global_Stats;
            JsonDeserializer jd;
            dynamic dyn1;
            dynamic dyn2;
            dynamic dyn3;
            dynamic dyn4;
            dynamic dyn5;
            JsonArray al;

            bool SaveCountry = false;

            Console.WriteLine("Storing data from file: " + Filepath_GlobalStats);
            Global_Stats = new RestResponse()
            {
                Content = File.ReadAllText(Filepath_GlobalStats)
            };
            jd = new JsonDeserializer();
            dyn1 = jd.Deserialize<dynamic>(Global_Stats);
            dyn2 = dyn1["stats"];
            dyn3 = dyn2["history"];
            al = (JsonArray)dyn3;
            if (al.Count == 0)
                return;
            dyn3 = al[^1];
            string dt = dyn3["date"];

            SqlConnection conn = new SqlConnection(sConnB.ConnectionString);
            conn.Open();

            dyn3 = dyn2["breakdowns"];
            al = (JsonArray)dyn3;
            for (int i = 0; i < al.Count; i++)
            {
                dyn4 = al[i];
                dyn5 = dyn4["location"];
                string isoCode = dyn5["isoCode"];
                string Country = dyn5["countryOrRegion"];

                if ((isoCode == null) || (Country == null))
                    continue;

                long confirmed = dyn4["totalConfirmedCases"];
                long deaths = dyn4["totalDeaths"];
                long recovered = dyn4["totalRecoveredCases"];

                if (SaveCountry)
                {
                    using (SqlCommand cmd2 = new SqlCommand("UPDATE DimLocation SET IsCovidCountry = 1 WHERE Alpha_2_code = N'" + isoCode + "'", conn))
                    {
                        cmd2.CommandType = CommandType.Text;
                        int rowsAffected = cmd2.ExecuteNonQuery();
                    }
                }
                else
                    SaveStatData(dt, isoCode, confirmed, deaths, recovered, (i == 0), conn);

            }
            conn.Close();
        }

        private void Get_CountryOrStateStats(LogType ltype, int action)
        {
            bool CheckFile = false;
            if (action == 1)
                return;
            else if (action == 2)
            {
                CheckFile = true;
            }

            if (ltype == LogType.COUNTRY)
            {
                Get_AreaStats(LogType.COUNTRY, CheckFile);
            }
            else if (ltype == LogType.STATE)
            {
                Get_AreaStats(LogType.STATE, CheckFile);
            }
            else if (ltype == LogType.COUNTRY_STATE)
            {
                Get_AreaStats(LogType.COUNTRY, CheckFile);
                Get_AreaStats(LogType.STATE, CheckFile);
            }
        }

        private void Get_AreaStats(LogType ltype, bool CheckFile)
        {
            string Command;
            string Path;

            if (ltype == LogType.COUNTRY)
            {
                Command = "SELECT Alpha_2_code FROM GetAPICountries()";
                Path = Filepath_CountryStats + Filename_Stats;
            }
            else if (ltype == LogType.STATE)
            {
                Command = "SELECT Alpha_2_code FROM GetAPIStates()";
                Path = Filepath_StateStats + Filename_Stats;
            }
            else
                return;

            SqlConnection conn = new SqlConnection(sConnB.ConnectionString);
            conn.Open();

            using (SqlCommand cmd = new SqlCommand(Command, conn))
            {
                SqlDataReader isoCodes = cmd.ExecuteReader();

                if (isoCodes.HasRows)
                {
                    while (isoCodes.Read())
                    {
                        string isoCode = isoCodes.GetString(0).Trim();
                        string jsonpath = Path + isoCode + ".json";
                        if (CheckFile)
                        {
                            if (File.Exists(jsonpath))
                                continue;
                        }
                        string jsonContents;
                        request = new RestRequest(isoCode + "/");
                        response_Stats = client.Execute<RootObject_Stats>(request);
                        jsonContents = response_Stats.Content;
                        Console.WriteLine("Saving file: " + jsonpath);
                        File.WriteAllText(jsonpath, jsonContents);
                        Thread.Sleep(Delay);
                    }
                }
            }
            conn.Close();
        }
//        var client = new RestClient("https://rapidapi.p.rapidapi.com/stats/v1/US/");
//        var request = new RestRequest(Method.GET);
//        request.AddHeader("x-rapidapi-host", "coronavirus-smartable.p.rapidapi.com");
//request.AddHeader("x-rapidapi-key", "22a317b985msh249bce487c7aa57p18909fjsn918b465d2071");
//IRestResponse response = client.Execute(request);
        //private void Get_CountryStats(int action)
        //{
        //    bool CheckFile = false;
        //    if (action == 1)
        //        return;
        //    else if (action == 2)
        //    {
        //        CheckFile = true;
        //    }

        //    string jsonContentsStatsCountry;
        //    SqlConnection conn = new SqlConnection(sConnB.ConnectionString);
        //    conn.Open();

        //    string Command = "SELECT Alpha_2_code FROM GetAPICountries()";

        //    using (SqlCommand cmd = new SqlCommand(Command, conn))
        //    {
        //        string path = Filepath_CountryStats + Filename_Stats;
        //        SqlDataReader isoCodes = cmd.ExecuteReader();

        //        if (isoCodes.HasRows)
        //        {
        //            while (isoCodes.Read())
        //            {
        //                string isoCode = isoCodes.GetString(0).Trim();
        //                string jsonpath = path + isoCode + ".json";
        //                if (CheckFile)
        //                {
        //                    if (File.Exists(Filepath_GlobalStats))
        //                        continue;
        //                }
        //                request = new RestRequest(isoCode + "/");
        //                response_Stats = client.Execute<RootObject_Stats>(request);
        //                jsonContentsStatsCountry = response_Stats.Content;
        //                Console.WriteLine("Saving file: " + jsonpath);
        //                File.WriteAllText(jsonpath, jsonContentsStatsCountry);
        //                Thread.Sleep(Delay);
        //            }
        //        }
        //    }
        //    conn.Close();
        //}

        //private void Get_StateStats(int action)
        //{
        //    bool CheckFile = false;
        //    if (action == 1)
        //        return;
        //    else if (action == 2)
        //    {
        //        CheckFile = true;
        //    }

        //    string jsonContentsStatsState;
        //    SqlConnection conn = new SqlConnection(sConnB.ConnectionString);
        //    conn.Open();

        //    string Command = "SELECT Alpha_2_code FROM GetAPIStates()";

        //    using (SqlCommand cmd = new SqlCommand(Command, conn))
        //    {
        //        string path = Filepath_StateStats + Filename_Stats;
        //        SqlDataReader isoCodes = cmd.ExecuteReader();

        //        if (isoCodes.HasRows)
        //        {
        //            while (isoCodes.Read())
        //            {
        //                string isoCode = isoCodes.GetString(0).Trim();
        //                string jsonpath = path + isoCode + ".json";
        //                if (CheckFile)
        //                {
        //                    if (File.Exists(Filepath_GlobalStats))
        //                        return;
        //                }
        //                request = new RestRequest(isoCode + "/");
        //                response_Stats = client.Execute<RootObject_Stats>(request);
        //                jsonContentsStatsState = response_Stats.Content;
        //                Console.WriteLine("Saving file: " + jsonpath);
        //                File.WriteAllText(jsonpath, jsonContentsStatsState);
        //                Thread.Sleep(Delay);
        //            }
        //        }
        //    }
        //    conn.Close();
        //}

        private void Save_CountryOrStateStats(LogType ltype)
        {
            if (ltype == LogType.COUNTRY) 
            {
                Save_AreaStats(Filepath_CountryStats);
            }
            else if (ltype == LogType.STATE)
            {
                Save_AreaStats(Filepath_StateStats);
            }
            else if (ltype == LogType.COUNTRY_STATE)
            {
                Save_AreaStats(Filepath_CountryStats);
                Save_AreaStats(Filepath_StateStats);
            }
        }

        private void Save_AreaStats(string Path)
        {
            string[] JsonFiles = Directory.GetFiles(Path, "*.json");

            IRestResponse Area_Stats;
            JsonDeserializer jd;
            dynamic dyn1;
            dynamic dyn2;
            dynamic dyn3;
            dynamic dyn4;
            JsonArray al;

            SqlConnection conn = new SqlConnection(sConnB.ConnectionString);
            conn.Open();

            foreach (string FileName in JsonFiles)
            {
                bool SaveDate = true;
                Console.WriteLine("Storing data from file: " + FileName);
                Area_Stats = new RestResponse()
                {
                    Content = File.ReadAllText(FileName)
                };
                jd = new JsonDeserializer();
                dyn1 = jd.Deserialize<dynamic>(Area_Stats);
                dyn2 = dyn1["location"];
                string isoCode = dyn2["isoCode"];

                dyn2 = dyn1["stats"];
                dyn3 = dyn2["history"];
                al = (JsonArray)dyn3;
                for (int i = 0; i < al.Count; i++)
                {
                    dyn4 = al[i];
                    string dt = dyn4["date"];
                    long confirmed = dyn4["confirmed"];
                    long deaths = dyn4["deaths"];
                    long recovered = dyn4["recovered"];
                    SaveStatData(dt, isoCode, confirmed, deaths, recovered, SaveDate, conn);
                }
                SaveDate = false;
            }
            conn.Close();
        }


        private void SaveStatData(string dt, string isoCode, long confirmed, long deaths, long recovered, bool SaveDate, SqlConnection conn)
        {
            if (SaveDate)
            {
                using (SqlCommand cmd2 = new SqlCommand("Save_Date", conn))
                {
                    cmd2.CommandType = CommandType.StoredProcedure;
                    cmd2.Parameters.AddWithValue("@date", dt);
                    int rowsAffected = cmd2.ExecuteNonQuery();
                }
            }

            using (SqlCommand cmd2 = new SqlCommand("Save_DayStat", conn))
            {
                cmd2.CommandType = CommandType.StoredProcedure;
                cmd2.Parameters.AddWithValue("@Alpha_2_code", isoCode);
                cmd2.Parameters.AddWithValue("@date", dt);
                cmd2.Parameters.AddWithValue("@confirmed", confirmed);
                cmd2.Parameters.AddWithValue("@deaths", deaths);
                cmd2.Parameters.AddWithValue("@recovered", recovered);
                int rowsAffected = cmd2.ExecuteNonQuery();
            }
        }
    }
}
