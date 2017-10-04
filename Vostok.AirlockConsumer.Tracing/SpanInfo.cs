﻿using System;

namespace Vostok.AirlockConsumer.Tracing
{
    [Cassandra.Mapping.Attributes.Table]
    public class SpanInfo
    {
        [Cassandra.Mapping.Attributes.Column("trace_prefix_id")]
        [Cassandra.Mapping.Attributes.PartitionKey]
        public string TraceIdPrefix
        {
            get => traceIdPrefix = traceIdPrefix ?? GetTraceIdPrefix(TraceId);
            set => traceIdPrefix = value;
        }
        private string traceIdPrefix;

        [Cassandra.Mapping.Attributes.ClusteringKey]
        [Cassandra.Mapping.Attributes.Column("begin_timestamp")]
        public DateTimeOffset BeginTimestamp { get; set; }

        [Cassandra.Mapping.Attributes.Column("trace_id")]
        [Cassandra.Mapping.Attributes.ClusteringKey(1)]
        public Guid TraceId { get; set; }

        [Cassandra.Mapping.Attributes.ClusteringKey(2)]
        [Cassandra.Mapping.Attributes.Column("span_id")]
        public Guid SpanId { get; set; }

        [Cassandra.Mapping.Attributes.Column("parent_span_id")]
        public Guid? ParentSpanId { get; set; }

        [Cassandra.Mapping.Attributes.Column("end_timestamp")]
        public DateTimeOffset? EndTimestamp { get; set; }

        [Cassandra.Mapping.Attributes.Column("annotations")]
        public string Annotations { get; set; }

        public static string GetTraceIdPrefix(Guid traceId)
        {
            return traceId.ToString("N").Substring(0, 8);
        }
    }
}