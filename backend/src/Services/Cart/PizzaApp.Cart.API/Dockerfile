FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Services/Cart/ src/Services/Cart/
RUN dotnet restore src/Services/Cart/PizzaApp.Cart.API/PizzaApp.Cart.API.csproj
RUN dotnet publish src/Services/Cart/PizzaApp.Cart.API/PizzaApp.Cart.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PizzaApp.Cart.API.dll"]
