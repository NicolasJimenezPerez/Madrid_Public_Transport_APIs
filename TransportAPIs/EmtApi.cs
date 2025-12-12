using System.Net;
using System.Text.Json.Nodes;

namespace TransportAPIs
{
    public partial class EmtApi
    {
        #region Public Classes

        /// <summary>
        /// Defines a bus stop
        /// </summary>
        public class BusStop
        {
            #region Public Classes

            /// <summary>
            /// Defines a bus line
            /// </summary>
            public class Line
            {
                #region Public Variables

                /// <summary>
                /// Stores the line number
                /// </summary>
                public readonly string Id;

                /// <summary>
                /// Stores the line direction in a given stop
                /// </summary>
                public readonly string Direction;

                /// <summary>
                /// Defines the minimum frequency of the line
                /// </summary>
                public readonly TimeSpan MinFreq;

                /// <summary>
                /// Defines the maximum frequency of the line
                /// </summary>
                public readonly TimeSpan MaxFreq;

                /// <summary>
                /// Stores the estimated arrival times for the line
                /// </summary>
                public TimeSpan[] ArrivalTimes
                {
                    get => _arrivalTimes;
                    internal set
                    {
                        if (value == _arrivalTimes)
                            return;

                        _arrivalTimes = value;
                    }
                }
                private TimeSpan[] _arrivalTimes;

                #endregion

                #region Constructor

                /// <summary>
                /// Parses the raw data of the api to create a bus line
                /// </summary>
                /// <param name="rawLine">The raw data provided by the api</param>
                internal Line(JsonNode rawLine)
                {
                    Id = rawLine["label"].ToString();
                    Direction = rawLine["header" + rawLine["direction"]].ToString();
                    MinFreq = new TimeSpan(0, int.Parse(rawLine["minFreq"].ToString()), 0);
                    MaxFreq = new TimeSpan(0, int.Parse(rawLine["maxFreq"].ToString()), 0);
                }

                #endregion
            }

            #endregion

            #region Public Variables

            /// <summary>
            /// Stores the id of the bus stop
            /// </summary>
            public readonly int Id;

            /// <summary>
            /// Stores the bus lines that go through the stop
            /// </summary>
            public readonly Line[] Lines;

            #endregion

            #region Private Variables

            private EmtApi _emtApi;

            #endregion

            #region Constructor

            /// <summary>
            /// Defines the bus sto and loads the appropriate bus lines
            /// </summary>
            /// <param name="emtApi">Reference to the emtApi</param>
            /// <param name="id">Id of the bus stop to be defined</param>
            internal BusStop(EmtApi emtApi, int id)
            {
                this._emtApi = emtApi;
                Id = id;

                // Load the bus lines associated with the stop 
                Lines = emtApi.GetBusStopLines(id);

                UpdateArrivalTimes();
            }

            #endregion

            #region Public Methods

            /// <summary>
            /// Update the arrival time for each bus line that goes through the stop
            /// </summary>
            /// <returns>Returns true if the info is updated.</returns>
            public bool UpdateArrivalTimes()
            {
                foreach (var line in Lines)
                {
                    TimeSpan[]? arrivalTimes = _emtApi.GetTimeTillArrivalAtStop(Id, line.Id);
                    if (arrivalTimes == null)
                    {
                        Console.WriteLine($"Error: Could not get arrival times of {line.Id} to {Id}");
                        return false;
                    }

                    line.ArrivalTimes = arrivalTimes;
                }

                return true;
            }

            #endregion
        }

        #endregion

        #region Private Variables

        /// <summary>
        /// Http Client
        /// </summary>
        private readonly HttpClient client;

