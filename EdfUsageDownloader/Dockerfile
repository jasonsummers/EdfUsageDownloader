FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["EdfUsageDownloader/EdfUsageDownloader.csproj", "EdfUsageDownloader/"]
RUN dotnet restore "EdfUsageDownloader/EdfUsageDownloader.csproj"
COPY . .
WORKDIR "/src/EdfUsageDownloader"
RUN dotnet build "EdfUsageDownloader.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "EdfUsageDownloader.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=build /app/build/playwright.ps1 .

# Install Playwright dependencies
RUN apt-get update && apt-get install -y wget

# Download the Microsoft repository GPG keys
#RUN apt-get update && apt-get install -y wget
RUN wget https://packages.microsoft.com/config/debian/10/packages-microsoft-prod.deb && \
      dpkg -i packages-microsoft-prod.deb

# Register the Microsoft repository GPG keys
#RUN dpkg -i packages-microsoft-prod.deb

# Update the list of products
RUN apt-get update && apt-get install -y powershell

# Install PowerShell
#RUN apt-get install -y powershell

RUN pwsh playwright.ps1 install
RUN pwsh playwright.ps1 install-deps
ENTRYPOINT ["dotnet", "EdfUsageDownloader.dll"]
