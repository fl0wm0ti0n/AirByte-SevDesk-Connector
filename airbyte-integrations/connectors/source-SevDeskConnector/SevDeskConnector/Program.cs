using System;
using System.Collections;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Airbyte.Cdk;
using Airbyte.Cdk.Sources;
using Airbyte.Cdk.Sources.Streams.Http.Auth;
using Airbyte.Cdk.Sources.Utils;
using Flurl;
using Flurl.Http;
using Json.More;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stream = Airbyte.Cdk.Sources.Streams.Stream;

namespace SevDeskConnector
{
    public class Program : AbstractSource
    {
        public static async Task Main(string[] args) => await AirbyteEntrypoint.Main(args);
        static AirbyteLogger Logger { get; } = new();

        Dictionary<string, string> currentState = new Dictionary<string, string>();
        //Dictionary<string, string> nextPageToken = new Dictionary<string, string>();
        Dictionary<string, int> pageCount = new Dictionary<string, int>();


        /// <summary>
        /// CheckConnection would be run at begin of a connection and changing connectors config in airbyte
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="config">json wird von Airbyte per command übergeben</param>
        /// <param name="exc"></param>
        /// <returns></returns>
        public override bool CheckConnection(AirbyteLogger logger, JsonElement config, out Exception exc)
        {
            string? checkEndpoint = config.GetProperty("connection_check_api").GetString();
            string? apiToken = config.GetProperty("api_token").GetString();
           
            string? urlBase = config.GetProperty("base_url").GetString();
            urlBase = urlBase.SetQueryParam("token", apiToken);

            exc = null;
            try
            {
                return urlBase.AppendPathSegment(checkEndpoint)
                    .GetAsync()
                    .Result.ResponseMessage.IsSuccessStatusCode;
                    //.GetJsonAsync().Result.success;
            }
            catch (Exception e)
            {
                exc = e;
            }
            return false;
        }

        /// <summary>
        /// SevDesk's API needs a special treatment. Cuts parts of the jsonstring away and returns json.net JObjects.
        /// </summary>
        /// <param name="response"></param>
        /// <param name="jsonObjects"></param>
        /// <returns></returns>
        private bool GetJObjects(IFlurlResponse response, out List<JObject> jsonObjects)
        {
            jsonObjects = new List<JObject>();
            try
            {
                var jsonStringTask = response.ResponseMessage.Content.ReadAsStringAsync();
                var jsonString = jsonStringTask.Result.Remove(jsonStringTask.Result.Length - 1);
                jsonString = jsonString.Remove(0, 11);
                jsonObjects = JsonConvert.DeserializeObject<List<JObject>>(jsonString);
                return true;
            }
            catch (Exception e)
            {
                Logger.Info($"Json parsing error:\n {e}");
                return false;
            }
        }

        /// <summary>
        /// SevDesk's API needs a special treatment. Cuts parts of the jsonstring away and returns Text.Json JsonObjects..
        /// </summary>
        /// <param name="response"></param>
        /// <param name="jsonObjects"></param>
        /// <returns></returns>
        private bool GetJsonObjects(IFlurlResponse response, out List<JsonObject> jsonObjects)
        {
            jsonObjects = new List<JsonObject>();
            try
            {
                var jsonStringTask = response.ResponseMessage.Content.ReadAsStringAsync();
                var jsonString = jsonStringTask.Result.Remove(jsonStringTask.Result.Length - 1);
                jsonString = jsonString.Remove(0, 11);
                jsonObjects = System.Text.Json.JsonSerializer.Deserialize<List<JsonObject>>(jsonString);
                return true;
            }
            catch (Exception e)
            {
                Logger.Info($"Json parsing error:\n {e}");
                return false;
            }
        }

        /// <summary>
        /// SevDesk's API needs a special treatment. Cuts parts of the jsonstring away.
        /// </summary>
        /// <param name="response"></param>
        /// <param name="jsonObjects"></param>
        /// <returns></returns>
        private List<JsonElement> GetJsonElements(IFlurlResponse response)
        {
            var jsonElements = new List<JsonElement>();
            GetJsonObjects(response, out var listJson);
            foreach (var jsonObject in listJson)
            {
                jsonElements.Add(jsonObject.AsJsonElement());
            }
            return jsonElements;
        }

