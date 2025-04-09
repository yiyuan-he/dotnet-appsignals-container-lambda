[Public Documentation] Application Signals Set Up for Lambda with ECR Container Image (.NET)

This guide focuses on how to properly integrate the OpenTelemetry Layer with AppSignals support into your containerized .NET Lambda function.

## Why This Approach is Necessary

Lambda functions deployed as container images do not support Lambda Layers in the traditional way. When using container images, you cannot simply attach the layer as you would with other Lambda deployment methods. Instead, you must manually incorporate the layer's contents into your container image during the build process.

This document outlines the necessary steps to download the `layer.zip` artifact and properly integrate it into your containerized .NET Lambda function to enable AppSignals monitoring.

## Prerequisites
- AWS CLI configured with your credentials
- Docker installed
- .NET 8 SDK
- The instructions assume you are on `x86_64` platform.

## 1. Set Up Project Structure
```console
mkdir dotnet-appsignals-container-lambda && \
cd dotnet-appsignals-container-lambda
```

## 2. Obtaining and Using the OpenTelemetry Layer with AppSignals Support

### Downloading and Integrating the Layer in Dockerfile

The most crucial step is downloading and integrating the OpenTelemetry Layer with AppSignals support directly in your Dockerfile:
```Dockerfile
FROM public.ecr.aws/lambda/dotnet:8

# Install utilities
RUN dnf install -y unzip wget dotnet-sdk-8.0 which

# Add dotnet command to docker container's PATH
ENV PATH="/usr/lib64/dotnet:${PATH}"

# Download the OpenTelemetry Layer with AppSignals Support
RUN wget https://github.com/aws-observability/aws-otel-dotnet-instrumentation/releases/latest/download/layer.zip -O /tmp/layer.zip

# Extract and include Lambda layer contents
RUN mkdir -p /opt && \
    unzip /tmp/layer.zip -d /opt/ && \
    chmod -R 755 /opt/ && \
    rm /tmp/layer.zip

WORKDIR ${LAMBDA_TASK_ROOT}

# Copy the project files
COPY dotnet-lambda-function/src/dotnet-lambda-function/*.csproj ${LAMBDA_TASK_ROOT}/
COPY dotnet-lambda-function/src/dotnet-lambda-function/Function.cs ${LAMBDA_TASK_ROOT}/
COPY dotnet-lambda-function/src/dotnet-lambda-function/aws-lambda-tools-defaults.json ${LAMBDA_TASK_ROOT}/

# Install dependencies and build the application
RUN dotnet restore

# Use specific runtime identifier and disable ReadyToRun optimization
RUN dotnet publish -c Release -o out --self-contained false /p:PublishReadyToRun=false

# Copy the published files to the Lambda runtime directory
RUN cp -r out/* ${LAMBDA_TASK_ROOT}/

CMD ["dotnet-lambda-function::dotnet_lambda_function.Function::FunctionHandler"]
```
> Note: The layer.zip file contains the OpenTelemetry instrumentation necessary for AWS AppSignals to monitor your Lambda function.

> Important: The layer extraction steps ensure that:
> 1. The layer.zip contents are properly extracted to the /opt/ directory
> 2. The otel-instrument script receives proper execution permissions
> 3. The temporary layer.zip file is removed to keep the image size smaller

## 3. Lambda Function Code

Initialize your Lambda project using the AWS Lambda .NET template:

```console
# Install the Lambda templates if you haven't already
dotnet new -i Amazon.Lambda.Templates

# Create a new Lambda project
dotnet new lambda.EmptyFunction -n dotnet-lambda-function
```

Your directory structure should look something like this:

```console
├── Dockerfile
├── dotnet-lambda-function
│   ├── src
│   │   └── dotnet-lambda-function
│   │       ├── Function.cs
│   │       ├── Readme.md
│   │       ├── aws-lambda-tools-defaults.json
│   │       └── dotnet-lambda-function.csproj
│   └── test
│       └── dotnet-lambda-function.Tests
│           ├── FunctionTest.cs
│           └── dotnet-lambda-function.Tests.csproj
└── instructions.md
```

Update the `Function.cs` code:
```c
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
```

Update the `dotnet-lambda-function.csproj` to:
```csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
    <AWSProjectType>Lambda</AWSProjectType>
    <!-- This property makes the build directory similar to a publish directory and helps the AWS .NET Lambda Mock Test Tool find project dependencies. -->
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <!-- Generate ready to run images during publishing to improve cold start time. -->
    <PublishReadyToRun>true</PublishReadyToRun>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Amazon.Lambda.Core" Version="2.5.0" />
    <PackageReference Include="Amazon.Lambda.Serialization.SystemTextJson" Version="2.4.4" />
    <PackageReference Include="AWSSDK.S3" Version="3.7.305.23" />
  </ItemGroup>
</Project>
```

## 4. Build and Deploy the Container Image

### Set up environment variables

```console
AWS_ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text) 
AWS_REGION=$(aws configure get region)

# For fish shell users:
# set AWS_ACCOUNT_ID (aws sts get-caller-identity --query Account --output text) 
# set AWS_REGION (aws configure get region)
```

### Authenticate with ECR

First with public ECR (for base image):

```console
aws ecr-public get-login-password --region us-east-1 | docker login --username AWS --password-stdin public.ecr.aws
```

Then with your private ECR:

```console
aws ecr get-login-password --region $AWS_REGION | docker login --username AWS --password-stdin $AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com
```

### Create ECR repository (if needed)

```console
aws ecr create-repository \
    --repository-name lambda-appsignals-demo \
    --region $AWS_REGION
```

### Build, tag and push your image

```console
# Build the Docker image
docker build -t lambda-appsignals-demo .

# Tag the image
docker tag lambda-appsignals-demo:latest $AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/lambda-appsignals-demo:latest

# Push the image
docker push $AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/lambda-appsignals-demo:latest
```

## 5. Create and Configure the Lambda Function

1. Go to the AWS Lambda console and create a new function
2. Select **Container image** as the deployment option
3. Select your ECR image

### Critical AppSignals Configuration

The following steps are essential for the `layer.zip` integration to work:

- Add the environment variable:
  - Key: `AWS_LAMBDA_EXEC_WRAPPER`
  - Value: `/opt/otel-instrument`
  - This environment variable tells Lambda to use the `otel-instrument` wrapper script that was extracted from the `layer.zip` file to your container's `/opt` directory.
- Attach required IAM policies:
  - **CloudWatchLambdaApplicationSignalsExecutionRolePolicy** - required for AppSignals
  - Additional policies for your function's operations (e.g., S3 access policy for our example)
- You may also need to configure your Lambda's Timeout and Memory settings under **General configuration**. In this example we use the following settings:
  - Timeout: 0 min 15 sec
  - Memory: 512 MB

## 6. Testing and Verification
1. Test your Lambda function with a simple event
2. If the layer integration is successful, your Lambda will appear in the AppSignals service map
3. You should see traces and metrics for your Lambda function in the CloudWatch console.

## Troubleshooting Layer Integration
If AppSignals isn't working:
  1. Check the function logs for any errors related to the OpenTelemetry instrumentation
  2. Verify the environment variable `AWS_LAMBDA_EXEC_WRAPPER` is set correctly
  3. Ensure the layer extraction in the Dockerfile completed successfully
  4. Confirm the IAM permissions are properly attached
  5. Increase the Timeout and Memory settings in **General configuration** if needed.
