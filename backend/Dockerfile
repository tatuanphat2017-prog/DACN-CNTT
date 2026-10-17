FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY DACN.Api/DACN.Api.csproj DACN.Api/
RUN dotnet restore DACN.Api/DACN.Api.csproj
COPY . .
RUN dotnet publish DACN.Api/DACN.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "DACN.Api.dll"]