        /// <summary>
        /// If response has some data id/date will be returned, else null will be returned
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        private Dictionary<string, object>? ExtractNextPageTokenResponse(IFlurlRequest request, IFlurlResponse response)
        {
            Logger.Info($"URL: {request.Url}");
            GetJObjects(response, out var listJson);
            if (listJson.Count == 0)
            {
                return new Dictionary<string, object>();
            }

            var responseValue = listJson[^1].TryGetValue("id", out var outIdValue);
            var responseDate = listJson[^1].TryGetValue("create", out var outDateValue);
            if (responseValue && responseDate)
            {
                currentState[outIdValue.ToString()] = outDateValue.ToString();
                return new Dictionary<string, object> { { "id", outIdValue.ToString() } };
            }
            else
            {
                //return new Dictionary<string, object>();
                return null;
            }
        }

        /// <summary>
        /// Builds the QueryParams depending on airbytes config.json and if NextpageToken is returning data. If NextPageToken != null it builds the OffsetPagination QueryParam.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="embed"></param>
        /// <param name="nextPageToken"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        private Dictionary<string, object> BuildQueryParams(string stream, string embed, Dictionary<string, object> nextPageToken, JsonElement config)
        {
            string? apiToken = config.GetProperty("api_token").GetString();
            int queryLimit = config.GetProperty("query_limit").GetInt32();
            int queryEmbedded = config.GetProperty("query_embedded").GetInt32();
            int queryOffset = config.GetProperty("query_offset").GetInt32();
            int cursorBased = config.GetProperty("cursor_based_pagination").GetInt32();
            var request = new Dictionary<string, object>();

            if (apiToken != null)
            {
                request["token"] = apiToken;
            }
            if (queryEmbedded != 0)
            {
                request["embed"] = embed;
            }
            if (queryLimit >= 0)
            {
                request["limit"] = queryLimit;
            }
            if (nextPageToken != null && cursorBased == 0)
            {
                //OffsetPagination
                request["offset"] = pageCount.GetValueOrDefault(stream);
                pageCount[stream] = pageCount.GetValueOrDefault(stream) + queryLimit;
            }
            else if (cursorBased == 0)
            {
                request["offset"] = 0;
                pageCount[stream] = 0;
            }
            return request;
        }

