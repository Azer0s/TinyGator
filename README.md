# TinyGator
A tiny data aggregator for storing JSON metrics in timescaledb

## Configure the aggregator

* `KAFKA_BOOTSTRAP_SERVER` - The Kafka bootstrap server to connect to
* `KAFKA_TOPIC` - The Kafka topic to push the data to
* `METRIC_ENDPOINT` - The endpoint from which to pull the data
* `METRIC_METHOD` - The HTTP method used when accessing the endpoint
* `METRIC_INTERVAL` - The interval the endpoint is accessed in (in ms)
* `METRIC_HEADERS` - The headers to use when accessing the endpoint

### Example:

```bash
KAFKA_BOOTSTRAP_SERVER=localhost:9092
KAFKA_TOPIC=test
METRIC_ENDPOINT="http://localhost:15672/api/queues/%2F/Test?sort=message_stats.publish_details.rate&sort_reverse=true&columns=name,message_stats.publish_details.rate,message_stats.deliver_get_details.rate"
METRIC_METHOD=GET
METRIC_INTERVAL=5000
METRIC_HEADERS={"authorization":"Basic Z3Vlc3Q6Z3Vlc3Q="}
```

## Configure the collector

* `KAFKA_BOOTSTRAP_SERVER` - The Kafka bootstrap server to connect to
* `KAFKA_TOPIC` - The Kafka topic to pull the data from
* `KAFKA_GROUP_ID` - The Kafka group id for the consumer
* `JSON_POINTER_EXTRACTORS` - The JSON pointer configuration to extract data from the metrics
* `POSTGRESQL_CONNECTION_STRING` - The PostgreSQL connection string (in ADO.NET form)
* `POSTGRESQL_INSERT_STATEMENT` - The PostgreSQL insert statement
* `ADD_TIMESTAMP` - The field to add the timestamp to (leave empty if your data already contains a timestamp)

### Example

```bash
KAFKA_BOOTSTRAP_SERVER=localhost:9092
KAFKA_TOPIC=test
KAFKA_GROUP_ID=test-group
JSON_POINTER_EXTRACTORS={"foo": {"pointer": "/name", "type": "string"}}
POSTGRESQL_CONNECTION_STRING="Host=localhost\;Username=postgres\;Password=timescale"
POSTGRESQL_INSERT_STATEMENT='INSERT INTO "test"."default" ("timestamp", "value") VALUES (@timestamp, @foo)'
ADD_TIMESTAMP=timestamp
```