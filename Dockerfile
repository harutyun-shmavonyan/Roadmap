# Stage 1: Build frontend
FROM node:20-alpine AS frontend-build
WORKDIR /app/frontend
COPY frontend/package.json frontend/package-lock.json* ./
RUN npm install
COPY frontend/ ./
RUN npm run build

# Stage 2: Build backend
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS backend-build
WORKDIR /app/backend
COPY backend/*.csproj ./
RUN dotnet restore
COPY backend/ ./
RUN dotnet publish -c Release -o /app/publish --no-restore

# Stage 3: Runtime — minimal image
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Copy published backend
COPY --from=backend-build /app/publish ./

# Copy built frontend into wwwroot (served as static files)
COPY --from=frontend-build /app/frontend/dist ./wwwroot/

# Railway provides PORT env var
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_GCConserveMemory=9
ENV DOTNET_GCHeapHardLimit=0x8000000
ENV MALLOC_TRIM_THRESHOLD_=131072
EXPOSE 5000

ENTRYPOINT ["dotnet", "Roadmap.Api.dll"]
