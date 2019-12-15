using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Newtonsoft.Json;

// ReSharper disable InconsistentNaming

namespace MetricAggregator
{
    class Program
    {
        static async Task Main()
        {
            const string KAFKA_BOOTSTRAP_SERVER = "KAFKA_BOOTSTRAP_SERVER";
            const string KAFKA_TOPIC = "KAFKA_TOPIC";
            const string KAFKA_CONNECT_TIMEOUT = "KAFKA_CONNECT_TIMEOUT";
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
            var connectTimeout =
                int.Parse(GetEnvironmentOrThrow(KAFKA_CONNECT_TIMEOUT, "the Kafka connect timeout in ms"));
            var metricEndpoint = GetEnvironmentOrThrow(METRIC_ENDPOINT, "a metric endpoint URL");
            var metricMethod = GetEnvironmentOrThrow(METRIC_METHOD, "a HTTP method").ToUpper();
            var metricInterval = int.Parse(GetEnvironmentOrThrow(METRIC_INTERVAL, "an interval in ms"));
            var metricHeader = GetEnvironmentOrThrow(METRIC_HEADERS, "HTTP headers in JSON format");
            
            var producerConfig = new ProducerConfig {BootstrapServers = server};
            var producer = new ProducerBuilder<Null, string>(producerConfig).Build();
            
            #region Try to connect to Kafka
            
            var adminClientConfig = new AdminClientConfig {BootstrapServers = server};
            using (var adminClient = new AdminClientBuilder(adminClientConfig).Build())
            {
                if(adminClient.GetMetadata(TimeSpan.FromMilliseconds(connectTimeout)) == null)
                    throw new Exception("Couldn't establish connection with Kafka!");
            }

            #endregion

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

            var headers = JsonConvert.DeserializeObject<Dictionary<string, string>>(metricHeader).ToList();

            #endregion

            #region Data load

            while (true)
            {
                var request = new HttpRequestMessage
                {
                    RequestUri = new Uri(metricEndpoint),
                    Method = method,
                };
            
                headers.ForEach(entry =>
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