        /// <summary>
        /// here are all streams defined
        /// </summary>
        /// <param name="config">json wird von Airbyte per command übergeben</param>
        /// <returns></returns>
        public override Stream[] Streams(JsonElement config)
        {
            string? urlBase = config.GetProperty("base_url").GetString();
            int backOffTime = config.GetProperty("back_off_time").GetInt32();
            int maxRetries = config.GetProperty("max_retries").GetInt32();
            int checkPointInterval = config.GetProperty("checkpoint_interval").GetInt32();

            var baseImpl = urlBase.HttpStream().ParseResponseObject("$")
                .BackoffTime((i, _) => TimeSpan.FromMinutes(i * backOffTime))
                .HttpMethod(HttpMethod.Get)
                .MaxRetries(maxRetries)
                .StateCheckpointInterval(checkPointInterval);

            //###################################################
            //### Stream for Vouchers 
            //###################################################
            var voucherImpl = baseImpl
                //.GetUpdatedState((_, _) => currentstate.AsJsonElement())
                //.CursorField(new[] { "create" })
                .NextPageToken((request, response) => ExtractNextPageTokenResponse(request, response))
                .RequestParams((_, _, nextPageToken) => BuildQueryParams("Voucher", "", nextPageToken, config))
                .ParseResponse((response, _, _, _) => GetJsonElements(response))
                .Path((_, _, _) => "Voucher")
                .Create("Voucher");

            //###################################################
            //### Stream for VoucherPoses
            //###################################################
            var voucherPosImpl = baseImpl
                //.GetUpdatedState((_, _) => currentstate.AsJsonElement())
                //.CursorField(new[] { "create" })
                .NextPageToken((request, response) => ExtractNextPageTokenResponse(request, response))
                .RequestParams((_, _, nextPageToken) => BuildQueryParams("VoucherPos", "", nextPageToken, config))
                .ParseResponse((response, _, _, _) => GetJsonElements(response))
                .Path((_, _, _) => "VoucherPos")
                .Create("VoucherPos");

            //###################################################
            //### Stream for Invoices 
            //###################################################
            var invoiceImpl = baseImpl
            //.GetUpdatedState((_, _) => currentstate.AsJsonElement())
            //.CursorField(new[] { "create" })
                .NextPageToken((request, response) => ExtractNextPageTokenResponse(request, response))
                .RequestParams((_, _, nextPageToken) => BuildQueryParams("Invoice", "", nextPageToken, config))
                .ParseResponse((response, _, _, _) =>
                {
                    var responseData = new List<JsonElement>();
                    GetJsonObjects(response, out var listJson);
                    foreach (var jsonObject in listJson)
                    {
                        jsonObject.Remove("additionalInformation");
                        jsonObject.Remove("originLastInvoice");
                        jsonObject.Remove("accountStartDate");
                        jsonObject.Remove("sumDiscountNet");
                        jsonObject.Remove("sumDiscountGross");
                        jsonObject.Remove("sumDiscountNetForeignCurrency");
                        jsonObject.Remove("sumDiscountGrossForeignCurrency");

                        responseData.Add(jsonObject.AsJsonElement());
                    }
                    return responseData;
                })
                .Path((_, _, _) => "Invoice")
                .Create("Invoice");

            //###################################################
            //### Stream for InvoicePoses
            //###################################################
            var invoicePosImpl = baseImpl
                //.GetUpdatedState((_, _) => currentstate.AsJsonElement())
                //.CursorField(new[] { "create" })
                .NextPageToken((request, response) => ExtractNextPageTokenResponse(request, response))
                .RequestParams((_, _, nextPageToken) => BuildQueryParams("InvoicePos", "", nextPageToken, config))
                .ParseResponse((response, _, _, _) => GetJsonElements(response))
                .Path((_, _, _) => "InvoicePos")
                .Create("InvoicePos");

            //###################################################
            //### Stream for Contacts 
            //###################################################
            var contactImpl = baseImpl
                //.GetUpdatedState((_, _) => currentstate.AsJsonElement())
                //.CursorField(new[] { "create" })
                .NextPageToken((request, response) => ExtractNextPageTokenResponse(request, response))
                .RequestParams((_, _, nextPageToken) => BuildQueryParams("Contact", "", nextPageToken, config))
                .Path((_, _, _) => "Contact")
                .Create("Contact");

            //###################################################
            //### Stream for ContactAddress
            //###################################################
            var contactAddressImpl = baseImpl
                //.GetUpdatedState((_, _) => currentstate.AsJsonElement())
                //.CursorField(new[] { "create" })
                .NextPageToken((request, response) => ExtractNextPageTokenResponse(request, response))
                .RequestParams((_, _, nextPageToken) => BuildQueryParams("ContactAddress", "", nextPageToken, config))
                .ParseResponse((response, _, _, _) => GetJsonElements(response))
                .Path((_, _, _) => "ContactAddress")
                .Create("ContactAddress");

            //###################################################
            //### Stream for AccountingAddress
            //###################################################
            var accountingContactImpl = baseImpl
                //.GetUpdatedState((_, _) => currentstate.AsJsonElement())
                //.CursorField(new[] { "create" })
                .NextPageToken((request, response) => ExtractNextPageTokenResponse(request, response))
                .RequestParams((_, _, nextPageToken) => BuildQueryParams("AccountingContact", "", nextPageToken, config))
                .Path((_, _, _) => "AccountingContact")
                .Create("AccountingContact");

            //###################################################
            //### Stream for Orders 
            //###################################################
            var orderImpl = baseImpl
                .NextPageToken((request, response) => ExtractNextPageTokenResponse(request, response))
                .RequestParams((_, _, nextPageToken) => BuildQueryParams("Order", "", nextPageToken, config))
                .ParseResponse((response, _, _, _) => GetJsonElements(response))
                //.GetUpdatedState((_, _) => currentstate.AsJsonElement())
                //.CursorField(new[] { "create" })
                .Path((_, _, _) => "Order")
                .Create("Order");

            //###################################################
            //### Stream for OrderPoses
            //###################################################
            var orderPosImpl = baseImpl
                //.GetUpdatedState((_, _) => currentstate.AsJsonElement())
                //.CursorField(new[] { "create" })
                .NextPageToken((request, response) => ExtractNextPageTokenResponse(request, response))
                .RequestParams((_, _, nextPageToken) => BuildQueryParams("OrderPos", "", nextPageToken, config))
                .ParseResponse((response, _, _, _) => GetJsonElements(response))
                .Path((_, _, _) => "OrderPos")
                .Create("OrderPos");

            //###################################################
            //### Stream for CommunicationWay 
            //###################################################
            var communicationWayImpl = baseImpl
                //.GetUpdatedState((_, _) => currentstate.AsJsonElement())
                //.CursorField(new[] { "create" })
                .NextPageToken((request, response) => ExtractNextPageTokenResponse(request, response))
                .RequestParams((_, _, nextPageToken) => BuildQueryParams("CommunicationWay", "", nextPageToken, config))
                .ParseResponse((response, _, _, _) => GetJsonElements(response))
                .Path((_, _, _) => "CommunicationWay")
                .Create("CommunicationWay");

            //###################################################
            //### Stream for CommunicationWay 
            //###################################################
            var partImpl = baseImpl
                //.GetUpdatedState((_, _) => currentstate.AsJsonElement())
                //.CursorField(new[] { "create" })
                .NextPageToken((request, response) => ExtractNextPageTokenResponse(request, response))
                .RequestParams((_, _, nextPageToken) => BuildQueryParams("Part", "", nextPageToken, config))
                .ParseResponse((response, _, _, _) => GetJsonElements(response))
                .Path((_, _, _) => "Part")
                .Create("Part");

            //###################################################
            //### Stream for CommunicationWay 
            //###################################################
            var emailImpl = baseImpl
                //.GetUpdatedState((_, _) => currentstate.AsJsonElement())
                //.CursorField(new[] { "create" })

                // <summary>
                // Override this method to define a pagination strategy.
                // The value returned from this method is passed to most other methods in this class. Use it to form a request e.g: set headers or query params.
                // </summary>
                // <param name="response"></param>
                // <returns>The token for the next page from the input response object. Returning None means there are no more pages to read in this response.</returns>
                .NextPageToken((request, response) => ExtractNextPageTokenResponse(request, response))
                // <summary>
                // Parses the raw response object into a list of records.
                // By default, this returns an iterable containing the input. Override to parse differently.
                // </summary>
                // <param name="response"></param>
                // <param name="streamstate"></param>
                // <param name="streamslice"></param>
                // <param name="nextpagetoken"></param>
                // <returns></returns>
                .ParseResponse((response, _, _, _) =>
                {
                    List<JsonElement> jsonElements = new List<JsonElement>();
                    GetJsonObjects(response, out var listJson);
                   foreach (var jsonObject in listJson) 
                   { 
                        //JObject header = (JObject)jsonObject.SelectToken("Object.id");
                        //header.Property("ConversionValue").Remove();
                        //jsonObject.Remove("objectName");
                        jsonObject.Remove("additionalInformation");
                        //jsonObject.Remove("create");
                        //jsonObject.Remove("update");
                        //jsonObject.Remove("object");
                        //jsonObject.Remove("from");
                        //jsonObject.Remove("to");
                        //jsonObject.Remove("subject");
                        //jsonObject.Remove("text");
                        //jsonObject.Remove("sevClient");

                        jsonElements.Add(jsonObject.AsJsonElement());
                   }
                   return jsonElements;
                })
                // <summary>
                // Override this method to define the query parameters that should be set on an outgoing HTTP request given the inputs.
                // E.g: you might want to define query parameters for paging if next_page_token is not None.
                // </summary>
                // <param name="streamstate"></param>
                // <param name="streamslice"></param>
                // <param name="nextpagetoken"></param>
                // <returns></returns>
                .RequestParams((_, _, nextPageToken) => BuildQueryParams("Email", "object", nextPageToken, config))
                // <summary>
                // Returns the URL path for the API endpoint e.g: if you wanted to hit https://myapi.com/v1/some_entity then this should return "some_entity"
                // Defaults to {UrlBase}/{Name} where Name is the name of this stream
                // </summary>
                // <param name="streamstate"></param>
                // <param name="streamslice"></param>
                // <param name="nextpagetoken"></param>
                // <returns></returns>
                .Path((_, _, _) => "Email")
                .Create("Email");

            return new Stream[]
            {
                voucherImpl,
                voucherPosImpl,
                invoiceImpl,
                invoicePosImpl,
                contactImpl,
                contactAddressImpl,
                accountingContactImpl,
                orderImpl,
                orderPosImpl,
                communicationWayImpl,
                partImpl,
                emailImpl
            };
        }
    }

    [JsonObject]
    public class Email
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
    }
}