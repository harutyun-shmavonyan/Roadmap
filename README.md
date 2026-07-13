# Roadmap — Goal Tracker

A tree-based goal tracker. Organize goals into categories and actionable items, visualized as a left-to-right tree.

## Stack

- **Backend**: C# / .NET 8 Minimal API + EF Core
- **Database**: PostgreSQL 16
- **Frontend**: React 18 + TypeScript + Vite

## Quick Start

### 1. Start Postgres

```bash
docker compose up -d
```

### 2. Run the backend

```bash
cd backend

# First time: install EF Core tools & create migration
dotnet tool install --global dotnet-ef
dotnet ef migrations add Init

# Run (auto-migrates + seeds demo data)
dotnet run --urls "http://localhost:5000"
```

Swagger UI: http://localhost:5000/swagger

### 3. Run the frontend

```bash
cd frontend
npm install
npm run dev
```

Open http://localhost:3000

## Usage

- **Click** a node to select it
- **Double-click** to rename inline
- **Right-click** for context menu: add child, add sibling, rename, toggle type, delete
- Gold squares (◆) are **categories** (branches)
- Blue circles (○) are **action items** (leaves) — these are the trackable, doable items

## Project Structure

```
roadmap-app/
├── backend/
│   ├── Data/             # EF Core DbContext
│   ├── Dtos/             # Request/response models
│   ├── Endpoints/        # Minimal API endpoints
│   ├── Entities/         # Domain entities
│   ├── Seeding/          # Demo data seed
│   ├── Program.cs        # App bootstrap
│   └── appsettings.json  # Config
├── frontend/
│   └── src/
│       ├── api.ts        # HTTP client
│       ├── types.ts      # TypeScript types
│       ├── App.tsx        # Roadmap picker
│       ├── RoadmapTreePage.tsx  # Tree view
│       ├── TreeNode.tsx   # Recursive node component
│       ├── AddNodeModal.tsx
│       └── styles.css     # All styles
└── docker-compose.yml     # Postgres
```

## API Endpoints

| Method   | Path                                        | Description         |
|----------|---------------------------------------------|---------------------|
| GET      | `/api/roadmaps`                              | List all roadmaps   |
| POST     | `/api/roadmaps`                              | Create roadmap      |
| DELETE   | `/api/roadmaps/{id}`                         | Delete roadmap      |
| GET      | `/api/roadmaps/{id}/tree`                    | Get full tree       |
| POST     | `/api/roadmaps/{id}/nodes`                   | Add node            |
| PUT      | `/api/roadmaps/{id}/nodes/{nodeId}`          | Update node         |
| PATCH    | `/api/roadmaps/{id}/nodes/{nodeId}/move`     | Move/reparent node  |
| DELETE   | `/api/roadmaps/{id}/nodes/{nodeId}`          | Delete node         |
