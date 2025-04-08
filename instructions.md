[Public Documentation] Application Signals Set Up for Lambda with ECR Container Image (.NET)

This guide focuses on how to properly integrate the OpenTelemetry Layer with AppSignals support into your containerized .NET Lambda function.

## Why This Approach is Necessary

Lambda functions deployed as container images do not support Lambda Layers in the traditional way. When using container images, you cannot simply attach the layer as you would with other Lambda deployment methods. Instead, you must manually incorporate the layer's contents into your container image during the build process.

This document outlines the necessary steps to download the `layer.zip` artifact and properly integrate it into your containerized .NET Lambda function to enable AppSignals monitoring.

```console
# Build the Docker image
docker build -t lambda-dotnet-appsignals-demo .

# Tag the image
docker tag lambda-dotnet-appsignals-demo:latest $AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/lambda-dotnet-appsignals-demo:latest

# Push the image
docker push $AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/lambda-dotnet-appsignals-demo:latest
```
