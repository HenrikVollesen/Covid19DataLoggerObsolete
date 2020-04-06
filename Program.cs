using RestSharp;
using RestSharp.Serialization.Json;
using System.IO;
using System.Windows;
using Microsoft.Data.SqlClient;
using System.Data;
using System;

namespace Covid19DataLogger
{
    class ProgramDataLogger
    {
        private const int NoOfTopCountries = 60;

        private const string APIKey = "0a36b4e6100a4e8a9b05b432324e65ab";
        private RestClient client = new RestClient("https://api.smartable.ai/coronavirus/stats/");
        //https://api.smartable.ai/coronavirus/stats/{location}

        private RestRequest request = null;
        private IRestResponse<RootObject_Stats> response_Stats = null;

        private SqlConnectionStringBuilder sConnB;

        private string filepath_GlobalStats = "..\\..\\..\\coronavirus\\stats\\LatestStatsGlobal.json";
        private string filepath_CountryStats = "..\\..\\..\\coronavirus\\stats\\CountryStats\\";
        private string filepath_StateStats = "..\\..\\..\\coronavirus\\stats\\StateStats\\";
        private string filename_Stats = "LatestStats_";

        //private string filepathCountriesCSV = "..\\..\\..\\coronavirus\\stats\\Countries.CSV";
        //private string filepathCountryCodesCSV = "..\\..\\..\\coronavirus\\stats\\CountryCodes.csv";
        //private string filepathNews = "..\\..\\..\\coronavirus\\news";
        //private string jsonContentsNews;

        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                Console.WriteLine("Covid19DataLogger (c) 2020\n");
                string arg0 = args[0].ToLower().Trim();
                ProgramDataLogger theLogger = new ProgramDataLogger();

                if (arg0 == "global")
                {
                    Console.WriteLine("Logtype: " + arg0);
                    theLogger.Get_GlobalStats();
                    theLogger.Save_GlobalStats();
                }
                else if (arg0 == "country")
                {
                    Console.WriteLine("Logtype: " + arg0);
                    theLogger.Get_CountryStats(); 
                    theLogger.Save_CountryStats();
                }
                else if (arg0 == "state")
                {
                    Console.WriteLine("Logtype: " + arg0);
                    theLogger.Get_StateStats();
                    theLogger.Save_StateStats();
                }
                else if (arg0 == "country_state")
                {
                    Console.WriteLine("Logtype: " + arg0);
                    theLogger.Get_CountryStats();
                    theLogger.Save_CountryStats();
                    theLogger.Get_StateStats();
                    theLogger.Save_StateStats();
                }
                else
                    Console.WriteLine("Unknown command: " + arg0);
            }
            else
            {
                Console.WriteLine("No task defined.\n");
                Console.WriteLine("Usage: Covid19DataLogger [global | country | state | country_state]");
            }
        }

        public ProgramDataLogger()
        {
            client.AddDefaultHeader("Subscription-Key", APIKey);

            sConnB = new SqlConnectionStringBuilder()
            {
                DataSource = "hildur.ucn.dk",
                InitialCatalog = "cs_hnv",
                UserID = "psu_Covid19_Reader",
                Password = "Corona_2020"
            };
        }

        private void Get_GlobalStats()
        {
            string jsonContentsStatsGlobal;

            request = new RestRequest("global/");
            response_Stats = client.Execute<RootObject_Stats>(request);
            jsonContentsStatsGlobal = response_Stats.Content;
            Console.WriteLine("Saving file: " + filepath_GlobalStats);
            File.WriteAllText(filepath_GlobalStats, jsonContentsStatsGlobal);
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

            Console.WriteLine("Storing data from file: " + filepath_GlobalStats);
            Global_Stats = new RestResponse()
            {
                Content = File.ReadAllText(filepath_GlobalStats)
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

                if (isoCode == null)
                    continue;

                long confirmed = dyn4["totalConfirmedCases"];
                long deaths = dyn4["totalDeaths"];
                long recovered = dyn4["totalRecoveredCases"];

                using SqlCommand cmd1 = new SqlCommand("SELECT COUNT(*) FROM Covid19_DayStat WHERE @isoCode IN (SELECT [isoCode] FROM Covid19_Country)", conn);
                cmd1.CommandType = CommandType.Text;
                cmd1.Parameters.AddWithValue("@isoCode", isoCode);
                int count1 = (int)cmd1.ExecuteScalar();

                if (count1 > 0)
                {
                    using (SqlCommand cmd2 = new SqlCommand("Save_DayStat", conn))
                    {
                        cmd2.CommandType = CommandType.StoredProcedure;
                        cmd2.Parameters.AddWithValue("@isoCode", isoCode);
                        cmd2.Parameters.AddWithValue("@date", dt);
                        cmd2.Parameters.AddWithValue("@confirmed", confirmed);
                        cmd2.Parameters.AddWithValue("@deaths", deaths);
                        cmd2.Parameters.AddWithValue("@recovered", recovered);
                        int rowsAffected = cmd2.ExecuteNonQuery();
                    }
                }
            }
            conn.Close();
        }


        private void Get_CountryStats()
        {
            string jsonContentsStatsCountry;
            SqlConnection conn = new SqlConnection(sConnB.ConnectionString);
            conn.Open();

            string Command = "SELECT isoCode FROM GetTopCountries(" + NoOfTopCountries + ")";

            using (SqlCommand cmd = new SqlCommand(Command, conn))
            {
                string path = filepath_CountryStats + filename_Stats;
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

            string Command = "SELECT isoCode FROM GetStates()";

            using (SqlCommand cmd = new SqlCommand(Command, conn))
            {
                string path = filepath_StateStats + filename_Stats;
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
            string[] JsonFiles = Directory.GetFiles(filepath_CountryStats, "*.json");

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

                    using (SqlCommand cmd2 = new SqlCommand("Save_DayStat", conn))
                    {
                        cmd2.CommandType = CommandType.StoredProcedure;
                        cmd2.Parameters.AddWithValue("@isoCode", isoCode);
                        cmd2.Parameters.AddWithValue("@date", dt);
                        cmd2.Parameters.AddWithValue("@confirmed", confirmed);
                        cmd2.Parameters.AddWithValue("@deaths", deaths);
                        cmd2.Parameters.AddWithValue("@recovered", recovered);
                        int rowsAffected = cmd2.ExecuteNonQuery();
                    }
                }
            }
            conn.Close();
        }

        private void Save_StateStats()
        {
            string[] JsonFiles = Directory.GetFiles(filepath_StateStats, "*.json");

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

                    using (SqlCommand cmd2 = new SqlCommand("Save_DayStat", conn))
                    {
                        cmd2.CommandType = CommandType.StoredProcedure;
                        cmd2.Parameters.AddWithValue("@isoCode", isoCode);
                        cmd2.Parameters.AddWithValue("@date", dt);
                        cmd2.Parameters.AddWithValue("@confirmed", confirmed);
                        cmd2.Parameters.AddWithValue("@deaths", deaths);
                        cmd2.Parameters.AddWithValue("@recovered", recovered);
                        int rowsAffected = cmd2.ExecuteNonQuery();
                    }
                }
            }
            conn.Close();
        }
    }
}
