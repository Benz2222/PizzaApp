# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy csproj and restore as distinct layers
COPY ["PizzaApp.API/PizzaApp.API.csproj", "PizzaApp.API/"]
COPY ["PizzaApp.Core/PizzaApp.Core.csproj", "PizzaApp.Core/"]
COPY ["PizzaApp.Infrastructure/PizzaApp.Infrastructure.csproj", "PizzaApp.Infrastructure/"]
RUN dotnet restore "PizzaApp.API/PizzaApp.API.csproj"

# copy everything and build
COPY . .
WORKDIR /src/PizzaApp.API
RUN dotnet publish "PizzaApp.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PizzaApp.API.dll"]