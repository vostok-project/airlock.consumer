﻿using Vostok.Airlock;

namespace Vostok.AirlockConsumer.MetricsAggregator
{
    public class MetricsAggregatorRotingKeyFilter : IRoutingKeyFilter
    {
        public bool Matches(string routingKey)
        {
            return RoutingKey.LastSuffixMatches(routingKey, RoutingKey.AppEventsSuffix) ||
                   RoutingKey.LastSuffixMatches(routingKey, RoutingKey.TracesSuffix);
        }
    }
}