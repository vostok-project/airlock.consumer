﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Vostok.Airlock;
using Vostok.AirlockConsumer.Logs;
using Vostok.AirlockConsumer.Tracing;
using Vostok.Contrails.Client;
using Vostok.Logging;

namespace Vostok.AirlockConsumer.IntergationTests
{
    public class ConsumerIntegrationTests : BaseTestClass
    {
        [Test]
        public void SendLogEventsToAirlock_GotItAtElastic()
        {
            var eventCount = 10;

            var dateTimeOffset = DateTimeOffset.UtcNow;
            var testId = Guid.NewGuid().ToString("N");

            var logEventDictionary = SendLogEvents(eventCount, dateTimeOffset, testId);

            var elasticUris = ElasticLogsIndexerEntryPoint.GetElasticUris(Log, EnvironmentVariables);
            var connectionPool = new StickyConnectionPool(elasticUris);
            var elasticConfig = new ConnectionConfiguration(connectionPool);
            var elasticClient = new ElasticLowLevelClient(elasticConfig);
            var routingKey = RoutingKey.ReplaceSuffix(RoutingKeyPrefix, RoutingKey.LogsSuffix);
            var indexName = string.Format("{0}-{1:yyyy.MM.dd}", routingKey.Replace('.', '-'), dateTimeOffset);
            //Thread.Sleep(10000);
            var applicationHost = new ConsumerApplicationHost<ElasticLogsIndexerEntryPoint>();
            var task = new Task(
                () =>
                {
                    applicationHost.Run();
                }, TaskCreationOptions.LongRunning);
            task.Start();

            WaitHelper.Wait(
                () =>
                {
                    var response = elasticClient.Search<string>(
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
                    Log.Debug("elastic responce: " + response.Body);
                    if (!response.Success)
                        throw new Exception("elastic error");
                    dynamic jObject = JObject.Parse(response.Body);
                    var hits = (JArray) jObject.hits.hits;
                    if (logEventDictionary.Count != hits.Count)
                        return WaitAction.ContinueWaiting;
                    foreach (dynamic hit in hits)
                    {
                        string message = hit._source.Message;
                        Assert.True(logEventDictionary.ContainsKey(message));
                    }
                    return WaitAction.StopWaiting;
                });
            applicationHost.Stop();
        }

        [Test]
        public void SendTraceEventsToAirlock_GotItAtCassandra()
        {
            var spansList = SendTraces(10);
            var contrailsClientSettings = TracingAirlockConsumerEntryPoint.GetContrailsClientSettings(Log, EnvironmentVariables);
            var contrailsClient = new ContrailsClient(contrailsClientSettings, Log);

            var applicationHost = new ConsumerApplicationHost<TracingAirlockConsumerEntryPoint>();
            var task = new Task(
                () =>
                {
                    applicationHost.Run();
                }, TaskCreationOptions.LongRunning);
            task.Start();

            WaitHelper.Wait(
                () =>
                {
                    foreach (var span in spansList)
                    {
                        var tracesById = contrailsClient.GetTracesById(span.TraceId, null, null, null, null, true).GetAwaiter().GetResult().ToArray();
                        if (tracesById.Length == 0)
                            return false;
                        var spanResult = tracesById[0];

                        Log.Debug("got span " + spanResult.ToJson());
                        Assert.AreEqual(1, tracesById.Length);
                        Assert.AreEqual(span.SpanId, spanResult.SpanId);
                    }
                    return true;
                });
            applicationHost.Stop();
        }
    }
}