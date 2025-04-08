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