        /// <summary>
        /// Api Access Token
        /// </summary>
        private string accessToken;

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor for the TransportAPIs class
        /// </summary>
        public EmtApi()
        {
            client = new HttpClient()
            {
                BaseAddress = new Uri("https://openapi.emtmadrid.es/"),
            };

            Login();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Checks if the server is up
        /// </summary>
        /// <returns>Returns true if the server is up</returns>
        private bool IsServerUp()
        {
            // Check Server Status
            var response = client.GetAsync("v1/hello/").Result;
            Console.WriteLine();

            return response.Content.ReadAsStringAsync().Result.Contains("Ok");
        }

        /// <summary>
        /// Logs in with the server and retrieves the access token
        /// </summary>
        /// <returns>Returns true if the login is successful</returns>
        public bool Login()
        {
            // Send get to server
            if (!sendGet("v3/mobilitylabs/user/login/", JsonNode.Parse(loginJson), out JsonNode response, out HttpStatusCode respStatusCode))
            {
                Console.WriteLine("Error: Communication to server failed during Login. Code: " + respStatusCode);
                return false;
            }

            // Check the code for a successful response and save the access token if so
            string code = response!["code"].ToString();
            if (code != "00" && code != "01")
            {
                Console.WriteLine("Error: Login failed. Code = " + code);
                return false;
            }

            accessToken = response!["data"][0]["accessToken"].ToString();

            return true;
        }

        /// <summary>
        /// Ask the server if the access token is active
        /// </summary>
        /// <returns>Returns true if the access token is active</returns>
        public bool IsTockenActive()
        {
            // Send query to server
            if (!sendGetWithAccessToken("v1/mobilitylabs/user/whoami/", out JsonNode response,
                    out HttpStatusCode respStatusCode))
            {
                Console.WriteLine("Error: Communication to server failed during IsTokenActive. Code: " +
                                  respStatusCode);
                return false;
            }

            // Check the code for a successful response and save the access token if so
            string code = response!["code"].ToString();
            if (code != "02")
            {
                Console.WriteLine("Error: Token inactive. Code = " + code);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns the selected bus stop
        /// </summary>
        /// <param name="stopId">The id of the bus stop</param>
        /// <returns>The bus stop associated with the given id</returns>
        public BusStop GetBusStop(int stopId)
        {
            return new BusStop(this, stopId);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Send get message to server
        /// </summary>
        /// <param name="uri">Server sub-domain</param>
        /// <param name="jsonHeader">Server query in json format</param>
        /// <param name="jsonResponse">Server response in json format</param>
        /// <param name="respStatusCode">Response status code</param>
        /// <returns>Returns true if the query is successful</returns>
        private bool sendGet(string uri, JsonNode jsonHeader, out JsonNode jsonResponse,
            out HttpStatusCode respStatusCode)
        {
            jsonResponse = null;

            // Create the request for the server login
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);

            // Add the json fields to the request header
            foreach (var field in jsonHeader.AsObject().AsEnumerable())
                request.Headers.Add(field.Key, field.Value.ToString());

            // Send the request
            HttpResponseMessage resp = client.SendAsync(request).Result;

            // Check if the response is a 2XX code
            if (resp.IsSuccessStatusCode)
                jsonResponse = JsonNode.Parse(resp.Content.ReadAsStringAsync().Result);

            respStatusCode = resp.StatusCode;
            return resp.IsSuccessStatusCode;
        }

        /// <summary>
        /// Send access token to server
        /// </summary>
        /// <param name="uri">Server sub-domain</param>
        /// <param name="jsonNode">Server response in json format</param>
        /// <param name="respStatusCode">Response status code</param>
        /// <returns>Returns true if the query is successful</returns>
        private bool sendGetWithAccessToken(string uri, out JsonNode jsonNode, out HttpStatusCode respStatusCode)
        {
            return sendGet(uri, JsonNode.Parse($"{{\"accessToken\": \"{accessToken}\"}}"), out jsonNode,
                out respStatusCode);
        }

        /// <summary>
        /// Sends a post message to the server
        /// </summary>
        /// <param name="uri">Server sub-domain</param>
        /// <param name="jsonBody">Json in the content of the post</param>
        /// <param name="jsonResponse">The server's response to the post</param>
        /// <param name="respStatusCode">The status code associated with the response</param>
        /// <returns>True if the post has completed without problem</returns>
        private bool sendPostWithAccessToken(string uri, JsonNode jsonBody, out JsonNode jsonResponse,
            out HttpStatusCode respStatusCode)
        {
            jsonResponse = null;

            // Create the request for the server login
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, uri);

            // Add headers to the post
            request.Headers.Add("accessToken", accessToken);

            // Add the body to the post
            request.Content = new StringContent(jsonBody.ToString());

            // Send the request
            HttpResponseMessage resp = client.SendAsync(request).Result;

            // Check if the response is a 2XX code
            if (resp.IsSuccessStatusCode)
                jsonResponse = JsonNode.Parse(resp.Content.ReadAsStringAsync().Result);

            respStatusCode = resp.StatusCode;
            return resp.IsSuccessStatusCode;
        }

        /// <summary>
        /// Gets the lines at a specified stop
        /// </summary>
        /// <param name="stopId">The id of the stop</param>
        /// <returns>The array of lines that go through the stop</returns>
        private BusStop.Line[]? GetBusStopLines(int stopId)
        {
            // Send get to server
            if (!sendGetWithAccessToken($"v1/transport/busemtmad/stops/{stopId}/detail/", out JsonNode response,
                    out HttpStatusCode respStatusCode))
            {
                Console.WriteLine("Error: Communication to server failed during GetBusStop. Code: " + respStatusCode);
                return null;
            }

            // Check the code for a successful response and save the access token if so
            string code = response!["code"].ToString();
            if (code != "00")
            {
                Console.WriteLine($"Error: Failed to retrieve info from bus stop #{stopId}. Code = " + code);
                return null;
            }

            // Get the buses in the stop
            JsonArray rawLines = response!["data"][0][0][0]["dataLine"].AsArray();

            // Generate a bus line array from the bus lines in the stop
            BusStop.Line[] lines = new BusStop.Line[rawLines.Count];
            for (int i = 0; i < rawLines.Count; i++)
                lines[i] = new BusStop.Line(rawLines[i]);

            return lines;
        }

        /// <summary>
        /// Gets the bus line estimated arrival times to the stop
        /// </summary>
        /// <param name="stopId">The bus stop id</param>
        /// <param name="lineId">The bus line id</param>
        /// <returns>An array of the predicted arrival times</returns>
        private TimeSpan[]? GetTimeTillArrivalAtStop(int stopId, string lineId)
        {
            // Create the Json to send to the server
            JsonNode content = JsonNode.Parse("{\"cultureInfo\": \"EN\"," +
                                              "\"Text_StopRequired_YN\": \"Y\"," + 
                                              "\"Text_EstimationsRequired_YN\": \"Y\"," + 
                                              "\"Text_IncidencesRequired_YN\": \"Y\"," + 
                                              $"\"DateTime_Referenced_Incidencies_YYYYMMDD\": \"{DateTime.Today.Year}{DateTime.Today.Month}{DateTime.Today.Day}\"}}");

            // Send post to server
            if (!sendPostWithAccessToken($"v2/transport/busemtmad/stops/{stopId}/arrives/{lineId}/", content,
                    out JsonNode response, out HttpStatusCode respStatusCode))
            {
                Console.WriteLine("Error: Communication to server failed during GetTimeTillArrivalAtStop. Code: " +
                                  respStatusCode);
                return null;
            }

            // Check the code for a successful response and save the access token if so
            string code = response!["code"].ToString();
            if (code != "00")
            {
                Console.WriteLine(
                    $"Error: Failed to retrieve info from bus line #{lineId} arrival at #{stopId}. Code = " + code);
                return null;
            }

            // Get the arrival time for the bus
            List<TimeSpan> arrivals = new List<TimeSpan>();
            foreach (var arrival in response!["data"][0]["Arrive"].AsArray())
                arrivals.Add(new TimeSpan(0, 0, int.Parse(arrival["estimateArrive"].ToString())));

            return arrivals.ToArray();
        }

        #endregion
    }
}