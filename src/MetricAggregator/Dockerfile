FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /app

# copy csproj and restore as distinct layers
COPY . .
RUN dotnet restore

# copy and publish app and libraries
RUN dotnet publish -c Release -o out


FROM mcr.microsoft.com/dotnet/core/runtime:3.1 AS runtime
WORKDIR /app
COPY --from=build /app/out ./

ENV KAFKA_BOOTSTRAP_SERVER=""
ENV KAFKA_TOPIC=""
ENV METRIC_ENDPOINT=""
ENV METRIC_METHOD=""
ENV METRIC_INTERVAL=""
ENV METRIC_HEADERS=""

ENTRYPOINT ["dotnet", "MetricAggregator.dll"]