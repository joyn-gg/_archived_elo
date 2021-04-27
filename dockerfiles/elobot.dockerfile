#-- Build context --------------------------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:3.1-alpine AS build_context

# Update system
RUN apk --no-cache update \
 && apk --no-cache upgrade

# Copy project code
COPY ./src /usr/src/elo_bot

# Set working directory
WORKDIR /usr/src/elo_bot

# Install project dependencies
RUN dotnet restore ELO.csproj

# Build
RUN dotnet build ELO.csproj -c Release -o /app/elo_bot/build
RUN dotnet publish ELO.csproj -c Release -o /app/elo_bot/publish


#-- Run context ----------------------------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/runtime:3.1-alpine AS run_context

# Update system and install dependencies
RUN apk --no-cache update \
 && apk --no-cache upgrade

# Set working directory
ARG WORKING_DIRECTORY=/opt/project
WORKDIR ${WORKING_DIRECTORY}

# Copy project code
COPY --from=build_context /app/elo_bot/publish .

# Run bot
ENTRYPOINT ["dotnet", "ELO.dll"]
