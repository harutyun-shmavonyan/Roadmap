using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Data;
using Roadmap.Api.Dtos;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Endpoints;

public static class RoadmapEndpoints
{
    public static void MapRoadmapEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/roadmaps").WithTags("Roadmaps").RequireAuthorization();

        // ===== English vocabulary (global; read-only + delete — words are added and
        // reviewed through the MCP tools, not the UI) =====
        var vocab = app.MapGroup("/api/vocab").WithTags("Vocab").RequireAuthorization();

        vocab.MapGet("/", async (RoadmapDbContext db) =>
        {
            var list = await db.VocabEntries.AsNoTracking()
                .Include(v => v.Reviews)
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();
            return Results.Ok(list.Select(v => VocabStore.ToDto(v, includeReviews: true)));
        });

        vocab.MapGet("/stats", async (RoadmapDbContext db) => Results.Ok(await VocabStore.StatsAsync(db)));

        vocab.MapDelete("/{id:guid}", async (Guid id, RoadmapDbContext db) =>
        {
            var v = await db.VocabEntries.FindAsync(id);
            if (v is null) return Results.NotFound();
            db.VocabEntries.Remove(v);   // reviews cascade
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ===== Daily Notes (global 'red' / 'green' books) =====
        var notes = app.MapGroup("/api/notes").WithTags("Notes").RequireAuthorization();
        notes.MapGet("/{book}", async (string book, RoadmapDbContext db) =>
        {
            var bk = book.Trim().ToLowerInvariant();
            if (bk is not ("red" or "green")) return Results.BadRequest("Invalid book. Use 'red' or 'green'.");
            var list = await db.Notes.AsNoTracking().Where(n => n.Book == bk)
                .OrderByDescending(n => n.DayNumber).ToListAsync();
            return Results.Ok(list.Select(n =>
                new NoteDto(n.Book, n.DayNumber, n.EntryDate.ToString("yyyy-MM-dd"), n.Content, n.CreatedAt, n.UpdatedAt)));
        });

        // Replace (or create) the content for one day. Keeps a single row per (book, date).
        notes.MapPut("/{book}/{dayNumber:int}", async (string book, int dayNumber, UpdateNoteRequest req, RoadmapDbContext db) =>
        {
            var bk = book.Trim().ToLowerInvariant();
            if (bk is not ("red" or "green")) return Results.BadRequest("Invalid book. Use 'red' or 'green'.");
            var note = await db.Notes.FirstOrDefaultAsync(n => n.Book == bk && n.DayNumber == dayNumber);
            if (note is null)
            {
                if (req.EntryDate is null || !DateOnly.TryParse(req.EntryDate, out var ed))
                    return Results.NotFound("No entry for that day; provide entryDate (YYYY-MM-DD) to create it.");
                note = new Note { Id = Guid.NewGuid(), Book = bk, DayNumber = dayNumber, EntryDate = ed, Content = req.Content ?? "" };
                db.Notes.Add(note);
            }
            else
            {
                note.Content = req.Content ?? "";
                note.UpdatedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();
            return Results.Ok(new NoteDto(note.Book, note.DayNumber, note.EntryDate.ToString("yyyy-MM-dd"), note.Content, note.CreatedAt, note.UpdatedAt));
        });

        // ===== Articles (global Markdown reading library) =====
        var articles = app.MapGroup("/api/articles").WithTags("Articles").RequireAuthorization();

        // Reusable projection of an article's images without their (potentially large) bytes.
        static Task<List<ArticleImageDto>> LoadImageDtos(RoadmapDbContext db, Guid articleId) =>
            db.ArticleImages.AsNoTracking().Where(i => i.ArticleId == articleId)
                .OrderBy(i => i.SortOrder).ThenBy(i => i.Name)
                .Select(i => new ArticleImageDto(i.Name, i.ContentType, i.SortOrder)).ToListAsync();

        // Normalise the requested body format to a known value; anything unknown falls back to markdown.
        static string NormFormat(string? f) =>
            string.Equals(f?.Trim(), "html", StringComparison.OrdinalIgnoreCase) ? "html" : "markdown";

        // List returns summaries (no body) — the reader fetches full content per article.
        // A correlated Count subquery gives each article's image count without loading any bytes.
        articles.MapGet("/", async (RoadmapDbContext db) =>
        {
            var rows = await db.Articles.AsNoTracking()
                .OrderBy(a => a.SortOrder).ThenByDescending(a => a.CreatedAt)
                .Select(a => new { A = a, C = a.Images.Count })
                .ToListAsync();
            return Results.Ok(rows.Select(r => ArticleLogic.ToSummary(r.A, r.C)));
        });

        articles.MapGet("/{id:guid}", async (Guid id, RoadmapDbContext db) =>
        {
            var a = await db.Articles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return a is null ? Results.NotFound() : Results.Ok(ArticleLogic.ToDetail(a, await LoadImageDtos(db, id)));
        });

        // Self-contained HTML document for an article (images inlined as data URIs). The reader
        // renders this in a sandboxed iframe and the "open in new tab" button opens it as a blob —
        // both stay behind the app's auth (this route is in the authorized group).
        articles.MapGet("/{id:guid}/html", async (Guid id, RoadmapDbContext db) =>
        {
            var a = await db.Articles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();
            var images = await db.ArticleImages.AsNoTracking().Where(i => i.ArticleId == id).ToListAsync();
            return Results.Content(ArticleLogic.BuildStandaloneHtml(a, images), "text/html; charset=utf-8");
        });

        articles.MapPost("/", async (CreateArticleRequest req, RoadmapDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Title)) return Results.BadRequest("Title is required.");
            var content = req.Content ?? "";
            var format = NormFormat(req.Format);
            var maxSort = await db.Articles.MaxAsync(a => (int?)a.SortOrder) ?? -1;
            var a = new Article
            {
                Id = Guid.NewGuid(),
                Title = req.Title.Trim(),
                Content = content,
                Format = format,
                ChatUrl = ArticleLogic.NormChatUrl(req.ChatUrl),
                ReadMinutes = req.ReadMinutes is > 0 ? req.ReadMinutes.Value : ArticleLogic.EstimateReadMinutes(content, format),
                SortOrder = maxSort + 1,
            };
            db.Articles.Add(a);
            await db.SaveChangesAsync();
            return Results.Created($"/api/articles/{a.Id}", ArticleLogic.ToDetail(a));
        });

        articles.MapPut("/{id:guid}", async (Guid id, UpdateArticleRequest req, RoadmapDbContext db) =>
        {
            var a = await db.Articles.FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();
            if (!string.IsNullOrWhiteSpace(req.Title)) a.Title = req.Title.Trim();
            if (req.Content != null) a.Content = req.Content;
            if (req.Format != null) a.Format = NormFormat(req.Format);
            if (req.ChatUrl != null) a.ChatUrl = ArticleLogic.NormChatUrl(req.ChatUrl);
            // Explicit read time wins; otherwise re-estimate when the body changed.
            if (req.ReadMinutes is > 0) a.ReadMinutes = req.ReadMinutes.Value;
            else if (req.Content != null && (req.ReadMinutes is null)) a.ReadMinutes = ArticleLogic.EstimateReadMinutes(a.Content, a.Format);
            a.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(ArticleLogic.ToDetail(a, await LoadImageDtos(db, id)));
        });

        articles.MapDelete("/{id:guid}", async (Guid id, RoadmapDbContext db) =>
        {
            var a = await db.Articles.FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();
            // Remove the linked achievement, if any, so deleting a read article cleans up.
            if (a.ReadLogId is Guid logId)
            {
                var log = await db.CustomLogs.FirstOrDefaultAsync(c => c.Id == logId);
                if (log != null) db.CustomLogs.Remove(log);
            }
            db.Articles.Remove(a);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Mark read → credits a custom achievement (3 pts per hour of reading) to a roadmap.
        articles.MapPost("/{id:guid}/read", async (Guid id, MarkArticleReadRequest? req, RoadmapDbContext db) =>
        {
            var a = await db.Articles.FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();
            if (a.IsRead) return Results.Ok(ArticleLogic.ToDetail(a)); // idempotent — no double credit

            DateOnly date = ArticleLogic.YerevanToday();
            if (!string.IsNullOrWhiteSpace(req?.Date) && !DateOnly.TryParse(req!.Date, out date))
                return Results.BadRequest("Invalid date.");

            // Credit the achievement to the requested roadmap, else the first one that exists.
            var roadmapId = req?.RoadmapId
                ?? await db.Roadmaps.OrderBy(r => r.CreatedAt).Select(r => (Guid?)r.Id).FirstOrDefaultAsync();
            if (roadmapId is Guid rid)
            {
                var points = ArticleLogic.PointsFor(a.ReadMinutes);
                var log = new CustomLog
                {
                    Id = Guid.NewGuid(),
                    RoadmapId = rid,
                    Title = $"📖 Read: {a.Title}",
                    Points = points,
                    Date = date,
                    Note = $"{a.ReadMinutes} min read · 3 pts/hr",
                };
                db.CustomLogs.Add(log);
                a.ReadLogId = log.Id;
            }
            a.IsRead = true;
            a.ReadOn = date;
            a.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(ArticleLogic.ToDetail(a));
        });

        // Mark pending again → removes the achievement created when it was read.
        articles.MapPost("/{id:guid}/unread", async (Guid id, RoadmapDbContext db) =>
        {
            var a = await db.Articles.FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();
            if (a.ReadLogId is Guid logId)
            {
                var log = await db.CustomLogs.FirstOrDefaultAsync(c => c.Id == logId);
                if (log != null) db.CustomLogs.Remove(log);
            }
            a.IsRead = false;
            a.ReadOn = null;
            a.ReadLogId = null;
            a.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(ArticleLogic.ToDetail(a));
        });

        // ===== Article images (uploaded assets referenced from HTML bodies as {{img:NAME}}) =====

        // Upload one or more images to an article via multipart/form-data. Every file field is
        // stored keyed by its sanitized filename; re-uploading the same name replaces it. Mirrors
        // the CV upload so large binaries stream from disk instead of riding a JSON payload.
        articles.MapPost("/{id:guid}/images", async (Guid id, HttpRequest request, RoadmapDbContext db) =>
        {
            if (!request.HasFormContentType) return Results.BadRequest("Expected multipart/form-data with one or more file fields.");
            var article = await db.Articles.FirstOrDefaultAsync(x => x.Id == id);
            if (article is null) return Results.NotFound();
            var form = await request.ReadFormAsync();
            if (form.Files.Count == 0) return Results.BadRequest("No files uploaded.");

            var maxSort = await db.ArticleImages.Where(i => i.ArticleId == id).MaxAsync(i => (int?)i.SortOrder) ?? -1;
            var saved = new List<object>();
            foreach (var file in form.Files)
            {
                if (file.Length == 0) continue;
                if (file.Length > 40 * 1024 * 1024) return Results.BadRequest($"Image '{file.FileName}' exceeds 40 MB.");
                var name = ArticleLogic.SanitizeImageName(file.FileName);
                if (string.IsNullOrEmpty(name)) return Results.BadRequest("An uploaded image is missing a usable filename.");
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var bytes = ms.ToArray();
                var ct = string.IsNullOrWhiteSpace(file.ContentType) || file.ContentType == "application/octet-stream"
                    ? ArticleLogic.GuessContentType(name) : file.ContentType;

                var existing = await db.ArticleImages.FirstOrDefaultAsync(i => i.ArticleId == id && i.Name == name);
                if (existing is null)
                    db.ArticleImages.Add(new ArticleImage { Id = Guid.NewGuid(), ArticleId = id, Name = name, ContentType = ct, Data = bytes, SortOrder = ++maxSort });
                else { existing.ContentType = ct; existing.Data = bytes; }
                saved.Add(new { name, contentType = ct, bytes = bytes.Length, reference = $"{{{{img:{name}}}}}" });
            }
            article.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { articleId = id, images = saved });
        }).DisableAntiforgery();

        // Raw bytes for one image — used by the editor's image manager to preview thumbnails.
        articles.MapGet("/{id:guid}/images/{name}", async (Guid id, string name, RoadmapDbContext db) =>
        {
            var img = await db.ArticleImages.AsNoTracking().FirstOrDefaultAsync(i => i.ArticleId == id && i.Name == name);
            return img is null ? Results.NotFound() : Results.File(img.Data, img.ContentType);
        });

        articles.MapDelete("/{id:guid}/images/{name}", async (Guid id, string name, RoadmapDbContext db) =>
        {
            var img = await db.ArticleImages.FirstOrDefaultAsync(i => i.ArticleId == id && i.Name == name);
            if (img is null) return Results.NotFound();
            db.ArticleImages.Remove(img);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ===== Roadmaps =====
        group.MapGet("/", async (RoadmapDbContext db) =>
            Results.Ok(await db.Roadmaps.OrderBy(r => r.CreatedAt)
                .Select(r => new RoadmapSummaryDto(r.Id, r.Name, r.Description, r.CreatedAt)).ToListAsync()));

        group.MapGet("/{roadmapId:guid}/tree", async (Guid roadmapId, RoadmapDbContext db) =>
        {
            var rm = await db.Roadmaps.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roadmapId);
            if (rm is null) return Results.NotFound();
            var nodes = await db.Nodes.AsNoTracking().Where(n => n.RoadmapId == roadmapId).OrderBy(n => n.SortOrder).ToListAsync();
            var links = await db.NodeCategoryLinks.AsNoTracking().Where(l => nodes.Select(n => n.Id).Contains(l.NodeId)).ToListAsync();
            var lk = nodes.ToDictionary(n => n.Id);
            return Results.Ok(new RoadmapTreeDto(rm.Id, rm.Name, rm.Description, BuildTree(nodes, links, lk, null)));
        });

        group.MapGet("/{roadmapId:guid}/actionables", async (Guid roadmapId, string? status, RoadmapDbContext db) =>
        {
            var nodes = await db.Nodes.AsNoTracking().Where(n => n.RoadmapId == roadmapId).OrderBy(n => n.SortOrder).ToListAsync();
            var acts = nodes.Where(n => n.IsActionable).AsEnumerable();
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<ActionItemStatus>(status, true, out var ps)) acts = acts.Where(n => n.Status == ps);
            var lk = nodes.ToDictionary(n => n.Id);
            var ids = acts.Select(n => n.Id).ToList();
            var logs = await db.WorkLogs.Where(w => w.RoadmapId == roadmapId && ids.Contains(w.NodeId))
                .GroupBy(w => w.NodeId).Select(g => new { g.Key, Total = g.Sum(w => w.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Total);
            return Results.Ok(acts.Select(n => new ActionableItemDto(n.Id, n.Title, BuildPath(n, lk), n.Status.ToString(),
                n.Unit, n.TotalSize, n.UnitsPerHour, n.PointsPerUnit, logs.GetValueOrDefault(n.Id, 0), n.ScheduleTemplate)).ToList());
        });

        // ===== Schedule (daily view) =====
        group.MapGet("/{roadmapId:guid}/schedule/{date}", async (Guid roadmapId, string date, RoadmapDbContext db) =>
        {
            if (!DateOnly.TryParse(date, out var pd)) return Results.BadRequest("Invalid date.");
            var sprint = await db.Sprints.AsNoTracking().Where(s => s.RoadmapId == roadmapId && s.StartDate <= pd && s.EndDate >= pd).FirstOrDefaultAsync();
            var sprintDto = sprint is null ? null : ToSprintDto(sprint);
            if (sprint is null)
                return Results.Ok(new { blocks = new List<ScheduleBlockDto>(), activeSprint = (SprintDto?)null });

            var dow = (int)pd.DayOfWeek;
            var relaxSet = ParseRelaxDays(sprint.RelaxDays);
            var isRelaxDay = relaxSet.Contains(pd.ToString("yyyy-MM-dd"));
            var allNodes = await db.Nodes.AsNoTracking().Where(n => n.RoadmapId == roadmapId).OrderBy(n => n.SortOrder).ToListAsync();
            var schedBlocks = await db.ScheduleBlocks.AsNoTracking().Include(sb => sb.Items).Where(sb => sb.RoadmapId == roadmapId).ToListAsync();
            var lk = allNodes.ToDictionary(n => n.Id);
            var logTotals = await db.WorkLogs.AsNoTracking().Where(w => w.RoadmapId == roadmapId)
                .GroupBy(w => w.NodeId).Select(g => new { g.Key, Total = g.Sum(w => w.Amount) })
                .ToDictionaryAsync(x => x.Key, x => x.Total);
            var workLogDatesList = await db.WorkLogs.AsNoTracking()
                .Where(w => w.RoadmapId == roadmapId)
                .Select(w => new { w.NodeId, w.Date })
                .ToListAsync();
            var workLogDates = workLogDatesList
                .GroupBy(w => w.NodeId)
                .ToDictionary(g => g.Key, g => new HashSet<DateOnly>(g.Select(w => w.Date)));
            // Date each node was completed — used so that completing a task only advances the
            // queue from the NEXT day onward, leaving the completion day and earlier untouched.
            var completionRaw = await db.StatusChanges.AsNoTracking()
                .Where(s => s.RoadmapId == roadmapId && s.NewStatus == ActionItemStatus.Completed)
                .Select(s => new { s.NodeId, s.ChangedAt })
                .ToListAsync();
            var completionDates = completionRaw
                .GroupBy(s => s.NodeId)
                .ToDictionary(g => g.Key, g => AppClock.ToLocalDate(g.Max(x => x.ChangedAt)));
            var today = AppClock.Today();

            var blocks = new List<ScheduleBlockDto>();
            var scheduledNodeIds = new HashSet<Guid>();

            if (!isRelaxDay)
            {
                ScheduleBlockDto MakeBlock(RoadmapNode item, TemplateData eff, int dayOfWeek)
                {
                    var dur = eff.GetDurationMinutes(dayOfWeek);
                    var start = eff.GetStartMinute(dayOfWeek);
                    var planned = item.UnitsPerHour.HasValue ? (dur / 60.0) * item.UnitsPerHour.Value : 0;
                    var logged = logTotals.GetValueOrDefault(item.Id, 0);
                    var totalSize = item.TotalSize ?? 0;
                    var pct = totalSize > 0 ? Math.Round(logged / totalSize * 100, 1) : 0;
                    return new ScheduleBlockDto(item.Id, item.Title, BuildPath(item, lk), item.Unit,
                        item.UnitsPerHour, planned, start, dur, logged, item.TotalSize, pct, item.PointsPerUnit, item.IsChecklist);
                }

                // Schedule block queues
                foreach (var sblock in schedBlocks)
                {
                    var blockTmpl = ParseTemplate(sblock.ScheduleTemplate);
                    if (blockTmpl is null || !blockTmpl.Days.Contains(dow)) continue;
                    // Completed items stay in the queue so past/current days keep showing them;
                    // the projection advances past them only after their completion date.
                    var queue = sblock.Items
                        .Where(n => n.IsActionable && (n.Status == ActionItemStatus.Active || n.Status == ActionItemStatus.NotStarted || n.Status == ActionItemStatus.Completed))
                        .OrderBy(n => n.BlockSortOrder).ToList();
                    if (queue.Count == 0) continue;
                    var projected = ProjectBlockQueueToDate(queue, blockTmpl, pd, sprint.StartDate, workLogDates, completionDates, today);
                    if (projected is null) continue;
                    blocks.Add(MakeBlock(projected, blockTmpl, dow));
                    scheduledNodeIds.Add(projected.Id);
                }

                // Self-scheduled items
                var blockItemIds = new HashSet<Guid>(schedBlocks.SelectMany(sb => sb.Items.Select(i => i.Id)));
                foreach (var n in allNodes.Where(n => n.IsActionable && n.ScheduleTemplate != null
                    && (n.Status == ActionItemStatus.Active || n.Status == ActionItemStatus.NotStarted)
                    && !scheduledNodeIds.Contains(n.Id) && !blockItemIds.Contains(n.Id)))
                {
                    var tmpl = ParseTemplate(n.ScheduleTemplate);
                    if (tmpl is null || !tmpl.Days.Contains(dow)) continue;
                    blocks.Add(MakeBlock(n, tmpl, dow));
                }
            }

            return Results.Ok(new { blocks, activeSprint = sprintDto, isRelaxDay });
        });

        group.MapPost("/", async (CreateRoadmapRequest req, RoadmapDbContext db) =>
        {
            var rm = new RoadmapDefinition { Id = Guid.NewGuid(), Name = req.Name, Description = req.Description };
            db.Roadmaps.Add(rm); await db.SaveChangesAsync();
            return Results.Created("", new RoadmapSummaryDto(rm.Id, rm.Name, rm.Description, rm.CreatedAt));
        });

        group.MapDelete("/{roadmapId:guid}", async (Guid roadmapId, RoadmapDbContext db) =>
        {
            var rm = await db.Roadmaps.FindAsync(roadmapId); if (rm is null) return Results.NotFound();
            db.Roadmaps.Remove(rm); await db.SaveChangesAsync(); return Results.NoContent();
        });

        group.MapGet("/{roadmapId:guid}/history", async (Guid roadmapId, int? limit, RoadmapDbContext db) =>
        {
            var q = db.StatusChanges.AsNoTracking().Include(s => s.Node).Where(s => s.RoadmapId == roadmapId).OrderByDescending(s => s.ChangedAt);
            var changes = await (limit.HasValue ? q.Take(limit.Value) : q.Take(100)).ToListAsync();
            return Results.Ok(changes.Select(s => new StatusChangeDto(s.Id, s.NodeId, s.Node.Title, s.OldStatus.ToString(), s.NewStatus.ToString(), s.Trigger, s.ChangedAt.ToString("yyyy-MM-dd HH:mm:ss"))));
        });

        // ===== Nodes =====
        var nodes = app.MapGroup("/api/roadmaps/{roadmapId:guid}/nodes").WithTags("Nodes");

        nodes.MapPost("/", async (Guid roadmapId, CreateNodeRequest req, RoadmapDbContext db) =>
        {
            if (!await db.Roadmaps.AnyAsync(r => r.Id == roadmapId)) return Results.NotFound();
            if (req.ParentId.HasValue && !await db.Nodes.AnyAsync(n => n.Id == req.ParentId.Value && n.RoadmapId == roadmapId)) return Results.NotFound();
            var node = new RoadmapNode { Id = Guid.NewGuid(), RoadmapId = roadmapId, ParentId = req.ParentId,
                Title = req.Title, IsActionable = req.IsActionable, SortOrder = req.SortOrder,
                Unit = req.Unit, TotalSize = req.TotalSize, UnitsPerHour = req.UnitsPerHour,
                PointsPerUnit = req.PointsPerUnit, ScheduleTemplate = req.ScheduleTemplate,
                IsChecklist = req.IsChecklist };
            db.Nodes.Add(node); await db.SaveChangesAsync();
            await ReplanStartedSprintsAsync(db, roadmapId);
            return Results.Created("", new NodeDto(node.Id, node.ParentId, node.Title, node.IsActionable, node.Status.ToString(),
                node.Unit, node.TotalSize, node.UnitsPerHour, node.PointsPerUnit, node.ScheduleTemplate, node.SortOrder, node.ScheduleBlockId, node.BlockSortOrder, [], [], node.IsChecklist));
        });

        nodes.MapPut("/{nodeId:guid}", async (Guid roadmapId, Guid nodeId, UpdateNodeRequest req, RoadmapDbContext db) =>
        {
            var node = await db.Nodes.FirstOrDefaultAsync(n => n.Id == nodeId && n.RoadmapId == roadmapId);
            if (node is null) return Results.NotFound();
            node.Title = req.Title; node.IsActionable = req.IsActionable; node.SortOrder = req.SortOrder;
            node.IsChecklist = req.IsChecklist;
            node.Unit = req.Unit; node.TotalSize = req.TotalSize; node.UnitsPerHour = req.UnitsPerHour;
            node.PointsPerUnit = req.PointsPerUnit; node.ScheduleTemplate = req.ScheduleTemplate;
            await db.SaveChangesAsync();
            await ReplanStartedSprintsAsync(db, roadmapId);
            return Results.NoContent();
        });

        nodes.MapPatch("/{nodeId:guid}/status", async (Guid roadmapId, Guid nodeId, UpdateNodeStatusRequest req, RoadmapDbContext db) =>
        {
            var node = await db.Nodes.FirstOrDefaultAsync(n => n.Id == nodeId && n.RoadmapId == roadmapId);
            if (node is null) return Results.NotFound();
            if (!Enum.TryParse<ActionItemStatus>(req.Status, true, out var st)) return Results.BadRequest("Invalid status");
            var old = node.Status; if (old == st) return Results.NoContent();
            node.Status = st;
            db.StatusChanges.Add(new StatusChange { Id = Guid.NewGuid(), RoadmapId = roadmapId, NodeId = nodeId, OldStatus = old, NewStatus = st, Trigger = "manual" });
            if (st == ActionItemStatus.Completed)
                await ActivateNextInQueue(db, node);
            await db.SaveChangesAsync();
            await ReplanStartedSprintsAsync(db, roadmapId);
            return Results.NoContent();
        });

        nodes.MapPatch("/{nodeId:guid}/move", async (Guid roadmapId, Guid nodeId, MoveNodeRequest req, RoadmapDbContext db) =>
        {
            var node = await db.Nodes.FirstOrDefaultAsync(n => n.Id == nodeId && n.RoadmapId == roadmapId);
            if (node is null) return Results.NotFound();
            if (req.NewParentId.HasValue) { var d = await GetDescendantIds(db, roadmapId, nodeId); if (d.Contains(req.NewParentId.Value)) return Results.BadRequest("Cycle"); }
            node.ParentId = req.NewParentId; node.SortOrder = req.SortOrder; await db.SaveChangesAsync(); return Results.NoContent();
        });

        nodes.MapPatch("/{nodeId:guid}/reorder", async (Guid roadmapId, Guid nodeId, ReorderNodeRequest req, RoadmapDbContext db) =>
        {
            var node = await db.Nodes.FirstOrDefaultAsync(n => n.Id == nodeId && n.RoadmapId == roadmapId);
            if (node is null) return Results.NotFound();
            var siblings = await db.Nodes.Where(n => n.RoadmapId == roadmapId && n.ParentId == node.ParentId)
                .OrderBy(n => n.SortOrder).ThenBy(n => n.CreatedAt).ToListAsync();
            var idx = siblings.FindIndex(n => n.Id == nodeId);
            if (idx < 0) return Results.NotFound();
            var targetIdx = req.Direction == "up" ? idx - 1 : idx + 1;
            if (targetIdx < 0 || targetIdx >= siblings.Count) return Results.BadRequest("Already at the edge.");
            var other = siblings[targetIdx];
            (node.SortOrder, other.SortOrder) = (other.SortOrder, node.SortOrder);
            if (node.SortOrder == other.SortOrder) { if (req.Direction == "up") node.SortOrder--; else node.SortOrder++; }
            await db.SaveChangesAsync(); return Results.NoContent();
        });

        nodes.MapPost("/{nodeId:guid}/categories", async (Guid roadmapId, Guid nodeId, AddCategoryLinkRequest req, RoadmapDbContext db) =>
        {
            var node = await db.Nodes.FirstOrDefaultAsync(n => n.Id == nodeId && n.RoadmapId == roadmapId);
            if (node is null) return Results.NotFound(); if (!node.IsActionable) return Results.BadRequest("Not actionable");
            var cat = await db.Nodes.FirstOrDefaultAsync(n => n.Id == req.CategoryId && n.RoadmapId == roadmapId && !n.IsActionable);
            if (cat is null) return Results.NotFound();
            if (await db.NodeCategoryLinks.AnyAsync(l => l.NodeId == nodeId && l.CategoryId == req.CategoryId)) return Results.Conflict("Exists");
            var link = new NodeCategoryLink { Id = Guid.NewGuid(), NodeId = nodeId, CategoryId = req.CategoryId };
            db.NodeCategoryLinks.Add(link); await db.SaveChangesAsync();
            return Results.Created("", new CategoryLinkDto(link.Id, cat.Id, cat.Title));
        });

        nodes.MapDelete("/{nodeId:guid}/categories/{linkId:guid}", async (Guid roadmapId, Guid nodeId, Guid linkId, RoadmapDbContext db) =>
        {
            var link = await db.NodeCategoryLinks.FirstOrDefaultAsync(l => l.Id == linkId && l.NodeId == nodeId);
            if (link is null) return Results.NotFound(); db.NodeCategoryLinks.Remove(link); await db.SaveChangesAsync(); return Results.NoContent();
        });

        nodes.MapDelete("/{nodeId:guid}", async (Guid roadmapId, Guid nodeId, RoadmapDbContext db) =>
        {
            var node = await db.Nodes.FirstOrDefaultAsync(n => n.Id == nodeId && n.RoadmapId == roadmapId);
            if (node is null) return Results.NotFound(); db.Nodes.Remove(node); await db.SaveChangesAsync();
            await ReplanStartedSprintsAsync(db, roadmapId);
            return Results.NoContent();
        });

        // Node subpoint templates (used by checklist nodes)
        nodes.MapGet("/{nodeId:guid}/subpoints", async (Guid roadmapId, Guid nodeId, RoadmapDbContext db) =>
        {
            if (!await db.Nodes.AnyAsync(n => n.Id == nodeId && n.RoadmapId == roadmapId)) return Results.NotFound();
            var sps = await db.NodeSubPoints.AsNoTracking().Where(s => s.NodeId == nodeId).OrderBy(s => s.SortOrder).ToListAsync();
            return Results.Ok(sps.Select(s => new NodeSubPointDto(s.Id, s.Title, s.SortOrder)));
        });

        nodes.MapPost("/{nodeId:guid}/subpoints", async (Guid roadmapId, Guid nodeId, CreateNodeSubPointRequest req, RoadmapDbContext db) =>
        {
            if (!await db.Nodes.AnyAsync(n => n.Id == nodeId && n.RoadmapId == roadmapId)) return Results.NotFound();
            var maxSort = await db.NodeSubPoints.Where(s => s.NodeId == nodeId).MaxAsync(s => (int?)s.SortOrder) ?? -1;
            var sp = new NodeSubPoint { Id = Guid.NewGuid(), NodeId = nodeId, Title = req.Title.Trim(), SortOrder = maxSort + 1 };
            db.NodeSubPoints.Add(sp); await db.SaveChangesAsync();
            return Results.Created("", new NodeSubPointDto(sp.Id, sp.Title, sp.SortOrder));
        });

        nodes.MapPatch("/{nodeId:guid}/subpoints/{spId:guid}", async (Guid roadmapId, Guid nodeId, Guid spId, UpdateNodeSubPointRequest req, RoadmapDbContext db) =>
        {
            var sp = await db.NodeSubPoints.FirstOrDefaultAsync(s => s.Id == spId && s.NodeId == nodeId);
            if (sp is null) return Results.NotFound();
            if (!await db.Nodes.AnyAsync(n => n.Id == nodeId && n.RoadmapId == roadmapId)) return Results.NotFound();
            sp.Title = req.Title.Trim(); await db.SaveChangesAsync(); return Results.NoContent();
        });

        nodes.MapDelete("/{nodeId:guid}/subpoints/{spId:guid}", async (Guid roadmapId, Guid nodeId, Guid spId, RoadmapDbContext db) =>
        {
            var sp = await db.NodeSubPoints.FirstOrDefaultAsync(s => s.Id == spId && s.NodeId == nodeId);
            if (sp is null) return Results.NotFound();
            if (!await db.Nodes.AnyAsync(n => n.Id == nodeId && n.RoadmapId == roadmapId)) return Results.NotFound();
            db.NodeSubPoints.Remove(sp); await db.SaveChangesAsync(); return Results.NoContent();
        });

        nodes.MapGet("/{nodeId:guid}/history", async (Guid roadmapId, Guid nodeId, RoadmapDbContext db) =>
        {
            var changes = await db.StatusChanges.AsNoTracking().Include(s => s.Node)
                .Where(s => s.RoadmapId == roadmapId && s.NodeId == nodeId).OrderByDescending(s => s.ChangedAt).ToListAsync();
            return Results.Ok(changes.Select(s => new StatusChangeDto(s.Id, s.NodeId, s.Node.Title, s.OldStatus.ToString(), s.NewStatus.ToString(), s.Trigger, s.ChangedAt.ToString("yyyy-MM-dd HH:mm:ss"))));
        });

        // Work log history for a specific node (all-time, across sprints)
        nodes.MapGet("/{nodeId:guid}/logs", async (Guid roadmapId, Guid nodeId, RoadmapDbContext db) =>
        {
            var node = await db.Nodes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == nodeId && n.RoadmapId == roadmapId);
            if (node is null) return Results.NotFound();
            var logs = await db.WorkLogs.AsNoTracking().Include(w => w.Sprint)
                .Where(w => w.NodeId == nodeId && w.RoadmapId == roadmapId)
                .OrderByDescending(w => w.Date).ToListAsync();
            return Results.Ok(new WorkLogHistoryDto(node.Id, node.Title, node.Unit,
                logs.Select(w => new WorkLogHistoryEntryDto(w.Id, w.Date.ToString("yyyy-MM-dd"), w.Amount, w.Note, w.Sprint.Name)).ToList()));
        });

        // ===== Sprints =====
        var sprints = app.MapGroup("/api/roadmaps/{roadmapId:guid}/sprints").WithTags("Sprints");

        sprints.MapGet("/", async (Guid roadmapId, RoadmapDbContext db) =>
            Results.Ok(await db.Sprints.AsNoTracking().Where(s => s.RoadmapId == roadmapId).OrderByDescending(s => s.StartDate)
                .Select(s => new SprintDto(s.Id, s.Name, s.StartDate.ToString("yyyy-MM-dd"), s.EndDate.ToString("yyyy-MM-dd"), s.IsOpen, s.IsStarted, s.RelaxDays)).ToListAsync()));

        sprints.MapPost("/", async (Guid roadmapId, CreateSprintRequest req, RoadmapDbContext db) =>
        {
            if (!DateOnly.TryParse(req.StartDate, out var s) || !DateOnly.TryParse(req.EndDate, out var e)) return Results.BadRequest("Bad dates");
            if (e <= s) return Results.BadRequest("End must be after start");
            var overlap = await db.Sprints.AnyAsync(x => x.RoadmapId == roadmapId && x.StartDate <= e && x.EndDate >= s);
            if (overlap) return Results.BadRequest("Sprint dates overlap with an existing sprint.");
            var sp = new Sprint { Id = Guid.NewGuid(), RoadmapId = roadmapId, Name = req.Name, StartDate = s, EndDate = e };
            db.Sprints.Add(sp); await db.SaveChangesAsync();
            return Results.Created("", ToSprintDto(sp));
        });

        sprints.MapDelete("/{sprintId:guid}", async (Guid roadmapId, Guid sprintId, RoadmapDbContext db) =>
        {
            var sp = await db.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId && s.RoadmapId == roadmapId);
            if (sp is null) return Results.NotFound(); db.Sprints.Remove(sp); await db.SaveChangesAsync(); return Results.NoContent();
        });

        sprints.MapPatch("/{sprintId:guid}/close", async (Guid roadmapId, Guid sprintId, RoadmapDbContext db) =>
        {
            var sp = await db.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId && s.RoadmapId == roadmapId);
            if (sp is null) return Results.NotFound();
            if (!sp.IsOpen) return Results.BadRequest("Already closed.");
            var yesterday = AppClock.Today().AddDays(-1);
            sp.EndDate = sp.StartDate > AppClock.Today() ? sp.StartDate.AddDays(-1) : yesterday;
            await db.SaveChangesAsync();
            return Results.Ok(ToSprintDto(sp));
        });

        // Edit sprint name/dates. Safe after start: past plan rows are frozen history, and
        // Performance is window-filtered to [StartDate, EndDate], so changing the end date
        // (e.g. to complete early) reshapes the performance numbers without touching data.
        // Extending the end date re-plans the added days from today's state.
        sprints.MapPatch("/{sprintId:guid}", async (Guid roadmapId, Guid sprintId, UpdateSprintRequest req, RoadmapDbContext db) =>
        {
            var sp = await db.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId && s.RoadmapId == roadmapId);
            if (sp is null) return Results.NotFound();
            if (!DateOnly.TryParse(req.StartDate, out var s) || !DateOnly.TryParse(req.EndDate, out var e))
                return Results.BadRequest("Invalid date.");
            if (e < s) return Results.BadRequest("End date must be on or after start date.");
            var overlap = await db.Sprints.AnyAsync(x => x.RoadmapId == roadmapId && x.Id != sprintId && x.StartDate <= e && x.EndDate >= s);
            if (overlap) return Results.BadRequest("Sprint dates overlap with an existing sprint.");
            if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest("Name is required.");
            sp.Name = req.Name.Trim();
            sp.StartDate = s;
            sp.EndDate = e;
            await db.SaveChangesAsync();
            await ReplanStartedSprintsAsync(db, roadmapId);
            return Results.Ok(ToSprintDto(sp));
        });

