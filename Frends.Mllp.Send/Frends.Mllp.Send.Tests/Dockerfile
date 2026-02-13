FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy projects using paths relative to the root (context)
COPY ["Frends.Mllp.Send/Frends.Mllp.Send.csproj", "Frends.Mllp.Send/"]
COPY ["Frends.Mllp.Send.Tests/Frends.Mllp.Send.Tests.csproj", "Frends.Mllp.Send.Tests/"]
RUN dotnet restore "Frends.Mllp.Send.Tests/Frends.Mllp.Send.Tests.csproj"

# Copy the rest of the code
COPY . .

# Trust certs inside the container
USER root
RUN cp Frends.Mllp.Send.Tests/TestData/server.crt /usr/local/share/ca-certificates/ && \
    cp Frends.Mllp.Send.Tests/TestData/client.crt /usr/local/share/ca-certificates/ && \
    update-ca-certificates

WORKDIR "/app/Frends.Mllp.Send.Tests"
CMD ["dotnet", "test", "--configuration", "Release"]