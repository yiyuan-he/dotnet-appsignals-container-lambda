using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace dotnet_lambda_function
{
    public class Function
    {
        /// <summary>
        /// Sample Lambda function that can be used in a container image.
        /// </summary>
        /// <param name="input">Input event data</param>
        /// <param name="context">Lambda runtime information</param>
        /// <returns>Response object</returns>
        public async Task<Dictionary<string, object>> FunctionHandler(object input, ILambdaContext context)
        {
            context.Logger.LogInformation($"Received event: {JsonSerializer.Serialize(input, new JsonSerializerOptions { WriteIndented = true })}");

            // Create S3 client
            var s3Client = new AmazonS3Client();

            try
            {
                // List buckets
                var response = await s3Client.ListBucketsAsync();

                // Extract bucket names
                var buckets = new List<string>();
                foreach (var bucket in response.Buckets)
                {
                    buckets.Add(bucket.BucketName);
                }

                return new Dictionary<string, object>
                {
                    { "statusCode", 200 },
                    { "body", JsonSerializer.Serialize(new
                        {
                            message = "Successfully retrieved buckets",
                            buckets = buckets
                        })
                    }
                };
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Error listing buckets: {ex.Message}");
                return new Dictionary<string, object>
                {
                    { "statusCode", 500 },
                    { "body", JsonSerializer.Serialize(new
                        {
                            message = $"Error listing buckets: {ex.Message}"
                        })
                    }
                };
            }
        }
    }
}
