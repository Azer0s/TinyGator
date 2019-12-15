using System;
using System.Collections.Generic;
using System.Linq;
using Confluent.Kafka;
using Newtonsoft.Json;
using Npgsql;
using NpgsqlTypes;

// ReSharper disable InconsistentNaming

namespace MetricCollector
{
    class PointerData
    {
        [JsonProperty("pointer")]
        public string Pointer;
        
        [JsonProperty("type")]
        public string Type;
    }
    class Program
    {
        static void Main()
        {
            const string KAFKA_BOOTSTRAP_SERVER = "KAFKA_BOOTSTRAP_SERVER";
            const string KAFKA_TOPIC = "KAFKA_TOPIC";
            const string KAFKA_GROUP_ID = "KAFKA_GROUP_ID";
            const string JSON_POINTER_EXTRACTORS = "JSON_POINTER_EXTRACTORS";
            const string POSTGRESQL_CONNECTION_STRING = "POSTGRESQL_CONNECTION_STRING";
            const string POSTGRESQL_INSERT_STATEMENT = "POSTGRESQL_INSERT_STATEMENT";
            const string ADD_TIMESTAMP = "ADD_TIMESTAMP";
                
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
            var groupId = GetEnvironmentOrThrow(KAFKA_GROUP_ID, "a Kafka group id");
            var pointerExtractors = JsonConvert.DeserializeObject<Dictionary<string, PointerData>>(
                GetEnvironmentOrThrow(JSON_POINTER_EXTRACTORS, "JSON pointer extractors as JSON map"));
            var postgresqlConnectionString =
                GetEnvironmentOrThrow(POSTGRESQL_CONNECTION_STRING, "a PostgreSQL connection string");
            var postgresqlInsertStatement = GetEnvironmentOrThrow(POSTGRESQL_INSERT_STATEMENT, "an insert statement");

            var addTimestamp = Environment.GetEnvironmentVariable(ADD_TIMESTAMP);
            
            //The field of the insert statement in which the timestamp should be added in
            var shouldAddTimestamp = addTimestamp != null;
            
            var conn = new NpgsqlConnection(postgresqlConnectionString);
            
            var consumerConfig = new ConsumerConfig{BootstrapServers = server, GroupId = groupId};
            var consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
            
            #region Try to connect to Kafka
              
            var adminClientConfig = new AdminClientConfig {BootstrapServers = server};
            var adminClient = new AdminClientBuilder(adminClientConfig).Build();
            
            Metadata metadata = null;

            while (metadata == null)
            {
                try
                {
                    metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(1));
                }
                catch (Exception)
                {
                    adminClientConfig = new AdminClientConfig {BootstrapServers = server};
                    adminClient = new AdminClientBuilder(adminClientConfig).Build();
                }
            }
                        
            Console.WriteLine($"Connected to Kafka broker {metadata.OriginatingBrokerName}!");
            
            #endregion

            consumer.Subscribe(topic);

            #endregion

            #region Get data

            while (true)
            {
                try
                {
                    var cr = consumer.Consume();
                    var value = cr.Value;

                    conn.Open();

                    using var cmd = new NpgsqlCommand(postgresqlInsertStatement, conn);
                    
                    foreach (var entry in pointerExtractors.ToList())
                    {
                        switch (entry.Value.Type)
                        {
                            case "int":
                                cmd.Parameters.AddWithValue(entry.Key, NpgsqlDbType.Integer,
                                    JsonPointer.JsonPointer.Get<int>(value, entry.Value.Pointer));
                                break;
                            case "float":
                                cmd.Parameters.AddWithValue(entry.Key, NpgsqlDbType.Double,
                                    JsonPointer.JsonPointer.Get<double>(value, entry.Value.Pointer));
                                break;
                            case "bool":
                                cmd.Parameters.AddWithValue(entry.Key, NpgsqlDbType.Boolean,
                                    JsonPointer.JsonPointer.Get<bool>(value, entry.Value.Pointer));
                                break;
                            case "string":
                                cmd.Parameters.AddWithValue(entry.Key, NpgsqlDbType.Varchar,
                                    JsonPointer.JsonPointer.Get<string>(value, entry.Value.Pointer));
                                break;
                            default:
                                throw new Exception($"Unrecognized datatype: {entry.Value.Type}");
                        }
                    }

                    if (shouldAddTimestamp)
                    {
                        cmd.Parameters.AddWithValue(addTimestamp, NpgsqlDbType.Timestamp, DateTime.UtcNow);
                    }

                    cmd.ExecuteNonQuery();
                    conn.Close();
                }
                catch (ConsumeException e)
                {
                    Console.WriteLine($"Error occured: {e.Error.Reason}");
                }
            }

            #endregion

        }
    }
}