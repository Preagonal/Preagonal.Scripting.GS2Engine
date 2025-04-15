FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["GS2Engine.TestApp/GS2Engine.TestApp.csproj", "GS2Engine.TestApp/"]
RUN dotnet restore "GS2Engine.TestApp/GS2Engine.TestApp.csproj"
COPY . .
WORKDIR "/src/GS2Engine.TestApp"
RUN dotnet build "TestApp.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TestApp.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TestApp.dll"]
