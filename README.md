# ADO Tools — Azure DevOps Productivity Suite

A **WinUI 3** desktop application for Windows that streamlines common Azure DevOps workflows — browsing and downloading work items, searching backlogs with AI-powered semantic search, and downloading/installing software builds — all from a single, modern interface.

![.NET 8](https://img.shields.io/badge/.NET-8.0-blue)
![WinUI 3](https://img.shields.io/badge/WinUI-3-blueviolet)
![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-lightgrey)

---

## Table of Contents

- [Overview](#overview)
- [Features](#features)
  - [Work Items Tab](#work-items-tab)
  - [Software Downloads Tab](#software-downloads-tab)
  - [Settings Tab](#settings-tab)
- [Architecture](#architecture)
  - [Project Structure](#project-structure)
  - [Key Services](#key-services)
  - [Models](#models)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Configuration](#configuration)
  - [Building](#building)
- [Search System](#search-system)
  - [Semantic Search (AI-Powered)](#semantic-search-ai-powered)
  - [BM25 Keyword Search](#bm25-keyword-search)
  - [Hybrid Search](#hybrid-search)
  - [Find Similar](#find-similar)
  - [Domain Glossary](#domain-glossary)
- [Caching Strategy](#caching-strategy)
- [Settings Reference](#settings-reference)

---

## Overview

**ADO Tools** connects to Azure DevOps via the REST API using a Personal Access Token (PAT). It provides three main tabs:

| Tab | Purpose |
|-----|---------|
| **Work Items** | Browse queries, search backlogs (semantic + keyword), download attachments, find similar work items |
| **Software Downloads** | List Azure DevOps builds, download artifacts, extract, install/uninstall software |
| **Settings** | Configure connection (organization, project, PAT), folders, and build/manage the search index |

The application persists all user settings, column layouts, window size, and search history to a local JSON file, so everything is restored between sessions.

---

## Features

### Work Items Tab

The Work Items tab is the primary workspace for interacting with Azure DevOps work items.

#### Connection & Query Browsing
- **Auto-connect on startup**: If organization, project, and PAT are configured, the app automatically connects and loads the query tree.
- **Query tree**: Displays Azure DevOps queries in a hierarchical tree with **Favorites** (My Favorites and Team Favorites) expanded by default and **All Queries** collapsed.
- **Query persistence**: The last selected query is saved and automatically restored on the next session.
- **Collapsible tree**: Double-clicking a query collapses the tree into a compact label showing the selected query name. Click the label to expand it again.

#### Executing Queries
- **Incremental fetching**: Queries use a smart caching strategy — only new or changed work items are fetched from the API. The system:
  1. Executes the WIQL query to get work item IDs and column definitions.
  2. Fetches lightweight `ChangedDate` values from the API.
  3. Compares against the local query cache to determine which items need a full re-fetch.
  4. Fetches only new/changed items and merges them into the cache.
- **Progress reporting**: Both the "checking" and "fetching" phases show real-time progress (e.g., `Checking 50/200 items…`, `Fetching 12/30 items…`).

#### Dynamic Columns
- Columns are **dynamically generated** based on the fields returned by the query or the search index.
- **Column picker flyout**: Lets users show/hide any available field column.
- **Resizable columns**: Widths are persisted per mode (query vs. search) and restored between sessions.
- **Reset columns**: A button restores the default column layout.
- **Friendly display names**: ADO field reference names (e.g., `System.AssignedTo`) are mapped to human-readable headers (e.g., "Assigned To").

#### Row Filtering
- **Combo-box filters** for Type, State, Area Path, Priority, and Assigned To.
- Filters are populated dynamically from the loaded data.
- A **filter badge** shows how many filters are active.
- **Clear Filters** button resets all filters at once.

#### Row Highlighting
- Configurable **highlight days**: Rows with a `CreatedDate` within the specified number of days are highlighted in blue.

#### Sorting
- Click any column header to sort ascending/descending. The sort indicator is shown on the active column.

#### Downloading Attachments
- **Download Selected**: Downloads attachments for all selected work items. Creates a folder per work item and saves an HTML link file alongside attachments.
- **Download by ID**: Enter a specific work item ID and download its attachments directly.
- **Download folder link**: After downloading, a clickable link opens the download folder in Explorer.
- **Error resilience**: Individual attachment failures are counted and reported without aborting the entire download.

#### Search (Query-Level)
- After loading a query, a **BM25 keyword search** index is built from the cached query results.
- The search box provides **auto-suggest** with:
  - Recent search history (prefixed with ??).
  - Vocabulary completions from the BM25 index.

#### Search (Backlog-Level)
- Uses the **semantic search index** (built from the Settings tab) to search across the entire backlog.
- Three search modes:
  - **Hybrid** (default): Combines semantic and BM25 results using Reciprocal Rank Fusion.
  - **Semantic only**: Pure embedding-based similarity search.
  - **Keyword only**: Pure BM25 keyword search.
- Results show score percentages in the title column.

#### Find Similar
- Enter a work item ID (or select one from the grid) and click **Find Similar**.
- Uses a 3-tier lookup for the source item: current list ? embedding cache ? API.
- Runs **semantic similarity** and **BM25 keyword search** in parallel, then fuses results.
- The source item is displayed at the top with a `[Source]` tag and orange highlight.
- An **Exclude Done** checkbox filters out completed/closed items from results.

#### Context Badge
- A colored badge at the top of the grid indicates the current data source:
  - ?? **Query Results** — data from an ADO query.
  - ?? **Query Search** — keyword search within query results.
  - ?? **Backlog Search** — semantic/keyword search of the full backlog.
  - ?? **Compare Results** — "Find Similar" results.

---

### Software Downloads Tab

The Software Downloads tab manages downloading and installing software builds from Azure DevOps pipelines.

#### Product Definitions
- **Pre-configured products**: Ships with default definitions for OpenRail Designer, OpenRoads Designer, Overhead Line Designer, OpenBridge Designer, and MicroStation.
- **Custom products**: Users can add, edit, or remove product definitions (name, pipeline definition ID, and project).
- **Product combo box**: Switching products automatically loads the corresponding definition ID and project.

#### Loading Builds
- Queries Azure DevOps for available builds matching the selected pipeline definition.
- Configurable **build count** (how many recent builds to fetch).
- Builds are displayed in a list with **version highlighting** — the latest major version is visually distinguished.
- **Build filter**: A text box filters the build list by version string.

#### Update Workflow
The main **Update** button performs a multi-step workflow:

1. **Download** build artifacts (ZIP files) from Azure DevOps.
   - Supports **cancellation** via a Stop button.
   - Shows **download speed** and **progress** (MB downloaded, total size, MB/s).
   - **Smart re-download**: If a ZIP already exists locally with a matching size and valid structure, the user is asked whether to re-download.
   - **System sleep prevention**: Keeps the machine awake during downloads using `SetThreadExecutionState`.
2. **Extract** ZIP contents to a subfolder.
3. **Uninstall** the currently installed version (optional):
   - Detects installed Bentley software via Windows Registry.
   - Matches by product name and major version.
   - Supports **Clean Uninstall** (removes leftover files/folders after MSI uninstall).
4. **Install** the new version by running the extracted setup executable in quiet mode.

#### Download Only Mode
A toggle switch skips the uninstall/install steps and only downloads + extracts the artifacts.

#### Installed Software Management
- **Show Bentley Software** button opens a dialog listing all Bentley products installed on the machine (queried from the Windows Registry).
- Users can select an installed product and **uninstall** it directly from the dialog, with optional clean uninstall.

#### Logging
- All operations are logged to a scrollable **log panel** at the bottom of the page.
- Progress-style messages (e.g., download percentages) update the last log entry in-place to avoid log flooding.
- A **Clear Log** button resets the log. A log entry count badge is shown.

#### Installer Monitoring
- An **info bar** appears while Windows Installer (msiexec) is running, showing elapsed time.
- The app polls every 5 seconds to detect when the installer finishes.

---

### Settings Tab

#### Connection Settings
| Setting | Description |
|---------|-------------|
| **Organization** | Azure DevOps organization name (e.g., `bentleycs`) |
| **Project** | Default project name (e.g., `civil`) |
| **Personal Access Token** | PAT for authentication |
| **Validate** | Tests the connection and reports success/failure with specific error messages |

#### Folder Settings
| Setting | Description |
|---------|-------------|
| **Root Folder** | Default folder for work item attachment downloads |
| **Download Folder** | Folder for software build artifact downloads |

Both support a **Browse** button that opens a folder picker dialog.

#### Search Index Settings
| Setting | Description |
|---------|-------------|
| **Search Area Path** | Limits the backlog search index to a specific area path (e.g., `Civil\OpenCivil Designer`) |
| **Cutoff Date** | Only indexes work items created after this date |
| **Update Index** | Incrementally updates the semantic search index (fetches new items, embeds them) |
| **Force Rebuild** | Deletes the existing cache and rebuilds from scratch (requires two-click confirmation with 5-second timeout) |

#### Cache Status
- Shows the current cache state (e.g., `Current cache: 5,432 items indexed.`).
- Displays progress during index building (e.g., `Embedding 150/500…`).

#### About
- Displays the application version (from MSIX package or assembly metadata).

---

## Architecture

### Project Structure

```
src/ADO_Tools_WinUI/
??? Assets/
?   ??? Models/
?   ?   ??? model.onnx          # Sentence embedding model (MiniLM)
?   ?   ??? vocab.txt           # Tokenizer vocabulary
?   ??? ADOToolsSquare256Pixel.ico
??? Models/
?   ??? AttachmentDto.cs         # Work item attachment data
?   ??? BuildInfo.cs             # Azure DevOps build metadata
?   ??? EmbeddingCacheEntry.cs   # Cached work item embedding + metadata
?   ??? QueryDto.cs              # ADO query tree node
?   ??? QueryExecutionResult.cs  # WIQL execution result (IDs + columns)
?   ??? WiqlQueryResult.cs       # Full query result with work items
?   ??? WorkItemDto.cs           # Work item data transfer object
??? Pages/
?   ??? BuildInfoViewModel.cs    # View model for build list items
?   ??? LogEntryViewModel.cs     # View model for log entries
?   ??? SettingsPage.xaml(.cs)   # Settings UI and logic
?   ??? SoftwareDownloadPage.xaml(.cs) # Build download/install UI
?   ??? WorkItemsPage.xaml(.cs)  # Work items UI and search
??? Services/
?   ??? AppSettings.cs           # JSON-based settings persistence
?   ??? Bm25SearchService.cs     # BM25 keyword search engine
?   ??? BuildDownloadService.cs  # Build artifact download orchestration
?   ??? EmbeddingCache.cs        # Embedding vector + metadata cache
?   ??? InstallFunctions.cs      # Software install/uninstall operations
?   ??? IStatusReporter.cs       # Status reporting interface
?   ??? LocalEmbeddingService.cs # ONNX-based sentence embedding inference
?   ??? QuerySearchCache.cs      # Per-query work item cache
?   ??? SemanticSearchService.cs # Semantic search orchestration
?   ??? TfsRestClient.cs         # Azure DevOps REST API client
?   ??? VersionParser.cs         # Build version string parser
??? Utilities/
?   ??? ErrorHandling.cs         # Error handling utilities
??? App.xaml(.cs)                # Application entry point
??? MainWindow.xaml(.cs)         # Main window with TabView
??? ADO_Tools_WinUI.csproj       # Project file
```

### Key Services

| Service | Responsibility |
|---------|---------------|
| `TfsRestClient` | All Azure DevOps REST API communication: queries, work items, builds, artifacts, attachments |
| `SemanticSearchService` | Orchestrates semantic search: builds/loads embedding cache, generates embeddings, computes cosine similarity, applies domain glossary expansion |
| `Bm25SearchService` | BM25 (Okapi BM25) keyword search with TF-IDF weighting, stop word removal, and vocabulary auto-suggest |
| `LocalEmbeddingService` | Runs the ONNX sentence embedding model (MiniLM) locally using ONNX Runtime with DirectML acceleration |
| `EmbeddingCache` | Persists embedding vectors and work item metadata to disk as JSON for fast offline access |
| `QuerySearchCache` | Caches work item data per query with change-detection (only re-fetches items whose `ChangedDate` has been updated) |
| `BuildDownloadService` | Orchestrates build artifact downloading with progress reporting, cancellation, size validation, and ZIP extraction |
| `InstallFunctions` | Handles MSI-based software installation/uninstallation, registry queries for installed software, clean uninstall with file cleanup, and installer monitoring |
| `AppSettings` | Singleton settings manager — serializes/deserializes all app preferences to `%LocalAppData%/ADO_Tools_WinUI/settings.json` |

### Models

| Model | Purpose |
|-------|---------|
| `WorkItemDto` | Represents an Azure DevOps work item with fields, attachments, and metadata |
| `QueryDto` | Tree node for the query hierarchy (supports folders, favorites, WIQL queries) |
| `BuildInfo` | Build metadata from Azure DevOps (ID, version, status, product name, dates) |
| `EmbeddingCacheEntry` | Cached work item with embedding vector, searchable text, and all indexed fields |
| `AttachmentDto` | Work item attachment metadata (name, URL, size) |

---

## Getting Started

### Prerequisites

- **Windows 10** (version 1903 / build 18362) or later
- **.NET 8.0 SDK**
- **Windows App SDK 1.8+**
- **Visual Studio 2022** (17.8+) with the **.NET Desktop Development** and **Windows App SDK** workloads
- An **Azure DevOps** account with a Personal Access Token (PAT)

#### Embedding Model (Required for Semantic Search)

The semantic search feature requires a sentence embedding model:

1. Download a MiniLM-compatible ONNX model (e.g., `all-MiniLM-L6-v2`).
2. Place `model.onnx` and `vocab.txt` in:
   ```
   src/ADO_Tools_WinUI/Assets/Models/
   ```

These files are configured to be copied to the output directory on build.

### Configuration

1. Launch the application and navigate to the **Settings** tab.
2. Enter your Azure DevOps **Organization** name.
3. Enter the default **Project** name.
4. Enter your **Personal Access Token** (PAT).
5. Click **Validate** to test the connection.
6. Set the **Root Folder** (for work item attachment downloads) and **Download Folder** (for build artifacts).
7. (Optional) Configure the **Search Area Path** and **Cutoff Date**, then click **Update Index** to build the semantic search index.

### Building

```bash
# Clone the repository
git clone https://github.com/kivanck/ADO_Tools_WinUI.git
cd ADO_Tools_WinUI

# Restore packages and build
dotnet restore src\ADO_Tools_WinUI\ADO_Tools_WinUI.csproj
dotnet build src\ADO_Tools_WinUI\ADO_Tools_WinUI.csproj -c Debug -p:Platform=x64
```

Or open `ADO_Tools_WinUI.sln` in Visual Studio and build from there.

---

## Search System

ADO Tools implements a multi-tier search system that combines AI-powered semantic understanding with traditional keyword matching.

### Semantic Search (AI-Powered)

- Uses a **MiniLM sentence embedding model** (ONNX format) running locally via ONNX Runtime with **DirectML** GPU acceleration.
- Work items are converted to searchable text (title + description + fields), then embedded into dense vectors.
- At search time, the query is embedded and compared against all cached vectors using **cosine similarity**.
- **Domain glossary expansion**: Before embedding, domain-specific terms (e.g., "cant", "PGL", "corridor") are expanded with descriptive phrases so the general-purpose model understands engineering terminology.

### BM25 Keyword Search

- Implements the **Okapi BM25** ranking algorithm with:
  - **TF-IDF weighting**: Common terms are automatically down-weighted.
  - **Stop word removal**: Common English words are filtered out.
  - **Configurable parameters**: k1=1.2, b=0.75 (standard BM25 tuning).
- The BM25 index supports **vocabulary auto-suggest** in the search box.

### Hybrid Search

The default search mode combines both engines:

1. Semantic search and BM25 search run **in parallel** on background threads.
2. Results are merged using **Reciprocal Rank Fusion (RRF)**, which combines the ranking positions from both systems into a unified score.
3. This approach captures both:
   - **Meaning-based matches** (semantic) — e.g., "crash when opening large files" finds items about application errors.
   - **Exact keyword matches** (BM25) — e.g., "cant points" finds items with those specific terms.

### Find Similar

The "Find Similar" feature finds work items similar to a given source item:

1. Looks up the source item (from current list, embedding cache, or API).
2. Runs semantic similarity and BM25 search in parallel.
3. Fuses results with RRF.
4. Displays the source item at the top with results ranked by similarity score.

### Domain Glossary

The semantic search includes an extensive **domain glossary** mapping civil engineering and transportation design terms to descriptive expansions. This helps the general-purpose embedding model understand domain-specific terminology such as:

- **Alignment & Geometry**: alignment, horizontal, vertical, PGL, profile, spiral, stationing, chainage
- **Corridor Modeling**: corridor, template, component, end condition, daylight, cut/fill
- **Superelevation & Cant**: cant, superelevation, cross slope, crown, runoff, rollover
- **Terrain & Surface**: terrain, DTM, TIN, contour, breakline, grading, earthwork
- **Drainage & Utilities**: and more...

---

## Caching Strategy

ADO Tools uses multiple caching layers to minimize API calls and enable offline functionality:

| Cache | Location | Purpose |
|-------|----------|---------|
| **Embedding Cache** | `%LocalAppData%/ADO_Tools_WinUI/EmbeddingCache/` | Stores embedding vectors + metadata for all indexed work items. Enables fully offline semantic search. |
| **Query Cache** | `%LocalAppData%/ADO_Tools_WinUI/QueryCache/` | Per-query work item cache. Uses `ChangedDate` comparison to only re-fetch modified items. |
| **App Settings** | `%LocalAppData%/ADO_Tools_WinUI/settings.json` | All user preferences, column layouts, window size, search history. |

---

## Settings Reference

| Setting | Default | Description |
|---------|---------|-------------|
| `Organization` | `bentleycs` | Azure DevOps organization |
| `Project` | `civil` | Default project |
| `WorkItemProject` | `civil` | Project used on the Work Items tab (can differ from default) |
| `PersonalAccessToken` | *(empty)* | Azure DevOps PAT |
| `RootFolder` | *(empty)* | Work item attachment download folder |
| `DownloadFolder` | *(empty)* | Build artifact download folder |
| `SearchAreaPath` | `Civil\OpenCivil Designer` | Area path filter for the search index |
| `SearchCutoffDate` | `2020-01-01` | Only index work items created after this date |
| `BuildCount` | `30` | Number of recent builds to fetch |
| `ExcludeDone` | `true` | Exclude completed/closed items from search results |
| `WindowWidth` | `1280` | Persisted window width (logical pixels) |
| `WindowHeight` | `970` | Persisted window height (logical pixels) |

---

## Technology Stack

| Component | Technology |
|-----------|------------|
| **UI Framework** | WinUI 3 (Windows App SDK 1.8) |
| **Runtime** | .NET 8.0 |
| **AI/ML** | ONNX Runtime + DirectML (MiniLM sentence embeddings) |
| **Tokenization** | Microsoft.ML.Tokenizers |
| **Data Grid** | CommunityToolkit.WinUI.UI.Controls.DataGrid |
| **JSON** | Newtonsoft.Json + System.Text.Json |
| **API** | Azure DevOps REST API v7.1 |
| **Packaging** | MSIX |

---

## License

See the repository for license information.
