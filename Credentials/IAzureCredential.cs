//
// Copyright (c) Vincent LE TOUX for Ping Castle. All rights reserved.
// https://www.pingcastle.com
//
// Licensed under the Non-Profit OSL. See LICENSE file in the project root for full license information.
//
using PingCastleCloud.RESTServices.Azure;
using PingCastleCloud.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PingCastleCloud.Credentials
{
    public interface IAzureCredential
    {
        string Tenantid { get; }
        string TenantidToQuery { get; set; }
        Task<Token> GetToken<T>() where T : IAzureService;
        Token LastTokenQueried { get; }
    }
}
