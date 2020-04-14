using RestSharp;
using RestSharp.Serialization.Json;
using System.IO;
using System.Windows;
using Microsoft.Data.SqlClient;
using System.Data;
using System;
using System.Diagnostics;

namespace Covid19DataLogger
{
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

        //private string filepathNews = DataFolder + @"news";
        //private string jsonContentsNews;

        public ProgramDataLogger()
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

            Settings = new RestResponse()
            {
                Content = File.ReadAllText(@"Settings.json")
            };
            jd = new JsonDeserializer();
            dyn1 = jd.Deserialize<dynamic>(Settings);
            dyn2 = dyn1["DataFolder"];
            DataFolder = dyn2;
            if (!Directory.Exists(DataFolder))
            {
                Console.WriteLine("Path: " + DataFolder + " does not exist!");
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

        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                ProgramDataLogger theLogger = new ProgramDataLogger();

                Console.WriteLine("Covid19DataLogger (c) 2020\n");
                string arg0 = args[0].ToLower().Trim();

                bool Get = true;

                if (args.Length > 1)
                {
                    string arg1 = args[1].ToLower().Trim();
                    if (arg1 == "-storeonly")
                    {
                        Get = false;
                    }
                }

                if (arg0 == "global")
                {
                    Console.WriteLine("Logtype: " + arg0);
                    if (Get)
                        theLogger.Get_GlobalStats();
                    theLogger.Save_GlobalStats();
                }
                else if (arg0 == "country")
                {
                    Console.WriteLine("Logtype: " + arg0);
                    if (Get)
                        theLogger.Get_CountryStats(); 
                    theLogger.Save_CountryStats();
                }
                else if (arg0 == "state")
                {
                    Console.WriteLine("Logtype: " + arg0);
                    if (Get)
                        theLogger.Get_StateStats();
                    theLogger.Save_StateStats();
                }
                else if (arg0 == "country_state")
                {
                    Console.WriteLine("Logtype: " + arg0);
                    if (Get)
                        theLogger.Get_CountryStats();
                    theLogger.Save_CountryStats();
                    if (Get)
                        theLogger.Get_StateStats();
                    theLogger.Save_StateStats();
                }
                else
                    Console.WriteLine("Unknown command: " + arg0);
            }
            else
            {
                Console.WriteLine("No task defined.\n");
                Console.WriteLine("Usage: Covid19DataLogger ( global | country | state | country_state ) [ -storeonly ] ");
            }
        }

        private void Get_GlobalStats()
        {
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

                if ( (isoCode == null) || (Country == null) )
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

                /*
                using SqlCommand cmd1 = new SqlCommand("SELECT COUNT(*) FROM " + DimGeoRegionTable + " WHERE [isoCode] = N'" + isoCode + "' AND GeoRegionTypeId = 4", conn)
                {
                    CommandType = CommandType.Text
                };
                cmd1.Parameters.AddWithValue("@isoCode", isoCode);
                int count1 = (int)cmd1.ExecuteScalar();

                if (count1 > 0)
                {
                    SaveStatData(dt, isoCode, confirmed, deaths, recovered, (i == 0), conn);
                }
                */
            }
            conn.Close();
        }


        private void Get_CountryStats()
        {
            string jsonContentsStatsCountry;
            SqlConnection conn = new SqlConnection(sConnB.ConnectionString);
            conn.Open();

            string Command = "SELECT Alpha_2_code FROM GetAPICountries()";

            using (SqlCommand cmd = new SqlCommand(Command, conn))
            {
                string path = Filepath_CountryStats + Filename_Stats;
                SqlDataReader isoCodes = cmd.ExecuteReader();

                if (isoCodes.HasRows)
                {
                    while (isoCodes.Read())
                    {
                        string isoCode = isoCodes.GetString(0).Trim();
                        request = new RestRequest(isoCode + "/");
                        response_Stats = client.Execute<RootObject_Stats>(request);
                        jsonContentsStatsCountry = response_Stats.Content;
                        string jsonpath = path + isoCode + ".json";
                        Console.WriteLine("Saving file: " + jsonpath);
                        File.WriteAllText(jsonpath, jsonContentsStatsCountry);

                    }
                }
            }
            conn.Close();
        }

        private void Get_StateStats()
        {
            string jsonContentsStatsState;
            SqlConnection conn = new SqlConnection(sConnB.ConnectionString);
            conn.Open();

            string Command = "SELECT Alpha_2_code FROM GetAPIStates()";

            using (SqlCommand cmd = new SqlCommand(Command, conn))
            {
                string path = Filepath_StateStats + Filename_Stats;
                SqlDataReader isoCodes = cmd.ExecuteReader();

                if (isoCodes.HasRows)
                {
                    while (isoCodes.Read())
                    {
                        string isoCode = isoCodes.GetString(0).Trim();
                        request = new RestRequest(isoCode + "/");
                        response_Stats = client.Execute<RootObject_Stats>(request);
                        jsonContentsStatsState = response_Stats.Content;
                        string jsonpath = path + isoCode + ".json";
                        Console.WriteLine("Saving file: " + jsonpath);
                        File.WriteAllText(jsonpath, jsonContentsStatsState);

                    }
                }
            }
            conn.Close();
        }

        private void Save_CountryStats()
        {
            string[] JsonFiles = Directory.GetFiles(Filepath_CountryStats, "*.json");

            IRestResponse Country_Stats;
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
                Country_Stats = new RestResponse()
                {
                    Content = File.ReadAllText(FileName)
                };
                jd = new JsonDeserializer();
                dyn1 = jd.Deserialize<dynamic>(Country_Stats);
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

        private void Save_StateStats()
        {
            string[] JsonFiles = Directory.GetFiles(Filepath_StateStats, "*.json");

            IRestResponse Country_Stats;
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
                Country_Stats = new RestResponse()
                {
                    Content = File.ReadAllText(FileName)
                };
                jd = new JsonDeserializer();
                dyn1 = jd.Deserialize<dynamic>(Country_Stats);
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
