//
// Copyright (c) Vincent LE TOUX for Ping Castle. All rights reserved.
// https://www.pingcastle.com
//
// Licensed under the Non-Profit OSL. See LICENSE file in the project root for full license information.
//
using PingCastleCloud.Credentials;
using PingCastleCloud.RESTServices.Azure;
using PingCastleCloud.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace PingCastleCloud.RESTServices
{
    [AzureService("535fb089-9ff3-47b6-9bfb-4f1264799865", "https://graph.microsoft.com")]
    public class MicrosoftGraph : RESTClientBase<MicrosoftGraph>, IAzureService
    {
        public MicrosoftGraph(IAzureCredential credential) : base(credential)
        {
        }
        protected override string BuidEndPoint(string function, string optionalQuery)
        {
            var query = HttpUtility.ParseQueryString(optionalQuery);
            //query["api-version"] = "1.61-internal";

            var builder = new UriBuilder("https://graph.microsoft.com/beta/" + function);
            builder.Query = query.ToString();
            return builder.ToString();
        }

        public string GetMe()
        {
            return CallEndPoint<string>("me");
        }

        // message=Insufficient privileges to complete the operation.
        public string GetTenantRelationships(string tenantId)
        {
            return CallEndPoint<string>("tenantRelationships/findTenantInformationByTenantId(tenantId='" + tenantId + "')");
        }
    }
}
