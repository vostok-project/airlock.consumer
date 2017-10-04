﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Vostok.Commons.Extensions.UnitConvertions;
using Vostok.Metrics;

namespace Vostok.AirlockConsumer.MetricsAggregator
{
    internal class Bucket : IBucket
    {
        private readonly IReadOnlyDictionary<string, string> tags;
        private readonly TimeSpan period;
        private readonly ConcurrentDictionary<DateTimeOffset, TimeBin> timeBins;
        private readonly ICounter missedPastEvents;
        private readonly ICounter missedFutureEvents;
        private Borders borders;

        public Bucket(
            IMetricScope metricScope,
            IReadOnlyDictionary<string, string> tags,
            TimeSpan period,
            Borders borders)
        {
            this.tags = tags;
            this.period = period;
            timeBins = new ConcurrentDictionary<DateTimeOffset, TimeBin>();
            missedPastEvents = metricScope.Counter(1.Minutes(), "missed_past_events");
            missedFutureEvents = metricScope.Counter(1.Minutes(), "missed_future_events");
            this.borders = NormalizeBorders(borders);
        }

        public void Consume(IReadOnlyDictionary<string, double> values, DateTimeOffset timestamp)
        {
            var normalizedTimestamp = NormalizeTimestamp(timestamp);
            var currentBorders = Interlocked.CompareExchange(ref borders, null, null);

            if (normalizedTimestamp < currentBorders.Past)
            {
                missedPastEvents.Add();
                return;
            }

            if (normalizedTimestamp >= currentBorders.Future)
            {
                missedFutureEvents.Add();
                return;
            }

            var timeBin = timeBins.GetOrAdd(normalizedTimestamp, _ => new TimeBin());
            timeBin.Counter.Add();
            foreach (var kvp in values)
            {
                var meter = timeBin.Meters.GetOrAdd(kvp.Key, _ => new Meter());
                meter.Add(kvp.Value);
            }
        }

        public IEnumerable<MetricEvent> Reset(Borders nextBorders)
        {
            nextBorders = NormalizeBorders(nextBorders);
            Interlocked.Exchange(ref borders, nextBorders);
            foreach (var kvp in timeBins.Where(x => x.Key < nextBorders.Past))
            {
                if (!timeBins.TryRemove(kvp.Key, out _))
                    continue;
                var timestamp = kvp.Key;
                var timeBin = kvp.Value;
                var values = new Dictionary<string, double>
                {
                    {"count", timeBin.Counter.Reset()}
                };
                foreach (var m in timeBin.Meters)
                {
                    var meterValues = m.Value.GetValues();
                    foreach (var mv in meterValues)
                        values[m.Key + "_" + mv.Key] = mv.Value;
                }
                yield return new MetricEvent
                {
                    Timestamp = timestamp,
                    Tags = tags,
                    Values = values
                };
            }
        }

        private Borders NormalizeBorders(Borders b)
        {
            return new Borders(NormalizeTimestamp(b.Past), NormalizeTimestamp(b.Future));
        }

        private DateTimeOffset NormalizeTimestamp(DateTimeOffset timestamp)
        {
            return new DateTimeOffset(timestamp.Ticks - timestamp.Ticks%period.Ticks, TimeSpan.Zero);
        }

        private class TimeBin
        {
            public ConcurrentDictionary<string, Meter> Meters { get; } = new ConcurrentDictionary<string, Meter>();
            public Counter Counter { get; } = new Counter();
        }
    }
}