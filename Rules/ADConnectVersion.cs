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
    [RuleModel("ADConnectVersion")]
    [RuleComputation(RuleComputationType.TriggerOnPresence, 15)]
    [RuleMaturityLevel(1)]
    [RuleMitreAttackTechnique(MitreAttackTechnique.ManintheMiddle)]
    public class ADConnectVersion : RuleBase
    {
        protected override int? AnalyzeDataNew(HealthCheckCloudData healthCheckCloudData)
        {
            if (healthCheckCloudData.ProvisionDirectorySynchronizationStatus == "Enabled")
            {
                Version v;
                if (Version.TryParse(healthCheckCloudData.ProvisionDirSyncClientVersion, out v))
                {
                    if (v.Major == 1)
                    {
                        if (v < new Version(1, 6, 11, 3))
                        {
                            AddRawDetail(healthCheckCloudData.ProvisionDirSyncClientVersion);
                        }
                    }
                    else if (v.Major == 2)
                    {
                        if (v < new Version(2, 0, 8, 0))
                        {
                            AddRawDetail(healthCheckCloudData.ProvisionDirSyncClientVersion);
                        }
                    }
                }
            }
            return null;
        }

    }
}
