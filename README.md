# AirByte-SevDesk-Connector

[![Docker Image CI](https://github.com/fl0wm0ti0n/AirByte-SevDesk-Connector/actions/workflows/docker-image-sevdesk.yml/badge.svg)](https://github.com/fl0wm0ti0n/AirByte-SevDesk-Connector/actions/workflows/docker-image-sevdesk.yml)

### Last Docker Image 
ghcr.io/fl0wm0ti0n/airbyte-sevdesk-source-connector:v1.14

### Important links
This Airbyte Connector is built for https://sevdesk.de/
SevDesk Api Documentation will be found here https://hilfe.sevdesk.de/de/knowledge/sevdesk-rest-full-api
All Infos about Airbyte will be found at https://airbyte.com/
The Connector is built with the C# CDK which you can find here https://github.com/mrhamburg/airbyte.cdk.dotnet

### Hints
sevdesk api documentation is totaly wrong and they sometimes deliver string inbstead of float or fields which arent described in the docu. 
the connector handles this issues right now but sevdesk said they update their api sometimes, if this happens this connector will have errors. i will fix this asap if i know about the new issues.

### Open TODOs
*  incremental sync
*  schemas for embedded api results needs to be created -> see options "query_embedded"

### Following options are possible to be configured for this connector
"start_date":\
"description": "Start getting data from that date."

"api_token":\
"description": "API Token you got from SevDesk"

"base_url":\
"description": "API Token you got from SevDesk"

"back_off_time":\
"description": "Backofftime, default 10."

"checkpoint_interval":\
"description": "The AirbyteStateMessage is sent based on the StateCheckpointInterval setting of a stream object. Every N number of requests will result in sending out an AirbyteStateMessage"

"max_retries":\
"description": "Maximal retries before it stops"

"connection_check_api":\
"description": "api Endpoint like 'Voucher' from 'https://my.sevdesk.de/api/v1/Voucher'"

"query_embedded":\
"description": "activate it, if you want detailed output of nested objects. Leads to a massive increase of the amount of data. Good if you want to have all data linked together since BigQuery normalization doesn't support to link all streams together as the IDs of the streams would suggest it"

"query_limit":\
"description": "Limits the number of entries that are returned.Most useful in GET requests which will most likely deliver big sets of data like country or currency lists. In this case, you can bypass the default limitation on returned entries by providing a high number."

"query_offset":\
"description": "Specifies a certain offset for the data that will be returned. As an example, you can specify 'offset=2 if you want all entries except for the first two."
        
"cursor_based_pagination":\
"description": "If the api will support cursor-based pagination in the future"
