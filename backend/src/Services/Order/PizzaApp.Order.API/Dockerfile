FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Services/Order/ src/Services/Order/
RUN dotnet restore src/Services/Order/PizzaApp.Order.API/PizzaApp.Order.API.csproj
RUN dotnet publish src/Services/Order/PizzaApp.Order.API/PizzaApp.Order.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PizzaApp.Order.API.dll"]
