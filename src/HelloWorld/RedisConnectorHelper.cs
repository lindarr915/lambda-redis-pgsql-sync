using System;
using System.Linq;
using pgsql_client.Models;
using StackExchange.Redis;
using Newtonsoft.Json;

namespace HelloWorld
{
    public class RedisConnectorHelper
    {
        static private string cacheConnection = System.Environment.GetEnvironmentVariable("REDIS_ENDPOINT") ?? "darren-demo.lm5w0w.clustercfg.usw2.cache.amazonaws.com:6379";

        static RedisConnectorHelper()
        {
            RedisConnectorHelper._connection = new Lazy<ConnectionMultiplexer>(() =>
            {
                var connection = ConnectionMultiplexer.Connect(cacheConnection);
                return connection;
            });
        }

        private static Lazy<ConnectionMultiplexer> _connection;

        public static ConnectionMultiplexer Connection
        {
            get
            {
                return _connection.Value;
            }
        }
    }

}