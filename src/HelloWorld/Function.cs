using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

using System;
using System.Linq;
using pgsql_client.Models;
using StackExchange.Redis;
using Newtonsoft.Json;

using OpenTelemetry.Trace;
using OpenTelemetry;
using Amazon.S3;

using OpenTelemetry.Contrib.Instrumentation.AWSLambda.Implementation;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace HelloWorld
{

    public class Function
    {

        private static readonly HttpClient client = new HttpClient();
        private static IDatabase cache;
        private postgresContext db;

        private static async Task<string> GetCallingIP()
        {
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("User-Agent", "AWS Lambda .Net Client");

            var msg = await client.GetStringAsync("http://checkip.amazonaws.com/").ConfigureAwait(continueOnCapturedContext: false);

            return msg.Replace("\n", "");
        }

        public Function()
        {
            db = new postgresContext();
            cache = RedisConnectorHelper.Connection.GetDatabase();
        }

        TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
         // add other instrumentations
         .AddXRayTraceId()
         .AddOtlpExporter()
         .AddAWSInstrumentation()
         .AddAWSLambdaConfigurations()
         .AddEntityFrameworkCoreInstrumentation()
         .AddRedisInstrumentation(RedisConnectorHelper.Connection, options => options.SetVerboseDatabaseStatements = true)
         .Build();

        // new Lambda function handler passed in
        public async Task<APIGatewayProxyResponse> TracingFunctionHandler(APIGatewayProxyRequest input, ILambdaContext context)
        =>  await AWSLambdaWrapper.Trace(tracerProvider, FunctionHandler, input, context);

        // TODO: Get the popular product ID from external source
        static HashSet<int> firstRecordsToSyncIds = new HashSet<int>(){
            6,7,8,9,10
        };

        static HashSet<int> secondRecordsToSyncIds = new HashSet<int>(){
            6,7,8,9,10

        };
        async public Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest apigProxyEvent, ILambdaContext context)
        {
            {

                // Note: This sample requires the database to be created before running.
                // Create
                /*
                Console.WriteLine("Inserting a new Product");
                db.Add(new Product
                {
                    ProductName = "iPhone 13",
                    Description = "Your new superpower.",
                    Price = 500,
                    QuantityInStock = 100
                });
                await db.SaveChangesAsync();
                */

                // Read record from database and save to cache
                Console.WriteLine("Querying for a Product");
                var items = db.Products.Where(b => firstRecordsToSyncIds.Contains(b.ProductId));
                var WriteToRedisTasks = new List<Task<bool>>();

                foreach (var item in items)
                {
                    WriteToRedisTasks.Add( 
                        cache.StringSetAsync("Product:" + item.ProductId, JsonConvert.SerializeObject(item))
                    );
                    Console.WriteLine(String.Format("Write Product:{0} to Redis", item.ProductId));
                }
                await Task.WhenAll(WriteToRedisTasks);    

                db.ChangeTracker.Clear();

            }

            {
                // Reading object from Redis cache
                foreach (var record in secondRecordsToSyncIds)
                {
                    var ProductFromCache = JsonConvert.DeserializeObject<Product>(
                        await cache.StringGetAsync(String.Format("Product:{0}",record.ToString())));
                    // Console.WriteLine(JsonConvert.SerializeObject(ProductFromCache));
                    Console.WriteLine(String.Format("Write Product:{0} to Database", ProductFromCache.ProductId));

                    db.Update(ProductFromCache);
                }
                await db.SaveChangesAsync();

                // Update
                Console.WriteLine("Hello World");
            }

            // Delete
            // Console.WriteLine("Delete the Product");
            // db.Remove(Product);
            // db.SaveChanges();


            return new APIGatewayProxyResponse
            {
                Body = String.Empty,
                StatusCode = 200,
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }


    }
}
