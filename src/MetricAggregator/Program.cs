using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Newtonsoft.Json;

// ReSharper disable InconsistentNaming

namespace MetricAggregator
{
    class Program
    {
        static async Task Main(string[] args)
        {
            const string KAFKA_BOOTSTRAP_SERVER = "KAFKA_BOOTSTRAP_SERVER";
            const string KAFKA_TOPIC = "KAFKA_TOPIC";
            const string METRIC_ENDPOINT = "METRIC_ENDPOINT";
            const string METRIC_METHOD = "METRIC_METHOD";
            const string METRIC_INTERVAL = "METRIC_INTERVAL";
            const string METRIC_HEADERS = "METRIC_HEADERS";

            string GetEnvironmentOrThrow(string env, string info)
            {
                var value = Environment.GetEnvironmentVariable(env);

                if (string.IsNullOrEmpty(value))
                {
                    throw new Exception($"Expected {info} in ENV {env}!");
                }

                return value;
            }
            
            #region Setup

            var server = GetEnvironmentOrThrow(KAFKA_BOOTSTRAP_SERVER, "a Kafka bootstrap server connection string");
            var topic = GetEnvironmentOrThrow(KAFKA_TOPIC, "a Kafka topic");
            var metricEndpoint = GetEnvironmentOrThrow(METRIC_ENDPOINT, "a metric endpoint URL");
            var metricMethod = GetEnvironmentOrThrow(METRIC_METHOD, "a HTTP method").ToUpper();
            var metricInterval = int.Parse(GetEnvironmentOrThrow(METRIC_INTERVAL, "an interval in ms"));
            var metricHeader = GetEnvironmentOrThrow(METRIC_HEADERS, "HTTP headers in JSON format");
            
            var producerConfig = new ProducerConfig {BootstrapServers = server};
            var producer = new ProducerBuilder<Null, string>(producerConfig).Build();
            
            var client = new HttpClient();
            
            //Manual switch since Enum.parse sometimes returns a default value when it really shouldn't
            var method = metricMethod switch
            {
                "GET" => HttpMethod.Get,
                "PUT" => HttpMethod.Put,
                "POST" => HttpMethod.Post,
                "DELETE" => HttpMethod.Delete,
                "PATCH" => HttpMethod.Patch,
                "OPTIONS" => HttpMethod.Options,
                "HEAD" => HttpMethod.Head,
                _ => throw new Exception($"Invalid HTTP method {metricMethod}")
            };

            #endregion

            #region Data load

            while (true)
            {
                var request = new HttpRequestMessage
                {
                    RequestUri = new Uri(metricEndpoint),
                    Method = method,
                };
            
                JsonConvert.DeserializeObject<Dictionary<string, string>>(metricHeader).ToList().ForEach(entry =>
                {
                    request.Headers.Add(entry.Key, entry.Value);
                });
                
                var response = await client.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                producer.Produce(topic, new Message<Null, string>{Value = responseBody});
                Thread.Sleep(metricInterval);
            }

            #endregion
        }
    }
}