//
// Copyright (c) Vincent LE TOUX for Ping Castle. All rights reserved.
// https://www.pingcastle.com
//
// Licensed under the Non-Profit OSL. See LICENSE file in the project root for full license information.
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using PingCastleCloud.Data;

namespace PingCastleCloud.Rules
{
    public class RuleSet
    {
        private static Dictionary<string, RuleBase> _cachedRules = null;

        public static IEnumerable<RuleBase> Rules
        {
            get
            {
                if (_cachedRules == null || _cachedRules.Count == 0)
                {
                    ReloadRules();
                }
                return _cachedRules.Values;
            }
        }

        public static void ReloadRules()
        {
            _cachedRules = new Dictionary<string, RuleBase>();
            LoadRules(_cachedRules);
        }

        public static void LoadRules(Dictionary<string, RuleBase> rules)
        {
            // important: to work with W2000, we cannot use GetType because it will instanciate .Net 3.0 class then load the missing assembly
            // the trick here is to check only the exported type and put as internal the class using .Net 3.0 functionalities
            foreach (Type type in Assembly.GetAssembly(typeof(RuleSet)).GetExportedTypes())
            {
                if (type.IsSubclassOf(typeof(RuleBase)) && !type.IsAbstract)
                {
                    try
                    {
                        var a = (RuleBase)Activator.CreateInstance(type);
                        rules.Add(a.RiskId, a);
                    }
                    catch (Exception)
                    {
                        Trace.WriteLine("Unable to instanciate the type " + type);
                        throw;
                    }
                }
            }
        }

        public static void LoadCustomRules()
        {
            // force the load of rules
            var output = Rules;

            var customRules = CustomRulesSettings.GetCustomRulesSettings();
            if (customRules.CustomRules != null)
            {
                foreach (CustomRuleSettings rule in customRules.CustomRules)
                {
                    var riskId = rule.RiskId;
                    RuleBase matchedRule = GetRuleFromID(riskId);
                    if (matchedRule == null)
                    {
                        Trace.WriteLine("Rule computation does not match an existing ID (" + riskId + ")");
                        continue;
                    }
                    if (rule.Computations != null)
                    {
                        matchedRule.RuleComputation.Clear();
                        foreach (CustomRuleComputationSettings c in rule.Computations)
                        {
                            matchedRule.RuleComputation.Add(c.GetAttribute());
                        }
                    }
                    if (rule.MaturityLevel != 0)
                    {
                        matchedRule.MaturityLevel = rule.MaturityLevel;
                    }
                }
            }
        }

        // when multiple reports are ran each after each other, internal state can be kept
        void ReInitRule(RuleBase rule)
        {
            rule.Initialize();
        }

        public List<RuleBase> ComputeRiskRules(HealthCheckCloudData data)
        {
            var output = new List<RuleBase>();
            Trace.WriteLine("Begining to run risk rule");
            foreach (var rule in Rules)
            {
                string ruleName = rule.GetType().ToString();
                Trace.WriteLine("Rule: " + ruleName);
                try
                {
                    ReInitRule(rule);
                    if (rule.Analyze(data))
                    {
                        Trace.WriteLine("  matched");
                        output.Add(rule);
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("An exception occured when running the rule : " + ruleName);
                    Trace.WriteLine("An exception occured when running the rule : " + ruleName);
                    Console.WriteLine("Please contact support@pingcastle.com with the following details so the problem can be fixed");
                    Console.ResetColor();
                    Console.WriteLine("Message: " + ex.Message);
                    Trace.WriteLine("Message: " + ex.Message);
                    Console.WriteLine("StackTrace: " + ex.StackTrace);
                    Trace.WriteLine("StackTrace: " + ex.StackTrace);
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine("Inner StackTrace: " + ex.InnerException.StackTrace);
                        Trace.WriteLine("Inner StackTrace: " + ex.InnerException.StackTrace);
                    }
                }

            }
            Trace.WriteLine("Risk rule run stopped");
            ReComputeTotals(data, output.ConvertAll(x => x));
            return output;
        }

        public static void ReComputeTotals(HealthCheckCloudData data, IEnumerable<RuleBase> rules)
        {
            // consolidate scores
            data.GlobalScore = 0;
            /*data.StaleObjectsScore = 0;
            data.PrivilegiedGroupScore = 0;
            data.TrustScore = 0;
            data.AnomalyScore = 0;*/
            data.MaturityLevel = 5;
            foreach (var rule in rules)
            {
                /*switch (rule.Category)
                {
                    case RiskRuleCategory.Anomalies:
                        data.AnomalyScore += rule.Points;
                        break;
                    case RiskRuleCategory.PrivilegedAccounts:
                        data.PrivilegiedGroupScore += rule.Points;
                        break;
                    case RiskRuleCategory.StaleObjects:
                        data.StaleObjectsScore += rule.Points;
                        break;
                    case RiskRuleCategory.Trusts:
                        data.TrustScore += rule.Points;
                        break;
                }*/
                data.GlobalScore += rule.Points;
                var hcrule = RuleSet.GetRuleFromID(rule.RiskId);
                if (hcrule != null)
                {
                    int level = hcrule.MaturityLevel;
                    if (level > 0 && level < data.MaturityLevel)
                        data.MaturityLevel = level;
                }
            }
            // limit to 100
            /*if (data.StaleObjectsScore > 100)
                data.StaleObjectsScore = 100;
            if (data.PrivilegiedGroupScore > 100)
                data.PrivilegiedGroupScore = 100;
            if (data.TrustScore > 100)
                data.TrustScore = 100;
            if (data.AnomalyScore > 100)
                data.AnomalyScore = 100;
            // max of all scores
            data.GlobalScore = Math.Max(data.StaleObjectsScore,
                                            Math.Max(data.PrivilegiedGroupScore,
                                            Math.Max(data.TrustScore, data.AnomalyScore)));*/
        }

        public static string GetRuleDescription(string ruleid)
        {
            if (_cachedRules == null || _cachedRules.Count == 0)
            {
                ReloadRules();
            }
            if (_cachedRules.ContainsKey(ruleid))
                return _cachedRules[ruleid].Title;
            return String.Empty;
        }

        public static RuleBase GetRuleFromID(string ruleid)
        {
            if (_cachedRules == null || _cachedRules.Count == 0)
            {
                ReloadRules();
            }
            if (_cachedRules.ContainsKey(ruleid))
                return _cachedRules[ruleid];
            return null;

        }
    }
}
