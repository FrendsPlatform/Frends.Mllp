FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Kopiujemy oba projekty, używając ścieżek relatywnych do folderu solucji
COPY ["Frends.Mllp.Send/Frends.Mllp.Send.csproj", "Frends.Mllp.Send/"]
COPY ["Frends.Mllp.Send.Tests/Frends.Mllp.Send.Tests.csproj", "Frends.Mllp.Send.Tests/"]
RUN dotnet restore "Frends.Mllp.Send.Tests/Frends.Mllp.Send.Tests.csproj"

# Kopiujemy całą resztę kodu solucji
COPY . .

# Instalacja certyfikatów w systemie Linux (magazyn zaufania)
USER root
COPY Frends.Mllp.Send.Tests/TestData/server.crt /usr/local/share/ca-certificates/
COPY Frends.Mllp.Send.Tests/TestData/client.crt /usr/local/share/ca-certificates/
RUN update-ca-certificates

# Uruchomienie testów
WORKDIR "/app/Frends.Mllp.Send.Tests"
ENTRYPOINT ["dotnet", "test", "--configuration", "Release"]