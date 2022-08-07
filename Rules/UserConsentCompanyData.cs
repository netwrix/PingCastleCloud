//
// Copyright (c) Vincent LE TOUX for Ping Castle. All rights reserved.
// https://www.pingcastle.com
//
// Licensed under the Non-Profit OSL. See LICENSE file in the project root for full license information.
//
using PingCastleCloud.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PingCastleCloud.Rules
{
    [RuleModel("UserConsentCompanyData")]
    [RuleComputation(RuleComputationType.TriggerOnPresence, 5)]
    [RuleMaturityLevel(2)]
    public class UserConsentCompanyData : RuleBase
    {
        protected override int? AnalyzeDataNew(HealthCheckCloudData healthCheckCloudData)
        {
            if (healthCheckCloudData.UsersPermissionToUserConsentToAppEnabled == true)
            {
                AddRawDetail("true");
            }
            return null;
        }

    }
}
