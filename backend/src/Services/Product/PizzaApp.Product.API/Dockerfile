FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Services/Product/ src/Services/Product/
RUN dotnet restore src/Services/Product/PizzaApp.Product.API/PizzaApp.Product.API.csproj
RUN dotnet publish src/Services/Product/PizzaApp.Product.API/PizzaApp.Product.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# wwwroot phai ton tai TRUOC khi app chay: khong co no thi WebRootPath = null
# -> UseStaticFiles() khong phuc vu /uploads/* (moi anh 404).
RUN mkdir -p /app/wwwroot/uploads
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PizzaApp.Product.API.dll"]
