using System;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Airbyte.Cdk;
using Airbyte.Cdk.Sources;
using Airbyte.Cdk.Sources.Streams.Http.Auth;
using Airbyte.Cdk.Sources.Utils;
using Flurl;
using Flurl.Http;
using Json.More;
using Stream = Airbyte.Cdk.Sources.Streams.Stream;

namespace SevDeskConnector
{
    public class Program : AbstractSource
    {
        public static async Task Main(string[] args) => await AirbyteEntrypoint.Main(args);
        static AirbyteLogger Logger { get; } = new();

        //public string urlBase => "https://my.sevdesk.de/api/v1";

        /// <summary>
        /// CheckConnection wird immer zu begin vopn Airbyte aufgerufen
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
        /// Hauptroutine - Alle Streams und damit alle ApiAbfragen zu SevDesk - Hier werden alle anpassungen gemacht. hier muss konfiguriert werden wie die Daten ausgelesen werden.
        /// </summary>
        /// <param name="config">json wird von Airbyte per command übergeben</param>
        /// <returns></returns>
        public override Stream[] Streams(JsonElement config)
        {
            Dictionary<string, string> currentState = new Dictionary<string, string>();
            //Dictionary<string, string> nextPageToken = new Dictionary<string, string>();
            Dictionary<string, int> pageCount = new Dictionary<string, int>();
            int pageIndex = 0;

            string? urlBase = config.GetProperty("base_url").GetString();
            int backOffTime = config.GetProperty("back_off_time").GetInt32();
            int maxRetries = config.GetProperty("max_retries").GetInt32();
            string? apiToken = config.GetProperty("api_token").GetString();
            int checkPointInterval = config.GetProperty("checkpoint_interval").GetInt32();
            int queryLimit = config.GetProperty("query_limit").GetInt32();
            int queryEmbedded = config.GetProperty("query_embedded").GetInt32();
            int queryOffset = config.GetProperty("query_offset").GetInt32();

            var baseImpl = urlBase.HttpStream().ParseResponseObject("$")
                .BackoffTime((i, _) => TimeSpan.FromMinutes(i * backOffTime))
                .HttpMethod(HttpMethod.Get)
                .MaxRetries(maxRetries)
                .PageSize(queryLimit);
            //.StateCheckpointInterval(checkPointInterval)

            //###################################################
            //### Stream for Vouchers 
            //###################################################
            var voucherImpl = baseImpl
                //.GetUpdatedState((_, _) => currentstate.AsJsonElement())
                //.CursorField(new[] { "create" })
                .RequestParams((_, _, _) =>
                {
                    var request = new Dictionary<string, object>();

                    if (apiToken != null)
                    {
                        request["token"] = apiToken;
                    }
                    return request;
                })
                .Path((_, _, _) => "Voucher")
                .Create("Voucher");

            //###################################################
            //### Stream for VoucherPoses
            //###################################################
            var voucherPosImpl = baseImpl
                //.GetUpdatedState((_, _) => currentstate.AsJsonElement())
                //.CursorField(new[] { "create" })
                .RequestParams((_, _, _) =>
                {
                    var request = new Dictionary<string, object>();

                    if (apiToken != null)
                    {
                        request["token"] = apiToken;
                    }
                    return request;
                })
                .Path((_, _, _) => "VoucherPos")
                .Create("VoucherPos");

            //###################################################
            //### Stream for Invoices 
            //###################################################
            var invoiceImpl = baseImpl
            //.GetUpdatedState((_, _) => currentstate.AsJsonElement())
            //.CursorField(new[] { "create" })
            //.ParseResponse((_,_,_,) =>
            //    {
            //        return response;
            //    })
            .NextPageToken((request, response) =>
            {
                var responseBody = response.ResponseMessage.Content.AsJsonElement();
                //DateTime date = DateTime.ParseExact(request.Url.QueryParams.FirstOrDefault("create").ToString(),"O", CultureInfo.CurrentCulture);
                //var responseValue = request.Url.QueryParams.FirstOrDefault("id").ToString();
                //var date = request.Url.QueryParams.FirstOrDefault("create").ToString();
                var responseValue = responseBody.TryGetProperty("id", out var outvalue);
                var responseDate = responseBody.TryGetProperty("create", out var outDate);
                //outDate.TryGetDateTime(out var outDate2);

                Logger.Info($"Response: {responseBody}");
                Logger.Info($"URL: {request.Url}");

                if (responseValue && responseDate)
                {
                    currentState[outvalue.ToString()] = outDate.ToString();
                    return new Dictionary<string, object> { { "id", outvalue.ToString() } };
                }
                else
                {
                    return null;
                }
            })
            .RequestParams((_, _, nextPageToken) =>
                {
                    var request = new Dictionary<string, object>();

                    if (apiToken != null)
                    {
                        request["token"] = apiToken;
                    }
                    if (queryEmbedded != 0)
                    {
                        request["embed"] = "contact";
                    }
                    if (queryLimit >= 0)
                    {
                        request["limit"] = queryLimit;
                    }
                    if (nextPageToken != null)
                    {
                        //DateTime dt = currentState[nextPageToken[].ToString()].AddDays(1);
                        request["offset"] = pageCount.GetValueOrDefault("Invoice");
                        pageCount["Invoice"] = pageCount.GetValueOrDefault("Invoice") + queryLimit;
                    }
                    else
                    {
                        request["offset"] = 0;
                        pageCount["Invoice"] = 0;
                    }
                    return request;
                })
            .Path((_, _, _) => "Invoice")
            .Create("Invoice");

            //###################################################
            //### Stream for InvoicePoses
            //###################################################
            var invoicePosImpl = baseImpl
                //.GetUpdatedState((_, _) => currentstate.AsJsonElement())
                //.CursorField(new[] { "create" })
                .RequestParams((_, _, _) =>
                {
                    var request = new Dictionary<string, object>();

                    if (apiToken != null)
                    {
                        request["token"] = apiToken;
                    }
                    return request;
                })
                .Path((_, _, _) => "InvoicePos")
                .Create("InvoicePos");

            //###################################################
            //### Stream for Contacts 
            //###################################################
            var contactImpl = baseImpl
                //.GetUpdatedState((_, _) => currentstate.AsJsonElement())
                //.CursorField(new[] { "create" })
                .RequestParams((_, _, _) =>
                {
                    var request = new Dictionary<string, object>();

                    if (apiToken != null)
                    {
                        request["token"] = apiToken;
                    }
                    return request;
                })
                .Path((_, _, _) => "Contact")
                .Create("Contact");

            //###################################################
            //### Stream for ContactAddress
            //###################################################
            var contactAddressImpl = baseImpl
                //.GetUpdatedState((_, _) => currentstate.AsJsonElement())
                //.CursorField(new[] { "create" })
                .RequestParams((_, _, _) =>
                {
                    var request = new Dictionary<string, object>();

                    if (apiToken != null)
                    {
                        request["token"] = apiToken;
                    }
                    return request;
                })
                .Path((_, _, _) => "ContactAddress")
                .Create("ContactAddress");

            //###################################################
            //### Stream for AccountingAddress
            //###################################################
            var accountingContactImpl = baseImpl
                //.GetUpdatedState((_, _) => currentstate.AsJsonElement())
                //.CursorField(new[] { "create" })
                .RequestParams((_, _, _) =>
                {
                    var request = new Dictionary<string, object>();

                    if (apiToken != null)
                    {
                        request["token"] = apiToken;
                    }
                    return request;
                })
                .Path((_, _, _) => "AccountingContact")
                .Create("AccountingContact");

            //###################################################
            //### Stream for Orders 
            //###################################################
            var orderImpl = baseImpl
                .RequestParams((_, _, _) =>
                {
                    var request = new Dictionary<string, object>();

                    if (apiToken != null)
                    {
                        request["token"] = apiToken;
                    }
                    return request;
                })
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
                .RequestParams((_, _, _) =>
                {
                    var request = new Dictionary<string, object>();

                    if (apiToken != null)
                    {
                        request["token"] = apiToken;
                    }
                    return request;
                })
                .Path((_, _, _) => "OrderPos")
                .Create("OrderPos");

            //###################################################
            //### Stream for CommunicationWay 
            //###################################################
            var communicationWayImpl = baseImpl
                //.GetUpdatedState((_, _) => currentstate.AsJsonElement())
                //.CursorField(new[] { "create" })
                .RequestParams((_, _, _) =>
                {
                    var request = new Dictionary<string, object>();

                    if (apiToken != null)
                    {
                        request["token"] = apiToken;
                    }
                    return request;
                })
                .Path((_, _, _) => "CommunicationWay")
                .Create("CommunicationWay");

            //###################################################
            //### Stream for CommunicationWay 
            //###################################################
            var partImpl = baseImpl
                //.GetUpdatedState((_, _) => currentstate.AsJsonElement())
                //.CursorField(new[] { "create" })
                .RequestParams((_, _, _) =>
                {
                    var request = new Dictionary<string, object>();

                    if (apiToken != null)
                    {
                        request["token"] = apiToken;
                    }
                    return request;
                })
                .Path((_, _, _) => "Part")
                .Create("Part");

            //###################################################
            //### Stream for CommunicationWay 
            //###################################################
            var emailImpl = baseImpl
                //.GetUpdatedState((_, _) => currentstate.AsJsonElement())
                //.CursorField(new[] { "create" })
                .RequestParams((_, _, _) =>
                {
                    var request = new Dictionary<string, object>();

                    if (apiToken != null)
                    {
                        request["token"] = apiToken;
                    }
                    return request;
                })
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
}