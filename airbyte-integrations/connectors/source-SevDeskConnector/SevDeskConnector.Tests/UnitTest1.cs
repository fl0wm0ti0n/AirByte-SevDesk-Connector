using System;
using System.Text.Json;
using Airbyte.Cdk;
using Airbyte.Cdk.Models;
using Airbyte.Cdk.Sources;
using Airbyte.Cdk.Sources.Streams;
using Airbyte.Cdk.Sources.Utils;
using Flurl;
using Moq;
using Xunit;

namespace SevDeskConnector.Tests
{
    public class UnitTest1
    {

        [Fact]
        public void UrlBuild1()
        {

            var url = "https://my.sevdesk.de/api/v1/".SetQueryParam("token", "12345");
            url.AppendPathSegment("entpoint");
            Assert.Equal("https://my.sevdesk.de/api/v1/entpoint?token=12345", url);

        }
        [Fact]
        public void UrlBuild2()
        {
            string? checkEndpoint = "endpoint";
            string? apiToken = "12345";

            string? urlBase = "https://my.sevdesk.de/api/v1/";
            urlBase = urlBase.AppendPathSegment("endpoint");
            urlBase = urlBase.SetQueryParam("token", apiToken);

            Assert.Equal("https://my.sevdesk.de/api/v1/endpoint?token=12345", urlBase);

        }
        [Fact]
        public void UrlBuild3()
        {
            string? checkEndpoint = "endpoint";
            string? apiToken = "12345";

            string? urlBase = "https://my.sevdesk.de/api/v1/"
                .SetQueryParams(new
                {
                    token = apiToken
                });

            Assert.Equal("https://my.sevdesk.de/api/v1/endpoint?token=12345", urlBase.AppendPathSegment(checkEndpoint));

        }
    }
}