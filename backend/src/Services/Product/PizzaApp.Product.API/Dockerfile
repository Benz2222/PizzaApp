FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Services/Product/ src/Services/Product/
RUN dotnet restore src/Services/Product/PizzaApp.Product.API/PizzaApp.Product.API.csproj
RUN dotnet publish src/Services/Product/PizzaApp.Product.API/PizzaApp.Product.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PizzaApp.Product.API.dll"]
