using System;
using System.Text.Json;
using System.Threading.Tasks;
using Airbyte.Cdk;
using Airbyte.Cdk.Sources;
using Airbyte.Cdk.Sources.Streams.Http.Auth;
using Airbyte.Cdk.Sources.Utils;
using Flurl;
using Flurl.Http;
using Stream = Airbyte.Cdk.Sources.Streams.Stream;

namespace SevDeskConnector
{
    public class Program : AbstractSource
    {
        public static async Task Main(string[] args) => await AirbyteEntrypoint.Main(args);

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
            Dictionary<string, DateTime> currentstate = new Dictionary<string, DateTime>();
            
            string? urlBase = config.GetProperty("base_url").GetString();
            int backOffTime = config.GetProperty("back_off_time").GetInt32();
            int checkPointInterval = config.GetProperty("checkpoint_interval").GetInt32();
            int maxRetries = config.GetProperty("max_retries").GetInt32();
            string? apiToken = config.GetProperty("api_token").GetString();

            var baseImpl = urlBase.HttpStream().ParseResponseObject("$")
                .BackoffTime((i, _) => TimeSpan.FromMinutes(i * backOffTime))
                .GetUpdatedState((_, _) => currentstate.AsJsonElement())
                .HttpMethod(HttpMethod.Get)
                .StateCheckpointInterval(checkPointInterval)
                .MaxRetries(maxRetries)
                .WithAuth(new BasicAuth(new[] { apiToken }))
                //.RequestParams(_,_,);
                .CursorField(new[] { "create" });
            //###################################################
            //### Stream for Vouchers 
            //###################################################
            var voucherImpl = baseImpl

                .Create("Voucher");

            //###################################################
            //### Stream for VoucherPoses
            //###################################################
            var voucherPosImpl = baseImpl

                .Create("VoucherPos");

            //###################################################
            //### Stream for Invoices 
            //###################################################
            var invoiceImpl = baseImpl

                .Create("Invoice");

            //###################################################
            //### Stream for InvoicePoses
            //###################################################
            var invoicePosImpl = baseImpl

                .Create("InvoicePos");

            //###################################################
            //### Stream for Contacts 
            //###################################################
            var contactImpl = baseImpl

                .Create("Contact");

            //###################################################
            //### Stream for ContactAddress
            //###################################################
            var contactAddressImpl = baseImpl

                .Create("ContactAddress");

            //###################################################
            //### Stream for AccountingAddress
            //###################################################
            var accountingContactImpl = baseImpl

                .Create("AccountingContact");

            //###################################################
            //### Stream for Orders 
            //###################################################
            var orderImpl = baseImpl

                .Create("Order");

            //###################################################
            //### Stream for OrderPoses
            //###################################################
            var orderPosImpl = baseImpl

                .Create("OrderPos");

            //###################################################
            //### Stream for CommunicationWay 
            //###################################################
            var communicationWayImpl = baseImpl

                .Create("CommunicationWay");

            //###################################################
            //### Stream for CommunicationWay 
            //###################################################
            var partImpl = baseImpl

                .Create("Part");

            //###################################################
            //### Stream for CommunicationWay 
            //###################################################
            var emailImpl = baseImpl

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