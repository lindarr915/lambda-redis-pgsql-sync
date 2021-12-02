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


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace HelloWorld
{


    public class Function
    {

        private static readonly HttpClient client = new HttpClient();
        private static readonly IDatabase cache = RedisConnectorHelper.Connection.GetDatabase();

        private static async Task<string> GetCallingIP()
        {
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("User-Agent", "AWS Lambda .Net Client");

            var msg = await client.GetStringAsync("http://checkip.amazonaws.com/").ConfigureAwait(continueOnCapturedContext: false);

            return msg.Replace("\n", "");
        }

        public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest apigProxyEvent, ILambdaContext context)
        {


            using (postgresContext db = new postgresContext())
            {
                // Note: This sample requires the database to be created before running.
                // Create
                Console.WriteLine("Inserting a new Product");
                db.Add(new Product
                {
                    ProductName = "iPhone 13",
                    Description = "Your new superpower.",
                    Price = 500,
                    QuantityInStock = 100
                });
                db.SaveChanges();


                // Read
                Console.WriteLine("Querying for a Product");
                var Product = db.Products
                    .OrderBy(b => b.ProductId)
                    .First();

                cache.StringSet("Product:" + Product.ProductId, JsonConvert.SerializeObject(Product));
            }

            using (postgresContext db = new postgresContext())
            {
                // Reading object from Redis cache
                var ProductFromCache = JsonConvert.DeserializeObject<Product>(cache.StringGet("Product:2"));

                Console.WriteLine(JsonConvert.SerializeObject(ProductFromCache));

                // Update
                Console.WriteLine("Updating the Product");
                ProductFromCache.Price = 2000;
                db.Update(ProductFromCache);
                db.SaveChanges();
            }
            // Delete
            // Console.WriteLine("Delete the Product");
            // db.Remove(Product);
            // db.SaveChanges();


            // var location = await GetCallingIP();
            // var body = new Dictionary<string, string>
            // {
            // { "message", "hello world" },
            // { "location", location }
            // };

            return new APIGatewayProxyResponse
            {
                Body = String.Empty,
                StatusCode = 200,
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }


    }
}
