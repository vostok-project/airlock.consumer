﻿using System;
using System.Collections.Generic;
using System.Linq;
using Elasticsearch.Net;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Vostok.Airlock;
using Vostok.Airlock.Logging;
using Vostok.Logging;

namespace Vostok.AirlockConsumer.IntergationTests
{
    public class LogsTests
    {
        private static readonly string routingKey = RoutingKey.Create(IntegrationTestsEnvironment.Project, IntegrationTestsEnvironment.Environment, nameof(LogsTests), RoutingKey.LogsSuffix);

        [Test]
        public void SendLogEventsToAirlock_GotItAtElastic()
        {
            const int eventCount = 10;
            var logEvents = GenerateLogEvens(eventCount);
            PushToAirlock(logEvents);

            var connectionPool = new StickyConnectionPool(new[] {new Uri("http://localhost:9200")});
            var elasticConfig = new ConnectionConfiguration(connectionPool, cfg =>
            {
                cfg.EnableDebugMode();
                return null;
            });
            var elasticClient = new ElasticLowLevelClient(elasticConfig);
            var indexName = $"{IntegrationTestsEnvironment.Project}-{IntegrationTestsEnvironment.Environment}-{logEvents.First().Timestamp:yyyy.MM.dd}";

            var testId = logEvents.First().Properties["testId"];
            var expectedLogMessages = new HashSet<string>(logEvents.Select(x => x.Message));
            WaitHelper.WaitSafe(
                () =>
                {
                    var elasticsearchResponse = elasticClient.Search<string>(
                        indexName,
                        "LogEvent",
                        new
                        {
                            from = 0,
                            size = eventCount,
                            query = new
                            {
                                match = new
                                {
                                    testId,
                                }
                            }
                        });
                    IntegrationTestsEnvironment.Log.Debug(elasticsearchResponse.DebugInformation);
                    if (!elasticsearchResponse.Success)
                        return WaitAction.ContinueWaiting;
                    var hits = (JArray) ((dynamic) JObject.Parse(elasticsearchResponse.Body)).hits.hits;
                    if (expectedLogMessages.Count != hits.Count)
                        return WaitAction.ContinueWaiting;
                    foreach (dynamic hit in hits)
                    {
                        string message = hit._source.Message;
                        Assert.True(expectedLogMessages.Contains(message));
                    }
                    return WaitAction.StopWaiting;
                });
        }

        [Test]
        [Category("Load")]
        public void PushManyLogEventsToAirlock()
        {
            PushToAirlock(GenerateLogEvens(count: 100_000));
        }

        private static LogEventData[] GenerateLogEvens(int count)
        {
            var utcNow = DateTimeOffset.UtcNow;
            var testId = Guid.NewGuid().ToString("N");
            return Enumerable.Range(0, count)
                             .Select(i => new LogEventData
                             {
                                 Message = "hello!" + i,
                                 Level = LogLevel.Debug,
                                 Timestamp = utcNow.AddMilliseconds(-i*10),
                                 Properties = new Dictionary<string, string> {["testId"] = testId}
                             }).ToArray();
        }

        private static void PushToAirlock(LogEventData[] logEvents)
        {
            IntegrationTestsEnvironment.PushToAirlock(routingKey, logEvents, e => e.Timestamp);
        }
    }
}