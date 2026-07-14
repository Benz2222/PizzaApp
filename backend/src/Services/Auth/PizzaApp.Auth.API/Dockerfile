# Build từ thư mục backend/ làm context
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Services/Auth/ src/Services/Auth/
RUN dotnet restore src/Services/Auth/PizzaApp.Auth.API/PizzaApp.Auth.API.csproj
RUN dotnet publish src/Services/Auth/PizzaApp.Auth.API/PizzaApp.Auth.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PizzaApp.Auth.API.dll"]