        sprints.MapPatch("/{sprintId:guid}/relax/{date}", async (Guid roadmapId, Guid sprintId, string date, RoadmapDbContext db) =>
        {
            if (!DateOnly.TryParse(date, out var pd)) return Results.BadRequest("Invalid date.");
            var sp = await db.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId && s.RoadmapId == roadmapId);
            if (sp is null) return Results.NotFound();
            var days = ParseRelaxDays(sp.RelaxDays);
            var ds = pd.ToString("yyyy-MM-dd");
            if (days.Contains(ds)) days.Remove(ds); else days.Add(ds);
            sp.RelaxDays = System.Text.Json.JsonSerializer.Serialize(days);
            await db.SaveChangesAsync();
            await ReplanStartedSprintsAsync(db, roadmapId);
            return Results.Ok(ToSprintDto(sp));
        });

        // ===== Sprint Goals =====
        var sgoals = app.MapGroup("/api/roadmaps/{roadmapId:guid}/sprints/{sprintId:guid}/goals").WithTags("SprintGoals");

        sgoals.MapGet("/", async (Guid roadmapId, Guid sprintId, RoadmapDbContext db) =>
        {
            var goals = await db.SprintGoals.AsNoTracking().Include(g => g.Logs)
                .Where(g => g.SprintId == sprintId).OrderBy(g => g.SortOrder).ToListAsync();
            return Results.Ok(goals.Select(g => new SprintGoalDto(g.Id, g.Title, g.Unit, g.TargetAmount,
                g.Description, g.SortOrder, Math.Round(g.Logs.Sum(l => l.Amount), 2))).ToList());
        });

        sgoals.MapPost("/", async (Guid roadmapId, Guid sprintId, CreateSprintGoalRequest req, RoadmapDbContext db) =>
        {
            var maxSort = await db.SprintGoals.Where(g => g.SprintId == sprintId).MaxAsync(g => (int?)g.SortOrder) ?? -1;
            var goal = new SprintGoal { Id = Guid.NewGuid(), SprintId = sprintId, Title = req.Title.Trim(),
                Unit = req.Unit, TargetAmount = req.TargetAmount, Description = req.Description, SortOrder = maxSort + 1 };
            db.SprintGoals.Add(goal); await db.SaveChangesAsync();
            return Results.Created("", new SprintGoalDto(goal.Id, goal.Title, goal.Unit, goal.TargetAmount, goal.Description, goal.SortOrder, 0));
        });

        sgoals.MapPut("/{goalId:guid}", async (Guid roadmapId, Guid sprintId, Guid goalId, UpdateSprintGoalRequest req, RoadmapDbContext db) =>
        {
            var goal = await db.SprintGoals.FirstOrDefaultAsync(g => g.Id == goalId && g.SprintId == sprintId);
            if (goal is null) return Results.NotFound();
            goal.Title = req.Title.Trim(); goal.Unit = req.Unit; goal.TargetAmount = req.TargetAmount; goal.Description = req.Description;
            await db.SaveChangesAsync(); return Results.NoContent();
        });

        sgoals.MapDelete("/{goalId:guid}", async (Guid roadmapId, Guid sprintId, Guid goalId, RoadmapDbContext db) =>
        {
            var goal = await db.SprintGoals.FirstOrDefaultAsync(g => g.Id == goalId && g.SprintId == sprintId);
            if (goal is null) return Results.NotFound();
            db.SprintGoals.Remove(goal); await db.SaveChangesAsync(); return Results.NoContent();
        });

        // Log progress on a sprint goal
        sgoals.MapPost("/{goalId:guid}/log", async (Guid roadmapId, Guid sprintId, Guid goalId, LogSprintGoalRequest req, RoadmapDbContext db) =>
        {
            if (!DateOnly.TryParse(req.Date, out var pd)) return Results.BadRequest("Invalid date.");
            var goal = await db.SprintGoals.FirstOrDefaultAsync(g => g.Id == goalId && g.SprintId == sprintId);
            if (goal is null) return Results.NotFound();
            var log = new SprintGoalLog { Id = Guid.NewGuid(), SprintGoalId = goalId, Date = pd, Amount = req.Amount };
            db.SprintGoalLogs.Add(log); await db.SaveChangesAsync();
            return Results.Created("", log.Id);
        });

        // Delete a goal log
        sgoals.MapDelete("/{goalId:guid}/log/{logId:guid}", async (Guid roadmapId, Guid sprintId, Guid goalId, Guid logId, RoadmapDbContext db) =>
        {
            var log = await db.SprintGoalLogs.FirstOrDefaultAsync(l => l.Id == logId && l.SprintGoalId == goalId);
            if (log is null) return Results.NotFound();
            db.SprintGoalLogs.Remove(log); await db.SaveChangesAsync(); return Results.NoContent();
        });

        // ===== Start Sprint — queue-aware snapshot with capping =====
        sprints.MapPost("/{sprintId:guid}/start", async (Guid roadmapId, Guid sprintId, RoadmapDbContext db) =>
        {
            var sprint = await db.Sprints.Include(s => s.PlanEntries).FirstOrDefaultAsync(s => s.Id == sprintId && s.RoadmapId == roadmapId);
            if (sprint is null) return Results.NotFound();
            if (sprint.IsStarted) return Results.BadRequest("Sprint already started");

            var allNodes = await db.Nodes.AsNoTracking().Where(n => n.RoadmapId == roadmapId).OrderBy(n => n.SortOrder).ToListAsync();
            var blocks = await db.ScheduleBlocks.AsNoTracking().Include(sb => sb.Items).Where(sb => sb.RoadmapId == roadmapId).ToListAsync();
            var loggedBefore = await LoggedBeforeAsync(db, roadmapId, sprint.StartDate);

            var dates = new List<DateOnly>();
            for (var d = sprint.StartDate; d <= sprint.EndDate; d = d.AddDays(1)) dates.Add(d);

            var computed = ComputeSprintPlan(allNodes, blocks, dates, loggedBefore, ParseRelaxDays(sprint.RelaxDays));
            var entries = computed.Select(c => new SprintPlanEntry
            {
                Id = Guid.NewGuid(), SprintId = sprint.Id, NodeId = c.NodeId,
                CategoryId = null, Date = c.Date, StartMinute = c.StartMinute,
                DurationMinutes = c.DurationMinutes, PlannedUnits = c.PlannedUnits
            }).ToList();

            sprint.IsStarted = true;
            sprint.StartedAt = DateTime.UtcNow;
            db.SprintPlanEntries.AddRange(entries);
            await db.SaveChangesAsync();
            return Results.Ok(ToSprintDto(sprint));
        });

        sprints.MapGet("/{sprintId:guid}/plan", async (Guid roadmapId, Guid sprintId, RoadmapDbContext db) =>
        {
            var sprint = await db.Sprints.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sprintId && s.RoadmapId == roadmapId);
            if (sprint is null) return Results.NotFound();
            var entries = await db.SprintPlanEntries.AsNoTracking().Include(p => p.Node)
                .Where(p => p.SprintId == sprintId).OrderBy(p => p.Date).ThenBy(p => p.StartMinute).ToListAsync();
            return Results.Ok(entries.Select(p => new SprintPlanEntryDto(p.NodeId, p.Node.Title, p.Date.ToString("yyyy-MM-dd"), p.StartMinute, p.DurationMinutes, p.PlannedUnits)));
        });

        // ===== Performance — sprint-scoped. Draft sprints get projected plan, no actual data. =====
        sprints.MapGet("/{sprintId:guid}/performance", async (Guid roadmapId, Guid sprintId, RoadmapDbContext db) =>
        {
            var sprint = await db.Sprints.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sprintId && s.RoadmapId == roadmapId);
            if (sprint is null) return Results.NotFound();

            var dates = new List<DateOnly>();
            for (var d = sprint.StartDate; d <= sprint.EndDate; d = d.AddDays(1)) dates.Add(d);

            // For draft sprints: compute plan on-the-fly, no logs
            // For started sprints: use persisted snapshot + sprint-scoped logs
            List<(Guid NodeId, DateOnly Date, double PlannedUnits, int DurationMinutes)> planData;
            List<WorkLog> sprintLogs;

            if (sprint.IsStarted)
            {
                // Bound the frozen snapshot + logs to the sprint's CURRENT window so that
                // editing StartDate/EndDate after start correctly reshapes Performance
                // (the snapshot stays immutable; out-of-window rows are simply excluded).
                var planEntries = await db.SprintPlanEntries.AsNoTracking()
                    .Where(p => p.SprintId == sprintId && p.Date >= sprint.StartDate && p.Date <= sprint.EndDate).ToListAsync();
                planData = planEntries.Select(p => (p.NodeId, p.Date, p.PlannedUnits, p.DurationMinutes)).ToList();
                sprintLogs = await db.WorkLogs.AsNoTracking()
                    .Where(w => w.SprintId == sprintId && w.Date >= sprint.StartDate && w.Date <= sprint.EndDate).ToListAsync();
            }
            else
            {
                // Draft — project the plan on-the-fly
                var allNodes = await db.Nodes.AsNoTracking().Where(n => n.RoadmapId == roadmapId).OrderBy(n => n.SortOrder).ToListAsync();
                var blocks = await db.ScheduleBlocks.AsNoTracking().Include(sb => sb.Items).Where(sb => sb.RoadmapId == roadmapId).ToListAsync();
                var loggedBefore = await LoggedBeforeAsync(db, roadmapId, sprint.StartDate);
                var computed = ComputeSprintPlan(allNodes, blocks, dates, loggedBefore, ParseRelaxDays(sprint.RelaxDays));
                planData = computed.Select(c => (c.NodeId, c.Date, c.PlannedUnits, c.DurationMinutes)).ToList();
                sprintLogs = []; // no logs for draft
            }

            // Group by node
            var planByNode = planData.GroupBy(p => p.NodeId).ToDictionary(g => g.Key, g => g.ToList());
            var logsByNode = sprintLogs.GroupBy(w => w.NodeId).ToDictionary(g => g.Key, g => g.ToList());
            var nodeIds = planByNode.Keys.Union(logsByNode.Keys).ToHashSet();

            // Load node metadata
            var nodeLookup = new Dictionary<Guid, RoadmapNode>();
            if (nodeIds.Count > 0)
            {
                var loaded = await db.Nodes.AsNoTracking().Where(n => nodeIds.Contains(n.Id)).ToListAsync();
                nodeLookup = loaded.ToDictionary(n => n.Id);
            }

            var items = new List<PerformanceItemDto>();
            var dailyPointsMap = dates.ToDictionary(d => d, _ => 0.0);

            // First pass: compute per-item planned/done points
            var itemDataList = new List<(Guid NodeId, RoadmapNode Node, double TotalPlannedPts, double TotalDonePts, int Sessions,
                double TotalPlannedUnits, double TotalDoneUnits, double TotalMins,
                List<(DateOnly Date, double PlannedPts)> DailyPlan, List<(DateOnly Date, double DonePts)> DailyDone)>();

            double grandTotalPlannedPts = 0;

            foreach (var nodeId in nodeIds)
            {
                if (!nodeLookup.TryGetValue(nodeId, out var node)) continue;
                var ppu = node.PointsPerUnit ?? 0;

                var nodePlan = planByNode.GetValueOrDefault(nodeId, []);
                var nodeLogs = logsByNode.GetValueOrDefault(nodeId, []);

                var totalPlannedUnits = nodePlan.Sum(p => p.PlannedUnits);
                var totalDoneUnits = nodeLogs.Sum(w => w.Amount);
                var totalPlannedPts = totalPlannedUnits * ppu;
                var totalDonePts = totalDoneUnits * ppu;
                var sessions = nodePlan.Select(p => p.Date).Distinct().Count();
                var totalMins = nodePlan.Sum(p => (double)p.DurationMinutes);

                var dailyPlan = dates.Select(d => (Date: d, PlannedPts: nodePlan.Where(p => p.Date == d).Sum(p => p.PlannedUnits) * ppu)).ToList();
                var dailyDone = dates.Select(d => (Date: d, DonePts: nodeLogs.Where(w => w.Date == d).Sum(w => w.Amount) * ppu)).ToList();

                grandTotalPlannedPts += totalPlannedPts;
                itemDataList.Add((nodeId, node, totalPlannedPts, totalDonePts, sessions, totalPlannedUnits, totalDoneUnits, totalMins, dailyPlan, dailyDone));

                foreach (var log in nodeLogs) { if (dailyPointsMap.ContainsKey(log.Date)) dailyPointsMap[log.Date] += log.Amount * ppu; }
            }

            // Second pass: build items with per-item cumulative % (points-based relative to item's own planned points)
            foreach (var (nodeId, node, totalPlannedPts, totalDonePts, sessions, totalPlannedUnits, totalDoneUnits, totalMins, dailyPlan, dailyDone) in itemDataList)
            {
                var ppu = node.PointsPerUnit ?? 0;
                var dailyCum = new List<DailyCumulativeDto>();
                double runningDonePts = 0;
                double runningPlannedPts = 0;

                for (int di = 0; di < dates.Count; di++)
                {
                    runningDonePts += dailyDone[di].DonePts;
                    runningPlannedPts += dailyPlan[di].PlannedPts;

                    var actualPct = totalPlannedPts > 0 ? Math.Round(runningDonePts / totalPlannedPts * 100, 1) : 0;
                    var idealPct = totalPlannedPts > 0 ? Math.Round(runningPlannedPts / totalPlannedPts * 100, 1) : 0;
                    dailyCum.Add(new DailyCumulativeDto(dates[di].ToString("yyyy-MM-dd"), actualPct, idealPct));
                }

                // Projected completion: walk through plan days, accumulate units, find when totalSize is reached
                var willComplete = false;
                string? projectedDate = null;
                if (node.Status == ActionItemStatus.Completed)
                {
                    willComplete = true;
                    var completedNodeLogs = logsByNode.GetValueOrDefault(nodeId, []);
                    projectedDate = completedNodeLogs.OrderByDescending(w => w.Date).FirstOrDefault()?.Date.ToString("yyyy-MM-dd");
                }
                else if (node.TotalSize.HasValue && node.TotalSize.Value > 0)
                {
                    var totalLogged = await db.WorkLogs.AsNoTracking().Where(w => w.NodeId == nodeId).SumAsync(w => w.Amount);
                    double running = totalLogged;
                    var nodePlanForProj = planByNode.GetValueOrDefault(nodeId, []).OrderBy(p => p.Date).ToList();
                    foreach (var p in nodePlanForProj)
                    {
                        running += p.PlannedUnits;
                        if (running >= node.TotalSize.Value)
                        {
                            willComplete = true;
                            projectedDate = p.Date.ToString("yyyy-MM-dd");
                            break;
                        }
                    }
                }

                items.Add(new PerformanceItemDto(nodeId, node.Title, node.Unit, node.TotalSize, node.UnitsPerHour, node.PointsPerUnit,
                    sessions, Math.Round(totalPlannedUnits, 1), Math.Round(totalDoneUnits, 1),
                    Math.Round(totalPlannedPts, 1), Math.Round(totalDonePts, 1), Math.Round(totalMins, 0),
                    willComplete, projectedDate, dailyCum, node.Status == ActionItemStatus.Completed));
            }

            // Add habit points: +2 for checked, -2 for missed (strictly past days only)
            var today = AppClock.Today();
            var sprintHabits = await db.SprintHabits.AsNoTracking().Include(sh => sh.Checks)
                .Where(sh => sh.SprintId == sprintId && !sh.IsPaused).ToListAsync();
            double totalHabitPlannedPts = 0;
            double totalHabitEarnedPts = 0;
            foreach (var d in dates)
            {
                foreach (var sh in sprintHabits)
                {
                    // Only count planned points for days that have passed or are today
                    if (d <= today)
                        totalHabitPlannedPts += 2;

                    var check = sh.Checks.FirstOrDefault(c => c.Date == d);
                    if (check?.IsChecked == true)
                    {
                        dailyPointsMap[d] += 2;
                        totalHabitEarnedPts += 2;
                    }
                    else if (d < today) // only penalize days that have fully passed
                    {
                        dailyPointsMap[d] -= 2;
                        totalHabitEarnedPts -= 2;
                    }
                }
            }

            // Add completed task points (earned only, NOT planned)
            var completedTasks = await db.SingleTasks.AsNoTracking()
                .Where(t => t.RoadmapId == roadmapId && t.IsCompleted && t.CompletedDate.HasValue
                    && t.CompletedDate >= sprint.StartDate && t.CompletedDate <= sprint.EndDate)
                .ToListAsync();
            double totalTaskEarnedPts = 0;
            foreach (var ct in completedTasks)
            {
                var taskPts = Math.Floor(ct.EstimatedHours * 2);
                totalTaskEarnedPts += taskPts;
                if (ct.CompletedDate.HasValue && dailyPointsMap.ContainsKey(ct.CompletedDate.Value))
                    dailyPointsMap[ct.CompletedDate.Value] += taskPts;
            }

            // Add custom log points (earned only, not planned)
            var customLogs = await db.CustomLogs.AsNoTracking()
                .Where(c => c.RoadmapId == roadmapId && c.Date >= sprint.StartDate && c.Date <= sprint.EndDate)
                .ToListAsync();
            double totalCustomPts = 0;
            foreach (var cl in customLogs)
            {
                totalCustomPts += cl.Points;
                if (dailyPointsMap.ContainsKey(cl.Date)) dailyPointsMap[cl.Date] += cl.Points;
            }
            var customLogDtos = customLogs.Select(c => new CustomLogDto(c.Id, c.Title, c.Points, c.Date.ToString("yyyy-MM-dd"), c.Note)).ToList();

            var grandPlanned = items.Sum(i => i.PlannedPoints) + totalHabitPlannedPts;
            var grandEarned = items.Sum(i => i.EarnedPoints) + totalHabitEarnedPts + totalTaskEarnedPts + totalCustomPts;

            var ctDtos = completedTasks.Select(t => new CompletedTaskDto(t.Id, t.Title, t.Priority.ToString(),
                t.EstimatedHours, Math.Floor(t.EstimatedHours * 2), t.CompletedDate!.Value.ToString("yyyy-MM-dd"))).ToList();

            // Category time breakdown — hierarchical, every category gets accumulated time from descendants
            var allNodesForCat = await db.Nodes.AsNoTracking().Where(n => n.RoadmapId == roadmapId).ToListAsync();
            var nodeLk = allNodesForCat.ToDictionary(n => n.Id);

            // Map each item to all its ancestor category IDs
            var catAccum = new Dictionary<Guid, (double Minutes, double Points)>();
            foreach (var item in items)
            {
                if (!nodeLk.TryGetValue(item.NodeId, out var node)) continue;
                var totalMins = planData.Where(p => p.NodeId == item.NodeId).Sum(p => (double)p.DurationMinutes);
                // Walk up the parent chain, accumulating to every ancestor category
                var cur = node;
                while (cur.ParentId.HasValue && nodeLk.TryGetValue(cur.ParentId.Value, out var parent))
                {
                    if (!parent.IsActionable) // it's a category
                    {
                        if (!catAccum.ContainsKey(parent.Id)) catAccum[parent.Id] = (0, 0);
                        var prev = catAccum[parent.Id];
                        catAccum[parent.Id] = (prev.Minutes + totalMins, prev.Points + item.PlannedPoints);
                    }
                    cur = parent;
                }
                // Items at root level
                if (!node.ParentId.HasValue || !nodeLk.ContainsKey(node.ParentId.Value))
                {
                    var uncatId = Guid.Empty;
                    if (!catAccum.ContainsKey(uncatId)) catAccum[uncatId] = (0, 0);
                    var prev2 = catAccum[uncatId];
                    catAccum[uncatId] = (prev2.Minutes + totalMins, prev2.Points + item.PlannedPoints);
                }
            }

            // Build hierarchical DTOs
            List<CategoryTimeDto> BuildCatTree(Guid? parentId, int depth)
            {
                var result = new List<CategoryTimeDto>();
                var cats = allNodesForCat.Where(n => !n.IsActionable && n.ParentId == parentId).OrderByDescending(n => catAccum.GetValueOrDefault(n.Id).Minutes);
                foreach (var cat in cats)
                {
                    if (!catAccum.ContainsKey(cat.Id)) continue; // skip categories with no planned time
                    var (mins, pts) = catAccum[cat.Id];
                    var children = BuildCatTree(cat.Id, depth + 1);
                    result.Add(new CategoryTimeDto(cat.Title, Math.Round(mins, 1), Math.Round(pts, 1), depth, children));
                }
                return result;
            }
            var catDtos = BuildCatTree(null, 0);

            // Sprint goals
            var sprintGoals = await db.SprintGoals.AsNoTracking().Include(g => g.Logs)
                .Where(g => g.SprintId == sprintId).OrderBy(g => g.SortOrder).ToListAsync();
            var goalDtos = sprintGoals.Select(g => new SprintGoalDto(g.Id, g.Title, g.Unit, g.TargetAmount,
                g.Description, g.SortOrder, Math.Round(g.Logs.Sum(l => l.Amount), 2))).ToList();

            // Add sprint goal log amounts to daily points and earned total
            double totalSprintGoalPts = 0;
            foreach (var goal in sprintGoals)
            {
                foreach (var log in goal.Logs)
                {
                    if (dailyPointsMap.ContainsKey(log.Date))
                    {
                        dailyPointsMap[log.Date] += log.Amount;
                        totalSprintGoalPts += log.Amount;
                    }
                }
            }
            grandEarned += totalSprintGoalPts;

            return Results.Ok(new PerformanceSummaryDto(items, Math.Round(grandPlanned, 1),
                Math.Round(grandEarned, 1),
                dates.Select(d => new DailyPointsDto(d.ToString("yyyy-MM-dd"), Math.Round(dailyPointsMap[d], 1))).ToList(),
                ctDtos, customLogDtos, catDtos, goalDtos));
        });

        // ===== Work Logs (sprint-scoped) =====
        var wl = app.MapGroup("/api/roadmaps/{roadmapId:guid}/worklogs").WithTags("WorkLogs");

        wl.MapGet("/{date}", async (Guid roadmapId, string date, RoadmapDbContext db) =>
        {
            if (!DateOnly.TryParse(date, out var pd)) return Results.BadRequest("Invalid date.");
            var logs = await db.WorkLogs.AsNoTracking().Include(w => w.Node)
                .Where(w => w.RoadmapId == roadmapId && w.Date == pd).OrderBy(w => w.CreatedAt).ToListAsync();
            return Results.Ok(logs.Select(w => new WorkLogDto(w.Id, w.NodeId, w.Node.Title, w.Date.ToString("yyyy-MM-dd"), w.Amount, w.Node.Unit, w.Note)));
        });

        wl.MapPost("/", async (Guid roadmapId, LogWorkRequest req, RoadmapDbContext db) =>
        {
            if (!DateOnly.TryParse(req.Date, out var pd)) return Results.BadRequest("Invalid date.");
            var node = await db.Nodes.FirstOrDefaultAsync(n => n.Id == req.NodeId && n.RoadmapId == roadmapId);
            if (node is null) return Results.NotFound(); if (!node.IsActionable) return Results.BadRequest("Not actionable");

            // Find sprint covering this date
            var sprint = await db.Sprints.FirstOrDefaultAsync(s => s.RoadmapId == roadmapId && s.StartDate <= pd && s.EndDate >= pd && s.IsStarted);
            if (sprint is null) return Results.BadRequest("No active sprint covers this date. Start a sprint first.");

            var ex = await db.WorkLogs.FirstOrDefaultAsync(w => w.SprintId == sprint.Id && w.NodeId == req.NodeId && w.Date == pd);
            if (ex != null) { ex.Amount += req.Amount; ex.Note = req.Note ?? ex.Note; }
            else db.WorkLogs.Add(new WorkLog { Id = Guid.NewGuid(), RoadmapId = roadmapId, SprintId = sprint.Id,
                NodeId = req.NodeId, Date = pd, Amount = req.Amount, Note = req.Note });
            await db.SaveChangesAsync();

            if (node.TotalSize.HasValue && node.Status != ActionItemStatus.Completed)
            {
                var totalLogged = await db.WorkLogs.Where(w => w.NodeId == node.Id).SumAsync(w => w.Amount);
                if (totalLogged >= node.TotalSize.Value)
                {
                    var oldStatus = node.Status;
                    node.Status = ActionItemStatus.Completed;
                    db.StatusChanges.Add(new StatusChange { Id = Guid.NewGuid(), RoadmapId = roadmapId, NodeId = node.Id, OldStatus = oldStatus, NewStatus = ActionItemStatus.Completed, Trigger = "auto_completed" });
                    await ActivateNextInQueue(db, node);
                    await db.SaveChangesAsync();
                }
            }
            await ReplanStartedSprintsAsync(db, roadmapId);
            return Results.NoContent();
        });

        wl.MapPut("/{logId:guid}", async (Guid roadmapId, Guid logId, UpdateWorkLogRequest req, RoadmapDbContext db) =>
        {
            var log = await db.WorkLogs.FirstOrDefaultAsync(w => w.Id == logId && w.RoadmapId == roadmapId);
            if (log is null) return Results.NotFound();
            log.Amount = req.Amount; log.Note = req.Note;
            await db.SaveChangesAsync();
            var node = await db.Nodes.FirstOrDefaultAsync(n => n.Id == log.NodeId);
            if (node is not null && node.TotalSize.HasValue)
            {
                var total = await db.WorkLogs.Where(w => w.NodeId == node.Id).SumAsync(w => w.Amount);
                if (total >= node.TotalSize.Value && node.Status != ActionItemStatus.Completed)
                {
                    var old = node.Status; node.Status = ActionItemStatus.Completed;
                    db.StatusChanges.Add(new StatusChange { Id = Guid.NewGuid(), RoadmapId = roadmapId, NodeId = node.Id, OldStatus = old, NewStatus = ActionItemStatus.Completed, Trigger = "auto_completed" });
                    await ActivateNextInQueue(db, node);
                }
                else if (total < node.TotalSize.Value && node.Status == ActionItemStatus.Completed)
                {
                    node.Status = ActionItemStatus.Active;
                    db.StatusChanges.Add(new StatusChange { Id = Guid.NewGuid(), RoadmapId = roadmapId, NodeId = node.Id, OldStatus = ActionItemStatus.Completed, NewStatus = ActionItemStatus.Active, Trigger = "auto_reverted" });
                }
                await db.SaveChangesAsync();
            }
            await ReplanStartedSprintsAsync(db, roadmapId);
            return Results.NoContent();
        });

        wl.MapDelete("/{logId:guid}", async (Guid roadmapId, Guid logId, RoadmapDbContext db) =>
        {
            var log = await db.WorkLogs.FirstOrDefaultAsync(w => w.Id == logId && w.RoadmapId == roadmapId);
            if (log is null) return Results.NotFound();
            var nodeId = log.NodeId; db.WorkLogs.Remove(log); await db.SaveChangesAsync();
            var node = await db.Nodes.FirstOrDefaultAsync(n => n.Id == nodeId);
            if (node is not null && node.Status == ActionItemStatus.Completed && node.TotalSize.HasValue)
            {
                var total = await db.WorkLogs.Where(w => w.NodeId == nodeId).SumAsync(w => w.Amount);
                if (total < node.TotalSize.Value)
                {
                    node.Status = ActionItemStatus.Active;
                    db.StatusChanges.Add(new StatusChange { Id = Guid.NewGuid(), RoadmapId = roadmapId, NodeId = nodeId, OldStatus = ActionItemStatus.Completed, NewStatus = ActionItemStatus.Active, Trigger = "auto_reverted" });
                    await db.SaveChangesAsync();
                }
            }
            await ReplanStartedSprintsAsync(db, roadmapId);
            return Results.NoContent();
        });

        // ===== Week Plans (sprint-scoped) =====
        var weeks = app.MapGroup("/api/roadmaps/{roadmapId:guid}/weekplan").WithTags("WeekPlans");

        weeks.MapGet("/{date}", async (Guid roadmapId, string date, RoadmapDbContext db) =>
        {
            if (!DateOnly.TryParse(date, out var pd)) return Results.BadRequest("Invalid date.");
            var dayOfWeek = ((int)pd.DayOfWeek + 6) % 7;
            var monday = pd.AddDays(-dayOfWeek); var sunday = monday.AddDays(6);

            // Find sprint covering this week
            var sprint = await db.Sprints.AsNoTracking().Where(s => s.RoadmapId == roadmapId && s.IsStarted && s.StartDate <= sunday && s.EndDate >= monday).FirstOrDefaultAsync();
            if (sprint is null)
                return Results.Ok(new { noSprint = true });

            var plan = await db.WeekPlans.Include(w => w.CustomGoals.OrderBy(g => g.SortOrder))
                .FirstOrDefaultAsync(w => w.RoadmapId == roadmapId && w.WeekStart == monday);
            if (plan is null) { plan = new WeekPlan { Id = Guid.NewGuid(), RoadmapId = roadmapId, WeekStart = monday }; db.WeekPlans.Add(plan); await db.SaveChangesAsync(); plan.CustomGoals = []; }

            // Get sprint plan entries for this week
            var weekDates = Enumerable.Range(0, 7).Select(i => monday.AddDays(i)).Where(d => d >= sprint.StartDate && d <= sprint.EndDate).ToList();
            var planEntries = await db.SprintPlanEntries.AsNoTracking().Include(p => p.Node)
                .Where(p => p.SprintId == sprint.Id && weekDates.Contains(p.Date)).ToListAsync();
            var weekLogs = await db.WorkLogs.AsNoTracking()
                .Where(w => w.SprintId == sprint.Id && weekDates.Contains(w.Date)).ToListAsync();

            // Aggregate by node — with sprint-level completion projections
            var nodeIds = planEntries.Select(p => p.NodeId).Distinct().ToList();
            var allSprintPlan = await db.SprintPlanEntries.AsNoTracking().Include(p => p.Node)
                .Where(p => p.SprintId == sprint.Id).OrderBy(p => p.Date).ToListAsync();
            var allTimeLogs = await db.WorkLogs.AsNoTracking().Where(w => nodeIds.Contains(w.NodeId))
                .GroupBy(w => w.NodeId).Select(g => new { g.Key, Total = g.Sum(w => w.Amount) })
                .ToDictionaryAsync(x => x.Key, x => x.Total);

            var scheduledItems = nodeIds.Select(nid =>
            {
                var nodePlan = planEntries.Where(p => p.NodeId == nid).ToList();
                var first = nodePlan.First();
                var sessions = nodePlan.Select(p => p.Date).Distinct().Count();
                var planned = nodePlan.Sum(p => p.PlannedUnits);
                var logged = weekLogs.Where(w => w.NodeId == nid).Sum(w => w.Amount);
                var totalSize = first.Node.TotalSize;
                var totalLogged = allTimeLogs.GetValueOrDefault(nid, 0);

                // Project completion across the full sprint
                var willComplete = false;
                string? projDate = null;
                if (first.Node.Status == ActionItemStatus.Completed)
                {
                    willComplete = true;
                    projDate = weekLogs.Where(w => w.NodeId == nid).OrderByDescending(w => w.Date).FirstOrDefault()?.Date.ToString("yyyy-MM-dd");
                }
                else if (totalSize.HasValue && totalSize.Value > 0)
                {
                    double running = totalLogged;
                    foreach (var p in allSprintPlan.Where(p => p.NodeId == nid))
                    {
                        running += p.PlannedUnits;
                        if (running >= totalSize.Value) { willComplete = true; projDate = p.Date.ToString("yyyy-MM-dd"); break; }
                    }
                }

                return new WeekScheduledItemDto(nid, first.Node.Title, first.Node.Unit, first.Node.UnitsPerHour,
                    sessions, Math.Round(planned, 1), Math.Round(logged, 1),
                    totalSize, Math.Round(totalLogged, 1), willComplete, projDate,
                    first.Node.Status == ActionItemStatus.Completed);
            }).ToList();

            // Completed tasks this week
            var completedTasks = await db.SingleTasks.AsNoTracking()
                .Where(t => t.RoadmapId == roadmapId && t.IsCompleted && t.CompletedDate.HasValue
                    && weekDates.Contains(t.CompletedDate.Value))
                .OrderBy(t => t.CompletedDate).ToListAsync();
            var ctDtos = completedTasks.Select(t => new CompletedTaskDto(t.Id, t.Title, t.Priority.ToString(),
                t.EstimatedHours, Math.Floor(t.EstimatedHours * 2), t.CompletedDate!.Value.ToString("yyyy-MM-dd"))).ToList();

            // Custom logs this week
            var weekCustomLogs = await db.CustomLogs.AsNoTracking()
                .Where(c => c.RoadmapId == roadmapId && weekDates.Contains(c.Date))
                .OrderBy(c => c.Date).ToListAsync();
            var clDtos = weekCustomLogs.Select(c => new CustomLogDto(c.Id, c.Title, c.Points, c.Date.ToString("yyyy-MM-dd"), c.Note)).ToList();

            // Sprint goals for linking
            var sprintGoals = await db.SprintGoals.AsNoTracking().Include(g => g.Logs)
                .Where(g => g.SprintId == sprint.Id).OrderBy(g => g.SortOrder).ToListAsync();
            var sgLookup = sprintGoals.ToDictionary(g => g.Id);
            var goalDtos = sprintGoals.Select(g => new SprintGoalDto(g.Id, g.Title, g.Unit, g.TargetAmount,
                g.Description, g.SortOrder, Math.Round(g.Logs.Sum(l => l.Amount), 2))).ToList();

            // Include SprintGoal in CustomGoals — need to load them with the FK
            var goalsWithSg = await db.WeekPlanGoals.AsNoTracking()
                .Where(g => g.WeekPlanId == plan.Id).OrderBy(g => g.SortOrder).ToListAsync();
            var weekGoalDtos = goalsWithSg.Select(g => {
                SprintGoal? sg = g.SprintGoalId.HasValue && sgLookup.TryGetValue(g.SprintGoalId.Value, out var s) ? s : null;
                return new WeekPlanGoalDto(g.Id, g.Title, g.TargetDescription, g.TargetAmount, g.ResultAmount, g.ResultNote, g.IsCompleted, g.SortOrder,
                    g.SprintGoalId, sg?.Title, sg?.TargetAmount, sg != null ? Math.Round(sg.Logs.Sum(l => l.Amount), 2) : null, sg?.Unit);
            }).ToList();

            return Results.Ok(new WeekPlanDto(plan.Id, plan.RoadmapId, monday.ToString("yyyy-MM-dd"), plan.IsClosed, plan.Notes,
                scheduledItems, weekGoalDtos,
                ctDtos, clDtos, ToSprintDto(sprint), goalDtos));
        });

        weeks.MapPost("/{date}/goals", async (Guid roadmapId, string date, CreateWeekPlanGoalRequest req, RoadmapDbContext db) =>
        {
            if (!DateOnly.TryParse(date, out var pd)) return Results.BadRequest("Invalid date.");
            var dow = ((int)pd.DayOfWeek + 6) % 7; var monday = pd.AddDays(-dow);
            var plan = await db.WeekPlans.Include(w => w.CustomGoals).FirstOrDefaultAsync(w => w.RoadmapId == roadmapId && w.WeekStart == monday);
            if (plan is null) { plan = new WeekPlan { Id = Guid.NewGuid(), RoadmapId = roadmapId, WeekStart = monday }; db.WeekPlans.Add(plan); await db.SaveChangesAsync(); }
            var maxSort = plan.CustomGoals.Any() ? plan.CustomGoals.Max(g => g.SortOrder) : -1;
            var goal = new WeekPlanGoal { Id = Guid.NewGuid(), WeekPlanId = plan.Id, Title = req.Title, TargetDescription = req.TargetDescription, TargetAmount = req.TargetAmount, SprintGoalId = req.SprintGoalId, SortOrder = maxSort + 1 };
            db.WeekPlanGoals.Add(goal); await db.SaveChangesAsync();
            return Results.Created("", new WeekPlanGoalDto(goal.Id, goal.Title, goal.TargetDescription, goal.TargetAmount, goal.ResultAmount, goal.ResultNote, goal.IsCompleted, goal.SortOrder,
                goal.SprintGoalId, null, null, null, null));
        });

        weeks.MapPut("/{date}/goals/{goalId:guid}", async (Guid roadmapId, string date, Guid goalId, UpdateWeekPlanGoalRequest req, RoadmapDbContext db) =>
        {
            var goal = await db.WeekPlanGoals.Include(g => g.WeekPlan).FirstOrDefaultAsync(g => g.Id == goalId && g.WeekPlan.RoadmapId == roadmapId);
            if (goal is null) return Results.NotFound();
            goal.Title = req.Title; goal.TargetDescription = req.TargetDescription; goal.TargetAmount = req.TargetAmount;
            goal.ResultAmount = req.ResultAmount; goal.ResultNote = req.ResultNote; goal.IsCompleted = req.IsCompleted;

            // When marking complete on a linked goal, auto-set ResultAmount
            if (goal.SprintGoalId.HasValue && req.IsCompleted && !req.ResultAmount.HasValue)
            {
                if (goal.TargetAmount.HasValue && goal.TargetAmount.Value > 0)
                    goal.ResultAmount = goal.TargetAmount;
                else
                {
                    // Fall back to sprint goal's target or 1
                    var sg = await db.SprintGoals.AsNoTracking().FirstOrDefaultAsync(g => g.Id == goal.SprintGoalId.Value);
                    goal.ResultAmount = sg?.TargetAmount > 0 ? sg.TargetAmount : 1;
                }
            }

            // When explicitly uncompleting (resultAmount sent as null), clear the log
            if (goal.SprintGoalId.HasValue && !req.IsCompleted && !req.ResultAmount.HasValue)
            {
                goal.ResultAmount = null;
            }

            // Sync to sprint goal log if linked
            if (goal.SprintGoalId.HasValue)
            {
                var weekDate = goal.WeekPlan.WeekStart;
                var existing = await db.SprintGoalLogs.FirstOrDefaultAsync(l =>
                    l.SprintGoalId == goal.SprintGoalId.Value && l.Date == weekDate);
                var syncAmount = goal.ResultAmount ?? 0;

                if (existing != null)
                {
                    if (syncAmount > 0) existing.Amount = syncAmount;
                    else db.SprintGoalLogs.Remove(existing);
                }
                else if (syncAmount > 0)
                {
                    db.SprintGoalLogs.Add(new SprintGoalLog
                    {
                        Id = Guid.NewGuid(), SprintGoalId = goal.SprintGoalId.Value,
                        Date = weekDate, Amount = syncAmount
                    });
                }
            }

            await db.SaveChangesAsync(); return Results.NoContent();
        });

        weeks.MapDelete("/{date}/goals/{goalId:guid}", async (Guid roadmapId, string date, Guid goalId, RoadmapDbContext db) =>
        {
            var goal = await db.WeekPlanGoals.Include(g => g.WeekPlan).FirstOrDefaultAsync(g => g.Id == goalId && g.WeekPlan.RoadmapId == roadmapId);
            if (goal is null) return Results.NotFound(); db.WeekPlanGoals.Remove(goal); await db.SaveChangesAsync(); return Results.NoContent();
        });

        weeks.MapPatch("/{date}/close", async (Guid roadmapId, string date, CloseWeekPlanRequest? req, RoadmapDbContext db) =>
        {
            if (!DateOnly.TryParse(date, out var pd)) return Results.BadRequest("Invalid date.");
            var dow = ((int)pd.DayOfWeek + 6) % 7; var monday = pd.AddDays(-dow);
            var plan = await db.WeekPlans.FirstOrDefaultAsync(w => w.RoadmapId == roadmapId && w.WeekStart == monday);
            if (plan is null) return Results.NotFound();
            plan.IsClosed = !plan.IsClosed; if (req?.Notes != null) plan.Notes = req.Notes;
            await db.SaveChangesAsync(); return Results.NoContent();
        });

        // ===== Relax Days =====
        app.MapPost("/api/roadmaps/{roadmapId:guid}/relaxdays/{date}", async (Guid roadmapId, string date, RoadmapDbContext db) =>
        {
            if (!DateOnly.TryParse(date, out var pd)) return Results.BadRequest("Invalid date.");
            var existing = await db.RelaxDays.FirstOrDefaultAsync(r => r.RoadmapId == roadmapId && r.Date == pd);
            if (existing != null) { db.RelaxDays.Remove(existing); await db.SaveChangesAsync(); return Results.Ok(new { isRelaxDay = false }); }
            db.RelaxDays.Add(new RelaxDay { Id = Guid.NewGuid(), RoadmapId = roadmapId, Date = pd });
            await db.SaveChangesAsync(); return Results.Ok(new { isRelaxDay = true });
        });

        // ===== Schedule Blocks =====
        var sblocks = app.MapGroup("/api/roadmaps/{roadmapId:guid}/blocks").WithTags("ScheduleBlocks");

        sblocks.MapGet("/", async (Guid roadmapId, RoadmapDbContext db) =>
        {
            var blocks = await db.ScheduleBlocks.AsNoTracking().Include(sb => sb.Items.OrderBy(i => i.BlockSortOrder))
                .Where(sb => sb.RoadmapId == roadmapId).OrderBy(sb => sb.SortOrder).ToListAsync();
            return Results.Ok(blocks.Select(sb => new ScheduleBlockDefDto(sb.Id, sb.Name, sb.ScheduleTemplate, sb.SortOrder,
                sb.Items.Select(i => new ScheduleBlockItemDto(i.Id, i.Title, i.Unit, i.TotalSize, i.UnitsPerHour, i.Status.ToString(), i.BlockSortOrder)).ToList())).ToList());
        });

        sblocks.MapPost("/", async (Guid roadmapId, CreateScheduleBlockRequest req, RoadmapDbContext db) =>
        {
            var maxSort = await db.ScheduleBlocks.Where(sb => sb.RoadmapId == roadmapId).MaxAsync(sb => (int?)sb.SortOrder) ?? -1;
            var sb = new ScheduleBlock { Id = Guid.NewGuid(), RoadmapId = roadmapId, Name = req.Name.Trim(), ScheduleTemplate = req.ScheduleTemplate, SortOrder = maxSort + 1 };
            db.ScheduleBlocks.Add(sb); await db.SaveChangesAsync();
            return Results.Created("", new ScheduleBlockDefDto(sb.Id, sb.Name, sb.ScheduleTemplate, sb.SortOrder, []));
        });

        sblocks.MapPut("/{blockId:guid}", async (Guid roadmapId, Guid blockId, UpdateScheduleBlockRequest req, RoadmapDbContext db) =>
        {
            var sb = await db.ScheduleBlocks.FirstOrDefaultAsync(x => x.Id == blockId && x.RoadmapId == roadmapId);
            if (sb is null) return Results.NotFound();
            sb.Name = req.Name.Trim(); sb.ScheduleTemplate = req.ScheduleTemplate;
            await db.SaveChangesAsync();
            await ReplanStartedSprintsAsync(db, roadmapId);
            return Results.NoContent();
        });

        sblocks.MapDelete("/{blockId:guid}", async (Guid roadmapId, Guid blockId, RoadmapDbContext db) =>
        {
            var sb = await db.ScheduleBlocks.FirstOrDefaultAsync(x => x.Id == blockId && x.RoadmapId == roadmapId);
            if (sb is null) return Results.NotFound();
            // Unlink items (set ScheduleBlockId to null)
            var items = await db.Nodes.Where(n => n.ScheduleBlockId == blockId).ToListAsync();
            foreach (var i in items) { i.ScheduleBlockId = null; i.BlockSortOrder = 0; }
            db.ScheduleBlocks.Remove(sb); await db.SaveChangesAsync();
            await ReplanStartedSprintsAsync(db, roadmapId);
            return Results.NoContent();
        });

        // Assign item to block
        sblocks.MapPost("/{blockId:guid}/items", async (Guid roadmapId, Guid blockId, AssignToBlockRequest req, RoadmapDbContext db) =>
        {
            var sb = await db.ScheduleBlocks.FirstOrDefaultAsync(x => x.Id == blockId && x.RoadmapId == roadmapId);
            if (sb is null) return Results.NotFound();
            var node = await db.Nodes.FirstOrDefaultAsync(n => n.Id == req.NodeId && n.RoadmapId == roadmapId);
            if (node is null) return Results.NotFound();
            // Auto sort order: max + 1
            var maxSort = await db.Nodes.Where(n => n.ScheduleBlockId == blockId).MaxAsync(n => (int?)n.BlockSortOrder) ?? -1;
            node.ScheduleBlockId = blockId; node.BlockSortOrder = maxSort + 1;
            // Clear self-schedule since block provides the schedule
            node.ScheduleTemplate = null;
            // Auto-activate if NotStarted
            if (node.Status == ActionItemStatus.NotStarted)
            {
                var old = node.Status; node.Status = ActionItemStatus.Active;
                db.StatusChanges.Add(new StatusChange { Id = Guid.NewGuid(), RoadmapId = roadmapId, NodeId = node.Id, OldStatus = old, NewStatus = ActionItemStatus.Active, Trigger = "block_assign" });
            }
            await db.SaveChangesAsync();
            await ReplanStartedSprintsAsync(db, roadmapId);
            return Results.NoContent();
        });

        // Remove item from block
        sblocks.MapDelete("/{blockId:guid}/items/{nodeId:guid}", async (Guid roadmapId, Guid blockId, Guid nodeId, RoadmapDbContext db) =>
        {
            var node = await db.Nodes.FirstOrDefaultAsync(n => n.Id == nodeId && n.ScheduleBlockId == blockId);
            if (node is null) return Results.NotFound();
            node.ScheduleBlockId = null; node.BlockSortOrder = 0;
            await db.SaveChangesAsync();
            await ReplanStartedSprintsAsync(db, roadmapId);
            return Results.NoContent();
        });

        // Reorder item within block
        sblocks.MapPatch("/{blockId:guid}/items/{nodeId:guid}/reorder", async (Guid roadmapId, Guid blockId, Guid nodeId, ReorderNodeRequest req, RoadmapDbContext db) =>
        {
            var items = await db.Nodes.Where(n => n.ScheduleBlockId == blockId).OrderBy(n => n.BlockSortOrder).ToListAsync();
            var item = items.FirstOrDefault(n => n.Id == nodeId);
            if (item is null) return Results.NotFound();
            var idx = items.IndexOf(item);
            var newIdx = req.Direction == "up" ? idx - 1 : idx + 1;
            if (newIdx < 0 || newIdx >= items.Count) return Results.BadRequest("Already at edge.");
            (items[idx].BlockSortOrder, items[newIdx].BlockSortOrder) = (items[newIdx].BlockSortOrder, items[idx].BlockSortOrder);
            await db.SaveChangesAsync();
            await ReplanStartedSprintsAsync(db, roadmapId);
            return Results.NoContent();
        });

        // Batch reorder — set full order from an array of node IDs
        sblocks.MapPut("/{blockId:guid}/items/reorder", async (Guid roadmapId, Guid blockId, BatchReorderRequest req, RoadmapDbContext db) =>
        {
            var items = await db.Nodes.Where(n => n.ScheduleBlockId == blockId).ToListAsync();
            var lookup = items.ToDictionary(n => n.Id);
            for (int i = 0; i < req.NodeIds.Count; i++)
            {
                if (lookup.TryGetValue(req.NodeIds[i], out var node))
                    node.BlockSortOrder = i;
            }
            await db.SaveChangesAsync();
            await ReplanStartedSprintsAsync(db, roadmapId);
            return Results.NoContent();
        });

        // ===== Custom Logs =====
        var clogs = app.MapGroup("/api/roadmaps/{roadmapId:guid}/customlogs").WithTags("CustomLogs");

        clogs.MapGet("/", async (Guid roadmapId, RoadmapDbContext db) =>
            Results.Ok(await db.CustomLogs.AsNoTracking().Where(c => c.RoadmapId == roadmapId)
                .OrderByDescending(c => c.Date).ThenByDescending(c => c.CreatedAt)
                .Select(c => new CustomLogDto(c.Id, c.Title, c.Points, c.Date.ToString("yyyy-MM-dd"), c.Note)).ToListAsync()));

        clogs.MapPost("/", async (Guid roadmapId, CreateCustomLogRequest req, RoadmapDbContext db) =>
        {
            if (!DateOnly.TryParse(req.Date, out var pd)) return Results.BadRequest("Invalid date.");
            var c = new CustomLog { Id = Guid.NewGuid(), RoadmapId = roadmapId, Title = req.Title.Trim(), Points = req.Points, Date = pd, Note = req.Note };
            db.CustomLogs.Add(c); await db.SaveChangesAsync();
            return Results.Created("", new CustomLogDto(c.Id, c.Title, c.Points, c.Date.ToString("yyyy-MM-dd"), c.Note));
        });

        clogs.MapDelete("/{logId:guid}", async (Guid roadmapId, Guid logId, RoadmapDbContext db) =>
        {
            var c = await db.CustomLogs.FirstOrDefaultAsync(x => x.Id == logId && x.RoadmapId == roadmapId);
            if (c is null) return Results.NotFound(); db.CustomLogs.Remove(c); await db.SaveChangesAsync(); return Results.NoContent();
        });

        // Subpoints for a checklist node on a given date — templates + per-day check state
        app.MapGet("/api/roadmaps/{roadmapId:guid}/schedule/{date}/subpoints/{nodeId:guid}", async (Guid roadmapId, string date, Guid nodeId, RoadmapDbContext db) =>
        {
            if (!DateOnly.TryParse(date, out var pd)) return Results.BadRequest("Invalid date.");
            if (!await db.Nodes.AnyAsync(n => n.Id == nodeId && n.RoadmapId == roadmapId)) return Results.NotFound();
            var sps = await db.NodeSubPoints.AsNoTracking().Where(s => s.NodeId == nodeId).OrderBy(s => s.SortOrder).ToListAsync();
            var checkedIds = await db.NodeSubPointChecks.AsNoTracking()
                .Where(c => c.Date == pd && sps.Select(s => s.Id).Contains(c.SubPointId))
                .Select(c => c.SubPointId).ToListAsync();
            var checkedSet = new HashSet<Guid>(checkedIds);
            return Results.Ok(sps.Select(s => new ScheduleSubPointDto(s.Id, s.Title, s.SortOrder, checkedSet.Contains(s.Id))));
        });

        // Toggle a subpoint's check state for a date. Auto-creates a WorkLog of 1 unit when all become checked.
        app.MapPatch("/api/roadmaps/{roadmapId:guid}/schedule/{date}/subpoints/{nodeId:guid}/{spId:guid}", async (Guid roadmapId, string date, Guid nodeId, Guid spId, ToggleNodeSubPointRequest req, RoadmapDbContext db) =>
        {
            if (!DateOnly.TryParse(date, out var pd)) return Results.BadRequest("Invalid date.");
            var node = await db.Nodes.FirstOrDefaultAsync(n => n.Id == nodeId && n.RoadmapId == roadmapId);
            if (node is null) return Results.NotFound();
            var sp = await db.NodeSubPoints.FirstOrDefaultAsync(s => s.Id == spId && s.NodeId == nodeId);
            if (sp is null) return Results.NotFound();

            var existing = await db.NodeSubPointChecks.FirstOrDefaultAsync(c => c.SubPointId == spId && c.Date == pd);
            if (req.IsChecked && existing is null)
                db.NodeSubPointChecks.Add(new NodeSubPointCheck { Id = Guid.NewGuid(), SubPointId = spId, Date = pd });
            else if (!req.IsChecked && existing != null)
                db.NodeSubPointChecks.Remove(existing);
            await db.SaveChangesAsync();

            // After toggling: if every subpoint for this node has a check for this date, auto-log 1 unit (if not already logged).
            var allSpIds = await db.NodeSubPoints.Where(s => s.NodeId == nodeId).Select(s => s.Id).ToListAsync();
            if (allSpIds.Count > 0)
            {
                var checkedCount = await db.NodeSubPointChecks.CountAsync(c => allSpIds.Contains(c.SubPointId) && c.Date == pd);
                if (checkedCount == allSpIds.Count)
                {
                    var sprint = await db.Sprints.FirstOrDefaultAsync(s => s.RoadmapId == roadmapId && s.StartDate <= pd && s.EndDate >= pd && s.IsStarted);
                    if (sprint != null)
                    {
                        var existingLog = await db.WorkLogs.FirstOrDefaultAsync(w => w.SprintId == sprint.Id && w.NodeId == nodeId && w.Date == pd);
                        if (existingLog is null)
                        {
                            db.WorkLogs.Add(new WorkLog { Id = Guid.NewGuid(), RoadmapId = roadmapId, SprintId = sprint.Id,
                                NodeId = nodeId, Date = pd, Amount = 1, Note = "via subpoints" });
                            await db.SaveChangesAsync();
                            if (node.TotalSize.HasValue && node.Status != ActionItemStatus.Completed)
                            {
                                var totalLogged = await db.WorkLogs.Where(w => w.NodeId == nodeId).SumAsync(w => w.Amount);
                                if (totalLogged >= node.TotalSize.Value)
                                {
                                    var oldStatus = node.Status;
                                    node.Status = ActionItemStatus.Completed;
                                    db.StatusChanges.Add(new StatusChange { Id = Guid.NewGuid(), RoadmapId = roadmapId, NodeId = nodeId, OldStatus = oldStatus, NewStatus = ActionItemStatus.Completed, Trigger = "auto_completed" });
                                    await ActivateNextInQueue(db, node);
                                    await db.SaveChangesAsync();
                                }
                            }
                        }
                    }
                }
            }
            return Results.NoContent();
        });

        // Get custom logs for a specific date (schedule sidebar)
        app.MapGet("/api/roadmaps/{roadmapId:guid}/schedule/{date}/customlogs", async (Guid roadmapId, string date, RoadmapDbContext db) =>
        {
            if (!DateOnly.TryParse(date, out var pd)) return Results.BadRequest("Invalid date.");
            return Results.Ok(await db.CustomLogs.AsNoTracking().Where(c => c.RoadmapId == roadmapId && c.Date == pd)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new CustomLogDto(c.Id, c.Title, c.Points, c.Date.ToString("yyyy-MM-dd"), c.Note)).ToListAsync());
        });

        // ===== Single Tasks =====
        var tasks = app.MapGroup("/api/roadmaps/{roadmapId:guid}/tasks").WithTags("Tasks");

        tasks.MapGet("/", async (Guid roadmapId, RoadmapDbContext db) =>
        {
            var all = await db.SingleTasks.AsNoTracking().Where(t => t.RoadmapId == roadmapId)
                .OrderBy(t => t.IsCompleted).ThenBy(t => t.Priority).ThenBy(t => t.CreatedAt).ToListAsync();
            return Results.Ok(all.Select(ToTaskDto).ToList());
        });

        tasks.MapPost("/", async (Guid roadmapId, CreateSingleTaskRequest req, RoadmapDbContext db) =>
        {
            if (!DateOnly.TryParse(req.StartDate, out var sd)) return Results.BadRequest("Invalid start date.");
            DateOnly? dd = null;
            if (req.DueDate != null && DateOnly.TryParse(req.DueDate, out var parsed)) dd = parsed;
            if (!Enum.TryParse<TaskPriority>(req.Priority, true, out var pri)) pri = TaskPriority.Medium;
            var t = new SingleTask { Id = Guid.NewGuid(), RoadmapId = roadmapId, Title = req.Title.Trim(),
                Priority = pri, EstimatedHours = req.EstimatedHours, Weekdays = req.Weekdays,
                StartDate = sd, DueDate = dd };
            db.SingleTasks.Add(t); await db.SaveChangesAsync();
            return Results.Created("", ToTaskDto(t));
        });

        tasks.MapPut("/{taskId:guid}", async (Guid roadmapId, Guid taskId, UpdateSingleTaskRequest req, RoadmapDbContext db) =>
        {
            var t = await db.SingleTasks.FirstOrDefaultAsync(x => x.Id == taskId && x.RoadmapId == roadmapId);
            if (t is null) return Results.NotFound();
            if (!DateOnly.TryParse(req.StartDate, out var sd)) return Results.BadRequest("Invalid start date.");
            DateOnly? dd = null;
            if (req.DueDate != null && DateOnly.TryParse(req.DueDate, out var parsed)) dd = parsed;
            if (Enum.TryParse<TaskPriority>(req.Priority, true, out var pri)) t.Priority = pri;
            t.Title = req.Title.Trim(); t.EstimatedHours = req.EstimatedHours; t.Weekdays = req.Weekdays;
            t.StartDate = sd; t.DueDate = dd;
            await db.SaveChangesAsync(); return Results.NoContent();
        });

        tasks.MapDelete("/{taskId:guid}", async (Guid roadmapId, Guid taskId, RoadmapDbContext db) =>
        {
            var t = await db.SingleTasks.FirstOrDefaultAsync(x => x.Id == taskId && x.RoadmapId == roadmapId);
            if (t is null) return Results.NotFound(); db.SingleTasks.Remove(t); await db.SaveChangesAsync(); return Results.NoContent();
        });

        // Complete a task (credits points to the given date)
        tasks.MapPatch("/{taskId:guid}/complete", async (Guid roadmapId, Guid taskId, CompleteTaskRequest req, RoadmapDbContext db) =>
        {
            var t = await db.SingleTasks.FirstOrDefaultAsync(x => x.Id == taskId && x.RoadmapId == roadmapId);
            if (t is null) return Results.NotFound();
            if (!DateOnly.TryParse(req.Date, out var cd)) return Results.BadRequest("Invalid date.");
            t.IsCompleted = true; t.CompletedDate = cd;
            await db.SaveChangesAsync(); return Results.NoContent();
        });

        // Uncomplete a task
        tasks.MapPatch("/{taskId:guid}/uncomplete", async (Guid roadmapId, Guid taskId, RoadmapDbContext db) =>
        {
            var t = await db.SingleTasks.FirstOrDefaultAsync(x => x.Id == taskId && x.RoadmapId == roadmapId);
            if (t is null) return Results.NotFound();
            t.IsCompleted = false; t.CompletedDate = null;
            await db.SaveChangesAsync(); return Results.NoContent();
        });

        // Delay a task by 3 days from today
        tasks.MapPatch("/{taskId:guid}/delay", async (Guid roadmapId, Guid taskId, RoadmapDbContext db) =>
        {
            var t = await db.SingleTasks.FirstOrDefaultAsync(x => x.Id == taskId && x.RoadmapId == roadmapId);
            if (t is null) return Results.NotFound();
            t.DelayedUntil = AppClock.Today().AddDays(3);
            await db.SaveChangesAsync(); return Results.NoContent();
        });

        // Get tasks selected for a specific date (schedule view)
        app.MapGet("/api/roadmaps/{roadmapId:guid}/schedule/{date}/tasks", async (Guid roadmapId, string date, RoadmapDbContext db) =>
        {
            if (!DateOnly.TryParse(date, out var pd)) return Results.BadRequest("Invalid date.");
            var dow = (int)pd.DayOfWeek;
            var today = AppClock.Today();

            var candidates = await db.SingleTasks.AsNoTracking()
                .Where(t => t.RoadmapId == roadmapId && !t.IsCompleted && t.StartDate <= pd
                    && (t.DelayedUntil == null || t.DelayedUntil <= pd))
                .OrderBy(t => t.Priority).ThenBy(t => t.CreatedAt).ToListAsync();

            // Also include tasks completed on this specific date
            var completedToday = await db.SingleTasks.AsNoTracking()
                .Where(t => t.RoadmapId == roadmapId && t.IsCompleted && t.CompletedDate == pd).ToListAsync();

            // Filter by weekday
            var eligible = candidates.Where(t => {
                if (string.IsNullOrEmpty(t.Weekdays)) return true;
                try { var days = System.Text.Json.JsonSerializer.Deserialize<int[]>(t.Weekdays); return days?.Contains(dow) ?? true; }
                catch { return true; }
            }).ToList();

            // Select tasks fitting within 3hr cap
            var selected = new List<SingleTask>();
            double totalHours = 0;
            foreach (var t in eligible)
            {
                if (totalHours + t.EstimatedHours <= 3 || selected.Count == 0)
                {
                    selected.Add(t);
                    totalHours += t.EstimatedHours;
                    if (totalHours >= 3) break;
                }
            }

            // Merge with completed-today tasks (avoid duplicates)
            var selectedIds = selected.Select(s => s.Id).ToHashSet();
            foreach (var ct in completedToday.Where(ct => !selectedIds.Contains(ct.Id)))
                selected.Add(ct);

            return Results.Ok(selected.Select(t => new ScheduleTaskDto(t.Id, t.Title, t.Priority.ToString(),
                t.EstimatedHours, Math.Floor(t.EstimatedHours * 2),
                t.IsCompleted, t.DueDate?.ToString("yyyy-MM-dd"),
                t.DueDate.HasValue && t.DueDate.Value < today && !t.IsCompleted)).ToList());
        });

        // ===== Habits =====
        var habits = app.MapGroup("/api/roadmaps/{roadmapId:guid}/habits").WithTags("Habits");

        // Global habit library
        habits.MapGet("/", async (Guid roadmapId, RoadmapDbContext db) =>
            Results.Ok(await db.Habits.AsNoTracking().Where(h => h.RoadmapId == roadmapId).OrderBy(h => h.CreatedAt)
                .Select(h => new HabitDto(h.Id, h.Name, h.CreatedAt)).ToListAsync()));

        habits.MapPost("/", async (Guid roadmapId, CreateHabitRequest req, RoadmapDbContext db) =>
        {
            var h = new Habit { Id = Guid.NewGuid(), RoadmapId = roadmapId, Name = req.Name.Trim() };
            db.Habits.Add(h); await db.SaveChangesAsync();
            return Results.Created("", new HabitDto(h.Id, h.Name, h.CreatedAt));
        });

        habits.MapDelete("/{habitId:guid}", async (Guid roadmapId, Guid habitId, RoadmapDbContext db) =>
        {
            var h = await db.Habits.FirstOrDefaultAsync(x => x.Id == habitId && x.RoadmapId == roadmapId);
            if (h is null) return Results.NotFound(); db.Habits.Remove(h); await db.SaveChangesAsync(); return Results.NoContent();
        });

        // Sprint-habit links
        var sprintHabits = app.MapGroup("/api/roadmaps/{roadmapId:guid}/sprints/{sprintId:guid}/habits").WithTags("SprintHabits");

        sprintHabits.MapGet("/", async (Guid roadmapId, Guid sprintId, RoadmapDbContext db) =>
        {
            var sprint = await db.Sprints.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sprintId && s.RoadmapId == roadmapId);
            if (sprint is null) return Results.NotFound();
            var shs = await db.SprintHabits.AsNoTracking().Include(sh => sh.Habit).Include(sh => sh.Checks)
                .Where(sh => sh.SprintId == sprintId).ToListAsync();
            var dates = new List<DateOnly>();
            for (var d = sprint.StartDate; d <= sprint.EndDate; d = d.AddDays(1)) dates.Add(d);
            return Results.Ok(shs.Select(sh => BuildSprintHabitDto(sh, dates)).ToList());
        });

        sprintHabits.MapPost("/", async (Guid roadmapId, Guid sprintId, AddSprintHabitRequest req, RoadmapDbContext db) =>
        {
            if (!await db.Habits.AnyAsync(h => h.Id == req.HabitId && h.RoadmapId == roadmapId)) return Results.NotFound();
            var existing = await db.SprintHabits.FirstOrDefaultAsync(sh => sh.SprintId == sprintId && sh.HabitId == req.HabitId);
            if (existing != null)
            {
                // If paused, resume it
                if (existing.IsPaused) { existing.IsPaused = false; await db.SaveChangesAsync(); return Results.Ok(existing.Id); }
                return Results.Conflict("Already tracked");
            }
            var sh = new SprintHabit { Id = Guid.NewGuid(), SprintId = sprintId, HabitId = req.HabitId };
            db.SprintHabits.Add(sh); await db.SaveChangesAsync();
            return Results.Created("", sh.Id);
        });

        // Pause (untrack) — keeps the record, zeroes checks, hides from schedule
        sprintHabits.MapPatch("/{sprintHabitId:guid}/pause", async (Guid roadmapId, Guid sprintId, Guid sprintHabitId, RoadmapDbContext db) =>
        {
            var sh = await db.SprintHabits.Include(x => x.Checks).FirstOrDefaultAsync(x => x.Id == sprintHabitId && x.SprintId == sprintId);
            if (sh is null) return Results.NotFound();
            sh.IsPaused = true;
            db.HabitChecks.RemoveRange(sh.Checks); // zero the tracked results
            await db.SaveChangesAsync(); return Results.NoContent();
        });

        // Resume a paused habit
        sprintHabits.MapPatch("/{sprintHabitId:guid}/resume", async (Guid roadmapId, Guid sprintId, Guid sprintHabitId, RoadmapDbContext db) =>
        {
            var sh = await db.SprintHabits.FirstOrDefaultAsync(x => x.Id == sprintHabitId && x.SprintId == sprintId);
            if (sh is null) return Results.NotFound();
            sh.IsPaused = false;
            await db.SaveChangesAsync(); return Results.NoContent();
        });

        // Delete entirely (permanent removal)
        sprintHabits.MapDelete("/{sprintHabitId:guid}", async (Guid roadmapId, Guid sprintId, Guid sprintHabitId, RoadmapDbContext db) =>
        {
            var sh = await db.SprintHabits.FirstOrDefaultAsync(x => x.Id == sprintHabitId && x.SprintId == sprintId);
            if (sh is null) return Results.NotFound(); db.SprintHabits.Remove(sh); await db.SaveChangesAsync(); return Results.NoContent();
        });

        // Toggle habit check for a date
        sprintHabits.MapPut("/{sprintHabitId:guid}/check", async (Guid roadmapId, Guid sprintId, Guid sprintHabitId, ToggleHabitCheckRequest req, RoadmapDbContext db) =>
        {
            if (!DateOnly.TryParse(req.Date, out var pd)) return Results.BadRequest("Invalid date.");
            var sh = await db.SprintHabits.FirstOrDefaultAsync(x => x.Id == sprintHabitId && x.SprintId == sprintId);
            if (sh is null) return Results.NotFound();
            var ex = await db.HabitChecks.FirstOrDefaultAsync(c => c.SprintHabitId == sprintHabitId && c.Date == pd);
            if (ex != null) { ex.IsChecked = req.IsChecked; }
            else db.HabitChecks.Add(new HabitCheck { Id = Guid.NewGuid(), SprintHabitId = sprintHabitId, Date = pd, IsChecked = req.IsChecked });
            await db.SaveChangesAsync(); return Results.NoContent();
        });

        // Schedule-page view: habits for a specific date
        app.MapGet("/api/roadmaps/{roadmapId:guid}/schedule/{date}/habits", async (Guid roadmapId, string date, RoadmapDbContext db) =>
        {
            if (!DateOnly.TryParse(date, out var pd)) return Results.BadRequest("Invalid date.");
            var sprint = await db.Sprints.AsNoTracking().FirstOrDefaultAsync(s => s.RoadmapId == roadmapId && s.StartDate <= pd && s.EndDate >= pd);
            if (sprint is null) return Results.Ok(new List<ScheduleHabitDto>());
            var shs = await db.SprintHabits.AsNoTracking().Include(sh => sh.Habit).Include(sh => sh.Checks)
                .Where(sh => sh.SprintId == sprint.Id && !sh.IsPaused).ToListAsync();
            var dates = new List<DateOnly>();
            for (var d = sprint.StartDate; d <= pd; d = d.AddDays(1)) dates.Add(d);
            return Results.Ok(shs.Select(sh =>
            {
                var todayCheck = sh.Checks.FirstOrDefault(c => c.Date == pd);
                var (current, _, formed) = ComputeStreak(sh.Checks, dates);
                return new ScheduleHabitDto(sh.Id, sh.HabitId, sh.Habit.Name, todayCheck?.IsChecked ?? false, current, formed);
            }).ToList());
        });

        // ===== Job scouting (global — postings imported from the Finder pipeline) =====
        // Read-only: writes happen through the MCP tools, which is how the scout feeds in.
        var jobs = app.MapGroup("/api/job-runs").WithTags("JobRuns").RequireAuthorization();

        // Day list for the tab's date picker — no posting bodies, just the summaries.
        jobs.MapGet("/", async (RoadmapDbContext db) =>
        {
            var list = await db.JobRuns.AsNoTracking()
                .OrderByDescending(r => r.RunDate)
                .Select(r => new JobRunSummaryDto(r.Id, r.RunDate.ToString("yyyy-MM-dd"), r.Queries,
                    r.MaxAgeDays, r.RawCount, r.Postings.Count, r.CreatedAt))
                .ToListAsync();
            return Results.Ok(list);
        });

        // What the tab opens by default.
        jobs.MapGet("/latest", async (RoadmapDbContext db) =>
        {
            var run = await db.JobRuns.AsNoTracking().Include(r => r.Postings)
                .OrderByDescending(r => r.RunDate).FirstOrDefaultAsync();
            return run is null ? Results.NotFound() : Results.Ok(ToJobRunDto(run));
        });

        jobs.MapGet("/{date}", async (string date, RoadmapDbContext db) =>
        {
            if (!DateOnly.TryParse(date, out var d)) return Results.BadRequest("Invalid date. Use YYYY-MM-DD.");
            var run = await db.JobRuns.AsNoTracking().Include(r => r.Postings)
                .FirstOrDefaultAsync(r => r.RunDate == d);
            return run is null ? Results.NotFound() : Results.Ok(ToJobRunDto(run));
        });

        // Binary download for a posting's tailored CV PDF. Kept out of the list JSON
        // (only HasCv ships there); this streams the bytea on demand. Auth is the
        // group's Bearer requirement — the frontend fetches it with the token and
        // triggers a blob download.
        jobs.MapGet("/postings/{postingId:guid}/cv", async (Guid postingId, RoadmapDbContext db) =>
        {
            var p = await db.JobPostings.AsNoTracking()
                .Where(x => x.Id == postingId)
                .Select(x => new { x.TailoredCvPdf, x.Company })
                .FirstOrDefaultAsync();
            if (p?.TailoredCvPdf is null || p.TailoredCvPdf.Length == 0) return Results.NotFound();
            var slug = new string((p.Company ?? "cv").ToLowerInvariant()
                .Where(c => char.IsLetterOrDigit(c) || c is '-').ToArray());
            return Results.File(p.TailoredCvPdf, "application/pdf",
                $"cv-{(slug.Length == 0 ? "posting" : slug)}.pdf");
        });

        // Attach (or replace) one posting's tailored CV via multipart upload. This exists
        // because import_job_run inlines CVs as base64 and replaces the whole day in a single
        // call — a run's worth of ~200 KB PDFs is too large to carry that way. Uploading per
        // posting streams the bytes from disk (curl -F), so payload size stops being a limit.
        // Idempotent: re-uploading overwrites the posting's CV. Multipart fields:
        //   file         — the tailored CV PDF (required)
        //   cv_changes   — one-line summary of what the CV changed vs. the master (optional)
        //   cv_fit_score — CV-vs-JD fit, 0–100 (optional)
        //   cv_fit_gaps  — JSON array of {label, points, note} gaps, highest-impact first (optional)
        jobs.MapPost("/postings/{postingId:guid}/cv", async (Guid postingId, HttpRequest request, RoadmapDbContext db) =>
        {
            if (!request.HasFormContentType) return Results.BadRequest("Expected multipart/form-data with a 'file' field.");
            var form = await request.ReadFormAsync();
            var file = form.Files["file"];
            if (file is null || file.Length == 0) return Results.BadRequest("Missing 'file' (the tailored CV PDF).");
            if (file.Length > 5 * 1024 * 1024) return Results.BadRequest("CV PDF exceeds 5 MB.");

            var posting = await db.JobPostings.FirstOrDefaultAsync(x => x.Id == postingId);
            if (posting is null) return Results.NotFound();

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            posting.TailoredCvPdf = ms.ToArray();

            var changes = form["cv_changes"].ToString();
            if (!string.IsNullOrWhiteSpace(changes)) posting.CvChangeList = changes;

            var fitScoreRaw = form["cv_fit_score"].ToString();
            if (int.TryParse(fitScoreRaw, out var fitScore)) posting.CvFitScore = Math.Clamp(fitScore, 0, 100);

            var fitGaps = form["cv_fit_gaps"].ToString();
            if (!string.IsNullOrWhiteSpace(fitGaps)) posting.CvFitGaps = fitGaps;

            await db.SaveChangesAsync();
            return Results.Ok(new
            {
                postingId = posting.Id,
                company = posting.Company,
                bytes = posting.TailoredCvPdf.Length,
                hasChanges = posting.CvChangeList != null,
                fitScore = posting.CvFitScore,
                gapCount = CvFitGapsJson.Parse(posting.CvFitGaps).Count
            });
        }).DisableAntiforgery();
    }

    private static JobRunDto ToJobRunDto(JobRun r) => new(
        r.Id, r.RunDate.ToString("yyyy-MM-dd"), r.Queries, r.MaxAgeDays, r.RawCount, r.CreatedAt,
        r.Postings.OrderBy(p => p.SortOrder).Select(p => new JobPostingDto(
            p.Id, p.Title, p.Company, p.Url, p.Source, p.Location,
            p.PostedAt?.ToString("yyyy-MM-dd"), p.Description, p.Bucket,
            p.SeniorityClass, p.AiKeywordHits, p.GeoHints, p.Queries,
            p.Score, p.Reasoning, p.SortOrder,
            p.TailoredCvPdf != null && p.TailoredCvPdf.Length > 0, p.CvChangeList,
            p.CvFitScore, CvFitGapsJson.Parse(p.CvFitGaps))).ToList());

    // ===== Habit streak computation =====
    /// <summary>
    /// Compute current streak, best streak, and whether 21-day formation is achieved.
    /// Grace rule: 1 miss allowed per rolling 7-day window.
    /// </summary>
    private static (int Current, int Best, bool Formed) ComputeStreak(List<HabitCheck> checks, List<DateOnly> sprintDates)
    {
        var checkSet = checks.Where(c => c.IsChecked).Select(c => c.Date).ToHashSet();
        int current = 0, best = 0, missesInWindow = 0;
        var window = new Queue<bool>();

        foreach (var d in sprintDates)
        {
            var done = checkSet.Contains(d);
            window.Enqueue(done);
            if (!done) missesInWindow++;

            // Maintain 7-day rolling window
            if (window.Count > 7)
            {
                var removed = window.Dequeue();
                if (!removed) missesInWindow--;
            }

            // Streak breaks if more than 1 miss in the window
            if (missesInWindow > 1)
            {
                best = Math.Max(best, current);
                // Reset: count consecutive from the end of current window
                current = 0;
                // Recount from recent days in window
                foreach (var w in window) { if (w) current++; else current = 0; }
            }
            else
            {
                current++;
            }
        }
        best = Math.Max(best, current);
        return (current, best, best >= 21);
    }

    private static SprintHabitDto BuildSprintHabitDto(SprintHabit sh, List<DateOnly> sprintDates)
    {
        var (current, best, formed) = ComputeStreak(sh.Checks.ToList(), sprintDates);
        return new SprintHabitDto(sh.Id, sh.HabitId, sh.Habit.Name, sh.IsPaused, current, best, formed,
            sprintDates.Select(d => {
                var c = sh.Checks.FirstOrDefault(x => x.Date == d);
                return new HabitCheckDto(d.ToString("yyyy-MM-dd"), c?.IsChecked ?? false);
            }).ToList());
    }

    private record DayOverride(int StartMinute, int DurationMinutes);
    private record TemplateData(List<int> Days, int StartMinute, int DurationMinutes, Dictionary<int, DayOverride>? PerDay)
    {
        public int GetStartMinute(int dow) => PerDay != null && PerDay.TryGetValue(dow, out var o) ? o.StartMinute : StartMinute;
        public int GetDurationMinutes(int dow) => PerDay != null && PerDay.TryGetValue(dow, out var o) ? o.DurationMinutes : DurationMinutes;
    }

    private static TemplateData? ParseTemplate(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json); var r = doc.RootElement;
            var days = r.GetProperty("days").EnumerateArray().Select(e => e.GetInt32()).ToList();
            var startMinute = r.GetProperty("startMinute").GetInt32();
            var durationMinutes = r.GetProperty("durationMinutes").GetInt32();
            Dictionary<int, DayOverride>? perDay = null;
            if (r.TryGetProperty("perDay", out var pd) && pd.ValueKind == JsonValueKind.Object)
            {
                perDay = new Dictionary<int, DayOverride>();
                foreach (var prop in pd.EnumerateObject())
                {
                    if (int.TryParse(prop.Name, out var dayNum))
                    {
                        var sm = prop.Value.TryGetProperty("startMinute", out var smv) ? smv.GetInt32() : startMinute;
                        var dm = prop.Value.TryGetProperty("durationMinutes", out var dmv) ? dmv.GetInt32() : durationMinutes;
                        perDay[dayNum] = new DayOverride(sm, dm);
                    }
                }
            }
            return new TemplateData(days, startMinute, durationMinutes, perDay);
        }
        catch { return null; }
    }

    private static SprintDto ToSprintDto(Sprint s) => new(s.Id, s.Name,
        s.StartDate.ToString("yyyy-MM-dd"), s.EndDate.ToString("yyyy-MM-dd"), s.IsOpen, s.IsStarted, s.RelaxDays);

    private static SingleTaskDto ToTaskDto(SingleTask t) => new(t.Id, t.Title, t.Priority.ToString(),
        t.EstimatedHours, t.Weekdays, t.StartDate.ToString("yyyy-MM-dd"),
        t.DueDate?.ToString("yyyy-MM-dd"), t.DelayedUntil?.ToString("yyyy-MM-dd"),
        t.IsCompleted, t.CompletedDate?.ToString("yyyy-MM-dd"), Math.Floor(t.EstimatedHours * 2));

    private static double GetRemaining(RoadmapNode item, Dictionary<Guid, double> allTimeLogged)
    {
        if (!item.TotalSize.HasValue) return double.MaxValue;
        var logged = allTimeLogged.GetValueOrDefault(item.Id, 0);
        return Math.Max(0, item.TotalSize.Value - logged);
    }

    /// <summary>
    /// Lightweight plan entry used for both snapshot persistence and on-the-fly projection.
    /// </summary>
    private record ComputedPlanEntry(Guid NodeId, DateOnly Date, int StartMinute, int DurationMinutes, double PlannedUnits);

    /// <summary>
    /// Compute queue-aware, capped plan entries for a sprint date range.
    /// Uses schedule blocks for queued items and self-scheduled items for the rest.
    ///
    /// <paramref name="completionBoundaries"/> switches this into re-plan mode. Without it
    /// (Start Sprint, draft projection) completed items are simply dropped. With it, an item
    /// completed on or after the first date still owns its slots up to its completion day —
    /// so hitting Complete today leaves today's session with that item and hands the next
    /// queued item the *following* scheduled day, matching the daily schedule view.
    /// </summary>
    private static List<ComputedPlanEntry> ComputeSprintPlan(
        List<RoadmapNode> allNodes, List<ScheduleBlock> blocks, List<DateOnly> dates, Dictionary<Guid, double> allTimeLogged,
        HashSet<string>? relaxDays = null, Dictionary<Guid, DateOnly>? completionBoundaries = null)
    {
        var entries = new List<ComputedPlanEntry>();
        if (dates.Count == 0) return entries;

        var blockItemIds = new HashSet<Guid>();
        var relaxSet = relaxDays ?? [];
        var planFrom = dates[0];
        var beforePlan = planFrom > DateOnly.MinValue ? planFrom.AddDays(-1) : DateOnly.MinValue;

        // The day a completed item stops occupying the schedule, or null when the item is
        // still open. Completions that landed before the re-planned window free their slots
        // immediately; completions from planFrom onward keep them through the completion day.
        DateOnly? OccupiedThrough(RoadmapNode n)
        {
            if (n.Status != ActionItemStatus.Completed) return null;
            if (completionBoundaries is null) return beforePlan;
            return completionBoundaries.TryGetValue(n.Id, out var b) ? b : beforePlan;
        }

        // Schedule block queues
        foreach (var block in blocks)
        {
            var blockTmpl = ParseTemplate(block.ScheduleTemplate); if (blockTmpl is null) continue;
            var queue = block.Items
                .Where(n => n.IsActionable && (n.Status == ActionItemStatus.Active || n.Status == ActionItemStatus.NotStarted
                    || (n.Status == ActionItemStatus.Completed && OccupiedThrough(n) >= planFrom)))
                .OrderBy(n => n.BlockSortOrder).ToList();
            if (queue.Count == 0) continue;

            foreach (var item in queue) blockItemIds.Add(item.Id);

            int qi = 0;
            double remainingForCurrent = GetRemaining(queue[qi], allTimeLogged);

            foreach (var date in dates)
            {
                var ddow = (int)date.DayOfWeek;
                if (!blockTmpl.Days.Contains(ddow)) continue;
                if (relaxSet.Contains(date.ToString("yyyy-MM-dd"))) continue;

                // Step past items whose completion day has passed — the next one takes over here.
                while (qi < queue.Count && OccupiedThrough(queue[qi]) is DateOnly done && date > done)
                {
                    qi++;
                    if (qi < queue.Count) remainingForCurrent = GetRemaining(queue[qi], allTimeLogged);
                }
                if (qi >= queue.Count) break;

                var item = queue[qi];
                var dur = blockTmpl.GetDurationMinutes(ddow);
                var rawPlanned = item.UnitsPerHour.HasValue ? (dur / 60.0) * item.UnitsPerHour.Value : 0;
                // A closed item keeps the session it was scheduled for; "remaining" is
                // meaningless once it is done (it may have been completed short of its size).
                var isClosed = item.Status == ActionItemStatus.Completed;
                var actualPlanned = isClosed ? rawPlanned : Math.Min(rawPlanned, remainingForCurrent);

                if (actualPlanned > 0)
                    entries.Add(new ComputedPlanEntry(item.Id, date, blockTmpl.GetStartMinute(ddow), dur, Math.Round(actualPlanned, 2)));

                if (isClosed) continue;

                remainingForCurrent -= actualPlanned;

                // If this item is done, move to next in queue — next item starts NEXT scheduled day
                if (remainingForCurrent <= 0.01 && qi + 1 < queue.Count)
                {
                    qi++;
                    remainingForCurrent = GetRemaining(queue[qi], allTimeLogged);
                }
            }
        }

        // Self-scheduled items (have their own ScheduleTemplate, NOT in any block)
        foreach (var n in allNodes.Where(n => n.IsActionable && n.ScheduleTemplate != null
            && !blockItemIds.Contains(n.Id)
            && (n.Status == ActionItemStatus.Active || n.Status == ActionItemStatus.NotStarted
                || (n.Status == ActionItemStatus.Completed && OccupiedThrough(n) >= planFrom))))
        {
            var tmpl = ParseTemplate(n.ScheduleTemplate); if (tmpl is null) continue;
            var occupiedThrough = OccupiedThrough(n);
            var remaining = GetRemaining(n, allTimeLogged);
            foreach (var date in dates)
            {
                if (occupiedThrough.HasValue) { if (date > occupiedThrough.Value) break; }
                else if (remaining <= 0.01) break;
                var ddow = (int)date.DayOfWeek;
                if (!tmpl.Days.Contains(ddow)) continue;
                if (relaxSet.Contains(date.ToString("yyyy-MM-dd"))) continue;
                var dur = tmpl.GetDurationMinutes(ddow);
                var rawPlanned = n.UnitsPerHour.HasValue ? (dur / 60.0) * n.UnitsPerHour.Value : 0;
                var actualPlanned = occupiedThrough.HasValue ? rawPlanned : Math.Min(rawPlanned, remaining);
                entries.Add(new ComputedPlanEntry(n.Id, date, tmpl.GetStartMinute(ddow), dur, Math.Round(actualPlanned, 2)));
                if (!occupiedThrough.HasValue) remaining -= actualPlanned;
            }
        }

        return entries;
    }

    /// <summary>
    /// Work finished strictly before <paramref name="from"/>, per node — the only work that
    /// reduces what is left to schedule from <paramref name="from"/> onward.
    ///
    /// Work logged *inside* the planning window must not be counted here: those days still
    /// carry their own planned sessions, so subtracting the same work from the remainder as
    /// well double-counts it and shaves a session off the plan every time something is logged.
    /// </summary>
    private static async Task<Dictionary<Guid, double>> LoggedBeforeAsync(
        RoadmapDbContext db, Guid roadmapId, DateOnly from) =>
        await db.WorkLogs.AsNoTracking().Where(w => w.RoadmapId == roadmapId && w.Date < from)
            .GroupBy(w => w.NodeId).Select(g => new { g.Key, Total = g.Sum(w => w.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Total);

    /// <summary>
    /// Relax days are stored on the sprint as a JSON array of "yyyy-MM-dd" strings.
    /// </summary>
    private static HashSet<string> ParseRelaxDays(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        try { return JsonSerializer.Deserialize<HashSet<string>>(json) ?? []; } catch { return []; }
    }

    /// <summary>
    /// The last day each completed item still occupies the schedule: its completion date,
    /// extended to its last logged work day when that came later.
    /// </summary>
    private static async Task<Dictionary<Guid, DateOnly>> LoadCompletionBoundariesAsync(RoadmapDbContext db, Guid roadmapId)
    {
        var completions = await db.StatusChanges.AsNoTracking()
            .Where(s => s.RoadmapId == roadmapId && s.NewStatus == ActionItemStatus.Completed)
            .Select(s => new { s.NodeId, s.ChangedAt }).ToListAsync();
        var boundaries = completions.GroupBy(s => s.NodeId)
            .ToDictionary(g => g.Key, g => AppClock.ToLocalDate(g.Max(x => x.ChangedAt)));

        var lastLogged = await db.WorkLogs.AsNoTracking().Where(w => w.RoadmapId == roadmapId)
            .GroupBy(w => w.NodeId).Select(g => new { g.Key, Last = g.Max(w => w.Date) }).ToListAsync();
        foreach (var l in lastLogged)
            if (boundaries.TryGetValue(l.Key, out var b) && l.Last > b) boundaries[l.Key] = l.Last;

        return boundaries;
    }

    /// <summary>
    /// Rebuild the still-provisional half of every running (and not-yet-reached) sprint's plan.
    ///
    /// Days before today are frozen history and are never rewritten — a session you were
    /// planned to do and skipped stays on the record. Today onward is recomputed from the
    /// items' current state: sizes, all work logged so far, completions, and queue order. So
    /// finishing an item early hands its remaining sessions to whatever queues behind it, and
    /// growing an item's size pushes the followers back. Sprints that have already ended have
    /// no re-plannable days left and are skipped.
    ///
    /// Call after any write that can change what is planned; it is a no-op when nothing moved.
    /// </summary>
    internal static async Task ReplanStartedSprintsAsync(RoadmapDbContext db, Guid roadmapId)
    {
        var today = AppClock.Today();
        var sprints = await db.Sprints.AsNoTracking()
            .Where(s => s.RoadmapId == roadmapId && s.IsStarted && s.EndDate >= today).ToListAsync();
        if (sprints.Count == 0) return;

        var allNodes = await db.Nodes.AsNoTracking().Where(n => n.RoadmapId == roadmapId)
            .OrderBy(n => n.SortOrder).ToListAsync();
        var blocks = await db.ScheduleBlocks.AsNoTracking().Include(sb => sb.Items)
            .Where(sb => sb.RoadmapId == roadmapId).ToListAsync();
        var boundaries = await LoadCompletionBoundariesAsync(db, roadmapId);

        foreach (var sprint in sprints)
        {
            var from = today > sprint.StartDate ? today : sprint.StartDate;

            var stale = await db.SprintPlanEntries
                .Where(p => p.SprintId == sprint.Id && p.Date >= from).ToListAsync();
            db.SprintPlanEntries.RemoveRange(stale);

            var dates = new List<DateOnly>();
            for (var d = from; d <= sprint.EndDate; d = d.AddDays(1)) dates.Add(d);
            if (dates.Count == 0) continue;

            var computed = ComputeSprintPlan(allNodes, blocks, dates,
                await LoggedBeforeAsync(db, roadmapId, from),
                ParseRelaxDays(sprint.RelaxDays), boundaries);

            db.SprintPlanEntries.AddRange(computed.Select(c => new SprintPlanEntry
            {
                Id = Guid.NewGuid(), SprintId = sprint.Id, NodeId = c.NodeId, CategoryId = null,
                Date = c.Date, StartMinute = c.StartMinute, DurationMinutes = c.DurationMinutes,
                PlannedUnits = c.PlannedUnits
            }));
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Project which item in a block queue should play on a target date.
    /// Walks from baseDate forward, consuming sessions for each item until we reach targetDate.
    /// </summary>
    private static RoadmapNode? ProjectBlockQueueToDate(List<RoadmapNode> queue, TemplateData blockTmpl,
        DateOnly targetDate, DateOnly baseDate, Dictionary<Guid, HashSet<DateOnly>> workLogDates,
        Dictionary<Guid, DateOnly> completionDates, DateOnly today)
    {
        if (queue.Count == 0) return null;
        var itemSessions = new List<(RoadmapNode Node, int SessionsNeeded)>();
        foreach (var item in queue)
        {
            // Completed items keep their slot; they're advanced past by completion date, not sessions.
            if (item.Status == ActionItemStatus.Completed)
            { itemSessions.Add((item, 0)); continue; }
            if (!item.TotalSize.HasValue || !item.UnitsPerHour.HasValue || item.UnitsPerHour.Value == 0)
            { itemSessions.Add((item, int.MaxValue)); continue; }
            var dur = blockTmpl.DurationMinutes;
            var unitsPerSession = (dur / 60.0) * item.UnitsPerHour.Value;
            // Use total sessions (not remaining) — past sessions are tracked via work log dates below
            var sessionsNeeded = unitsPerSession > 0 ? (int)Math.Ceiling(item.TotalSize.Value / unitsPerSession) : 0;
            if (sessionsNeeded <= 0) continue;
            itemSessions.Add((item, sessionsNeeded));
        }
        if (itemSessions.Count == 0) return null;
        int queueIdx = 0; int sessionsConsumed = 0;
        var maxDate = baseDate.AddDays(365);
        for (var d = baseDate; d <= maxDate && d <= targetDate.AddDays(1); d = d.AddDays(1))
        {
            if (!blockTmpl.Days.Contains((int)d.DayOfWeek)) continue;
            // Advance past completed items only once we're strictly past their completion boundary,
            // so the completion day and earlier still show the completed task — the next task in the
            // queue only takes over the following scheduled day.
            while (queueIdx < itemSessions.Count
                   && itemSessions[queueIdx].Node.Status == ActionItemStatus.Completed
                   && d > CompletionBoundary(itemSessions[queueIdx].Node, completionDates, workLogDates, baseDate))
            { queueIdx++; sessionsConsumed = 0; }
            if (queueIdx >= itemSessions.Count) return null;
            if (d == targetDate) return itemSessions[queueIdx].Node;
            var current = itemSessions[queueIdx].Node;
            if (current.Status == ActionItemStatus.Completed) continue; // handled by the while-loop above
            // For past days: only consume a session if work was actually logged on that day.
            // For today and future days: assume work will happen (optimistic calendar projection).
            bool workedThisDay = d >= today || (workLogDates.TryGetValue(current.Id, out var dates) && dates.Contains(d));
            if (workedThisDay)
            {
                sessionsConsumed++;
                if (sessionsConsumed >= itemSessions[queueIdx].SessionsNeeded)
                { queueIdx++; sessionsConsumed = 0; }
            }
        }
        return queueIdx < itemSessions.Count ? itemSessions[queueIdx].Node : null;
    }

    /// <summary>
    /// The last day a completed task should still occupy the schedule: its completion date,
    /// extended to its last logged work day if that came later. The queue advances to the next
    /// task on the first scheduled day AFTER this boundary.
    /// </summary>
    private static DateOnly CompletionBoundary(RoadmapNode n, Dictionary<Guid, DateOnly> completionDates,
        Dictionary<Guid, HashSet<DateOnly>> workLogDates, DateOnly baseDate)
    {
        DateOnly? boundary = null;
        if (completionDates.TryGetValue(n.Id, out var c)) boundary = c;
        if (workLogDates.TryGetValue(n.Id, out var logs) && logs.Count > 0)
        { var last = logs.Max(); if (boundary is null || last > boundary) boundary = last; }
        // No completion record and no logs: fall back to pre-start so it's advanced immediately.
        return boundary ?? baseDate.AddDays(-1);
    }

    internal static async Task ActivateNextInQueue(RoadmapDbContext db, RoadmapNode completedNode)
    {
        // Check block queue first
        if (completedNode.ScheduleBlockId.HasValue)
        {
            var blockSiblings = await db.Nodes.Where(n => n.ScheduleBlockId == completedNode.ScheduleBlockId && n.IsActionable && n.Id != completedNode.Id
                && n.Status != ActionItemStatus.Completed && n.Status != ActionItemStatus.Stopped).OrderBy(n => n.BlockSortOrder).ToListAsync();
            var next = blockSiblings.FirstOrDefault(n => n.BlockSortOrder > completedNode.BlockSortOrder) ?? blockSiblings.FirstOrDefault();
            if (next != null && next.Status == ActionItemStatus.NotStarted)
            {
                var old = next.Status; next.Status = ActionItemStatus.Active;
                db.StatusChanges.Add(new StatusChange { Id = Guid.NewGuid(), RoadmapId = next.RoadmapId, NodeId = next.Id, OldStatus = old, NewStatus = ActionItemStatus.Active, Trigger = "auto_queue" });
            }
            return;
        }
        // Fallback: parent-based siblings (legacy)
        if (completedNode.ParentId is null) return;
        var siblings = await db.Nodes.Where(n => n.ParentId == completedNode.ParentId && n.IsActionable && n.Id != completedNode.Id
            && n.Status != ActionItemStatus.Completed && n.Status != ActionItemStatus.Stopped).OrderBy(n => n.SortOrder).ToListAsync();
        var nextSib = siblings.FirstOrDefault(n => n.SortOrder > completedNode.SortOrder) ?? siblings.FirstOrDefault();
        if (nextSib != null && nextSib.Status == ActionItemStatus.NotStarted)
        {
            var old = nextSib.Status; nextSib.Status = ActionItemStatus.Active;
            db.StatusChanges.Add(new StatusChange { Id = Guid.NewGuid(), RoadmapId = nextSib.RoadmapId, NodeId = nextSib.Id, OldStatus = old, NewStatus = ActionItemStatus.Active, Trigger = "auto_queue" });
        }
    }

    private static List<NodeDto> BuildTree(List<RoadmapNode> all, List<NodeCategoryLink> links, Dictionary<Guid, RoadmapNode> lk, Guid? pid) =>
        all.Where(n => n.ParentId == pid).OrderBy(n => n.SortOrder).Select(n => {
            var cl = links.Where(l => l.NodeId == n.Id).Select(l => new CategoryLinkDto(l.Id, l.CategoryId, lk.TryGetValue(l.CategoryId, out var c) ? c.Title : "?")).ToList();
            return new NodeDto(n.Id, n.ParentId, n.Title, n.IsActionable, n.Status.ToString(), n.Unit, n.TotalSize, n.UnitsPerHour, n.PointsPerUnit, n.ScheduleTemplate, n.SortOrder, n.ScheduleBlockId, n.BlockSortOrder, cl, BuildTree(all, links, lk, n.Id), n.IsChecklist);
        }).ToList();


    private static string BuildPath(RoadmapNode n, Dictionary<Guid, RoadmapNode> lk)
    { var p = new List<string>(); var c = n; while (c != null) { p.Add(c.Title); c = c.ParentId.HasValue && lk.TryGetValue(c.ParentId.Value, out var pr) ? pr : null; } p.Reverse(); return string.Join(" / ", p); }

    private static async Task<HashSet<Guid>> GetDescendantIds(RoadmapDbContext db, Guid rid, Guid nid)
    { var all = await db.Nodes.Where(n => n.RoadmapId == rid).Select(n => new { n.Id, n.ParentId }).ToListAsync();
        var d = new HashSet<Guid>(); var q = new Queue<Guid>(); q.Enqueue(nid);
        while (q.Count > 0) { var c = q.Dequeue(); foreach (var ch in all.Where(n => n.ParentId == c)) { d.Add(ch.Id); q.Enqueue(ch.Id); } } return d; }
}
