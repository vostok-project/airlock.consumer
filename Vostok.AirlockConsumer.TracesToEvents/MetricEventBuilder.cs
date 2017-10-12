﻿using System.Collections.Generic;
using Vostok.Metrics;
using Vostok.Tracing;

namespace Vostok.AirlockConsumer.TracesToEvents
{
    internal static class MetricEventBuilder
    {
        public static MetricEvent Build(Span span)
        {
            var tags = BuildTags(span);
            var values = BuildValues(span);

            return new MetricEvent
            {
                Timestamp = span.EndTimestamp.Value,
                Tags = tags,
                Values = values
            };
        }

        private static IReadOnlyDictionary<string, double> BuildValues(Span span)
        {
            return new Dictionary<string, double>
            {
                [MetricsValueNames.Duration] = (span.EndTimestamp.Value - span.BeginTimestamp).TotalMilliseconds
            };
        }

        private static Dictionary<string, string> BuildTags(Span span)
        {
            var result = new Dictionary<string, string>();

            if (span.Annotations.TryGetValue(TracingAnnotationNames.Host, out var host))
            {
                result[MetricsTagNames.Host] = host;
            }

            if (span.Annotations.TryGetValue(TracingAnnotationNames.HttpCode, out var httpCode))
            {
                result[MetricsTagNames.Status] = httpCode;
            }

            if (span.Annotations.TryGetValue(TracingAnnotationNames.Operation, out var operation))
            {
                result[MetricsTagNames.Operation] = operation;
            }

            result[MetricsTagNames.Type] = "requests";

            return result;
        }
    }
}