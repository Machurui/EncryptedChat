# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY EncryptedChat.Api/EncryptedChat.csproj EncryptedChat.Api/
RUN dotnet restore EncryptedChat.Api/EncryptedChat.csproj
COPY EncryptedChat.Api/ EncryptedChat.Api/
RUN dotnet publish EncryptedChat.Api/EncryptedChat.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "EncryptedChat.dll"]
