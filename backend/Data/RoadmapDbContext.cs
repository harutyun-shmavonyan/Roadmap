using Microsoft.EntityFrameworkCore;
using Roadmap.Api.Entities;

namespace Roadmap.Api.Data;

public class RoadmapDbContext(DbContextOptions<RoadmapDbContext> options) : DbContext(options)
{
    public DbSet<RoadmapDefinition> Roadmaps => Set<RoadmapDefinition>();
    public DbSet<RoadmapNode> Nodes => Set<RoadmapNode>();
    public DbSet<DayPlan> DayPlans => Set<DayPlan>();
    public DbSet<DayPlanEntry> DayPlanEntries => Set<DayPlanEntry>();
    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<WorkLog> WorkLogs => Set<WorkLog>();
    public DbSet<NodeCategoryLink> NodeCategoryLinks => Set<NodeCategoryLink>();
    public DbSet<StatusChange> StatusChanges => Set<StatusChange>();
    public DbSet<WeekPlan> WeekPlans => Set<WeekPlan>();
    public DbSet<WeekPlanGoal> WeekPlanGoals => Set<WeekPlanGoal>();
    public DbSet<SprintPlanEntry> SprintPlanEntries => Set<SprintPlanEntry>();
    public DbSet<Habit> Habits => Set<Habit>();
    public DbSet<SprintHabit> SprintHabits => Set<SprintHabit>();
    public DbSet<HabitCheck> HabitChecks => Set<HabitCheck>();
    public DbSet<SingleTask> SingleTasks => Set<SingleTask>();
    public DbSet<NodeSubPoint> NodeSubPoints => Set<NodeSubPoint>();
    public DbSet<NodeSubPointCheck> NodeSubPointChecks => Set<NodeSubPointCheck>();
    public DbSet<CustomLog> CustomLogs => Set<CustomLog>();
    public DbSet<ScheduleBlock> ScheduleBlocks => Set<ScheduleBlock>();
    public DbSet<RelaxDay> RelaxDays => Set<RelaxDay>();
    public DbSet<SprintGoal> SprintGoals => Set<SprintGoal>();
    public DbSet<SprintGoalLog> SprintGoalLogs => Set<SprintGoalLog>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<VocabEntry> VocabEntries => Set<VocabEntry>();
    public DbSet<VocabReview> VocabReviews => Set<VocabReview>();
    public DbSet<NotePrompt> NotePrompts => Set<NotePrompt>();
    public DbSet<NotePromptReview> NotePromptReviews => Set<NotePromptReview>();
    public DbSet<JobRun> JobRuns => Set<JobRun>();
    public DbSet<JobPosting> JobPostings => Set<JobPosting>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<NewsletterIssue> NewsletterIssues => Set<NewsletterIssue>();

    // --- Courses ---
    public DbSet<LessonTemplate> LessonTemplates => Set<LessonTemplate>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Stage> Stages => Set<Stage>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<LessonSection> LessonSections => Set<LessonSection>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<CourseResource> CourseResources => Set<CourseResource>();
    public DbSet<ProgressEvent> ProgressEvents => Set<ProgressEvent>();
    public DbSet<ArticleImage> ArticleImages => Set<ArticleImage>();
    public DbSet<Meal> Meals => Set<Meal>();
    public DbSet<MealImage> MealImages => Set<MealImage>();
    public DbSet<Experience> Experiences => Set<Experience>();
    public DbSet<ExperienceImage> ExperienceImages => Set<ExperienceImage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RoadmapDefinition>(e =>
        {
            e.ToTable("roadmaps");
            e.HasKey(r => r.Id);
            e.Property(r => r.Name).HasMaxLength(256).IsRequired();
            e.Property(r => r.Description).HasMaxLength(1024);
        });

        modelBuilder.Entity<RoadmapNode>(e =>
        {
            e.ToTable("roadmap_nodes");
            e.HasKey(n => n.Id);
            e.Property(n => n.Title).HasMaxLength(512).IsRequired();
            e.Property(n => n.Unit).HasMaxLength(64);
            e.Property(n => n.ScheduleTemplate).HasMaxLength(1024);

            e.Property(n => n.Status)
                .HasConversion<string>()
                .HasMaxLength(32)
                .HasDefaultValue(ActionItemStatus.NotStarted);

            e.HasOne(n => n.Roadmap)
                .WithMany(r => r.Nodes)
                .HasForeignKey(n => n.RoadmapId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(n => n.Parent)
                .WithMany(n => n.Children)
                .HasForeignKey(n => n.ParentId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(n => n.ScheduleBlock)
                .WithMany(sb => sb.Items)
                .HasForeignKey(n => n.ScheduleBlockId)
                .OnDelete(DeleteBehavior.SetNull);

            e.Property(n => n.BlockSortOrder).HasDefaultValue(0);
            e.Property(n => n.IsActiveInBlock).HasDefaultValue(true);
            e.Property(n => n.IsChecklist).HasDefaultValue(false);

            e.HasIndex(n => new { n.RoadmapId, n.ParentId, n.SortOrder });
            e.HasIndex(n => new { n.ScheduleBlockId, n.BlockSortOrder });
        });

        modelBuilder.Entity<ScheduleBlock>(e =>
        {
            e.ToTable("schedule_blocks");
            e.HasKey(sb => sb.Id);
            e.Property(sb => sb.Name).HasMaxLength(256).IsRequired();
            e.Property(sb => sb.ScheduleTemplate).HasMaxLength(1024);
            e.Property(sb => sb.Mode)
                .HasConversion<string>()
                .HasMaxLength(16)
                .HasDefaultValue(ScheduleBlockMode.Queue);
            e.HasOne(sb => sb.Roadmap).WithMany().HasForeignKey(sb => sb.RoadmapId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(sb => new { sb.RoadmapId, sb.SortOrder });
        });

        modelBuilder.Entity<NodeCategoryLink>(e =>
        {
            e.ToTable("node_category_links");
            e.HasKey(l => l.Id);

            e.HasOne(l => l.Node)
                .WithMany(n => n.CategoryLinks)
                .HasForeignKey(l => l.NodeId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(l => l.Category)
                .WithMany()
                .HasForeignKey(l => l.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(l => new { l.NodeId, l.CategoryId }).IsUnique();
        });

        modelBuilder.Entity<DayPlan>(e =>
        {
            e.ToTable("day_plans");
            e.HasKey(d => d.Id);
            e.Property(d => d.Notes).HasMaxLength(2048);

            e.HasOne(d => d.Roadmap)
                .WithMany()
                .HasForeignKey(d => d.RoadmapId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(d => new { d.RoadmapId, d.Date }).IsUnique();
        });

        modelBuilder.Entity<DayPlanEntry>(e =>
        {
            e.ToTable("day_plan_entries");
            e.HasKey(x => x.Id);
            e.Property(x => x.Note).HasMaxLength(1024);

            e.HasOne(x => x.DayPlan)
                .WithMany(d => d.Entries)
                .HasForeignKey(x => x.DayPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Node)
                .WithMany(n => n.DayPlanEntries)
                .HasForeignKey(x => x.NodeId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.DayPlanId, x.StartMinute });
        });

        modelBuilder.Entity<Sprint>(e =>
        {
            e.ToTable("sprints");
            e.HasKey(s => s.Id);
            e.Property(s => s.Name).HasMaxLength(256).IsRequired();
            e.Property(s => s.RelaxDays).HasMaxLength(2048);
            e.Property(s => s.ScoringMode)
                .HasConversion<string>()
                .HasMaxLength(16)
                .HasDefaultValue(ScoringMode.Fixed);
            e.Ignore(s => s.IsOpen);

            e.HasOne(s => s.Roadmap)
                .WithMany()
                .HasForeignKey(s => s.RoadmapId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(s => new { s.RoadmapId, s.StartDate });
        });

        modelBuilder.Entity<SprintPlanEntry>(e =>
        {
            e.ToTable("sprint_plan_entries");
            e.HasKey(p => p.Id);

            e.HasOne(p => p.Sprint)
                .WithMany(s => s.PlanEntries)
                .HasForeignKey(p => p.SprintId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(p => p.Node)
                .WithMany()
                .HasForeignKey(p => p.NodeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Pool sessions belong to the block, not to any one item.
            e.HasOne(p => p.Block)
                .WithMany()
                .HasForeignKey(p => p.BlockId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(p => new { p.SprintId, p.Date });
        });

        modelBuilder.Entity<WorkLog>(e =>
        {
            e.ToTable("work_logs");
            e.HasKey(w => w.Id);
            e.Property(w => w.Note).HasMaxLength(1024);

            e.HasOne(w => w.Node)
                .WithMany(n => n.WorkLogs)
                .HasForeignKey(w => w.NodeId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(w => w.Roadmap)
                .WithMany()
                .HasForeignKey(w => w.RoadmapId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(w => w.Sprint)
                .WithMany()
                .HasForeignKey(w => w.SprintId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(w => new { w.SprintId, w.NodeId, w.Date }).IsUnique();
            e.HasIndex(w => new { w.RoadmapId, w.NodeId, w.Date });
        });

        modelBuilder.Entity<StatusChange>(e =>
        {
            e.ToTable("status_changes");
            e.HasKey(s => s.Id);
            e.Property(s => s.Trigger).HasMaxLength(64).IsRequired();
            e.Property(s => s.OldStatus).HasConversion<string>().HasMaxLength(32);
            e.Property(s => s.NewStatus).HasConversion<string>().HasMaxLength(32);

            e.HasOne(s => s.Node)
                .WithMany(n => n.StatusChanges)
                .HasForeignKey(s => s.NodeId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(s => s.Roadmap)
                .WithMany()
                .HasForeignKey(s => s.RoadmapId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(s => new { s.RoadmapId, s.NodeId, s.ChangedAt });
        });

        modelBuilder.Entity<WeekPlan>(e =>
        {
            e.ToTable("week_plans");
            e.HasKey(w => w.Id);
            e.Property(w => w.Notes).HasMaxLength(2048);

            e.HasOne(w => w.Roadmap)
                .WithMany()
                .HasForeignKey(w => w.RoadmapId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(w => new { w.RoadmapId, w.WeekStart }).IsUnique();
        });

        modelBuilder.Entity<WeekPlanGoal>(e =>
        {
            e.ToTable("week_plan_goals");
            e.HasKey(g => g.Id);
            e.Property(g => g.Title).HasMaxLength(512).IsRequired();
            e.Property(g => g.TargetDescription).HasMaxLength(256);
            e.Property(g => g.ResultNote).HasMaxLength(1024);

            e.HasOne(g => g.WeekPlan)
                .WithMany(w => w.CustomGoals)
                .HasForeignKey(g => g.WeekPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(g => g.SprintGoal)
                .WithMany()
                .HasForeignKey(g => g.SprintGoalId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(g => new { g.WeekPlanId, g.SortOrder });
        });

        modelBuilder.Entity<Habit>(e =>
        {
            e.ToTable("habits");
            e.HasKey(h => h.Id);
            e.Property(h => h.Name).HasMaxLength(256).IsRequired();
            e.HasOne(h => h.Roadmap).WithMany().HasForeignKey(h => h.RoadmapId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SprintHabit>(e =>
        {
            e.ToTable("sprint_habits");
            e.HasKey(sh => sh.Id);
            e.HasOne(sh => sh.Sprint).WithMany().HasForeignKey(sh => sh.SprintId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(sh => sh.Habit).WithMany(h => h.SprintHabits).HasForeignKey(sh => sh.HabitId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(sh => new { sh.SprintId, sh.HabitId }).IsUnique();
        });

        modelBuilder.Entity<HabitCheck>(e =>
        {
            e.ToTable("habit_checks");
            e.HasKey(hc => hc.Id);
            e.HasOne(hc => hc.SprintHabit).WithMany(sh => sh.Checks).HasForeignKey(hc => hc.SprintHabitId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(hc => new { hc.SprintHabitId, hc.Date }).IsUnique();
        });

        modelBuilder.Entity<SingleTask>(e =>
        {
            e.ToTable("single_tasks");
            e.HasKey(t => t.Id);
            e.Property(t => t.Title).HasMaxLength(512).IsRequired();
            e.Property(t => t.Priority).HasConversion<string>().HasMaxLength(16);
            e.Property(t => t.Weekdays).HasMaxLength(64);
            e.HasOne(t => t.Roadmap).WithMany().HasForeignKey(t => t.RoadmapId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(t => new { t.RoadmapId, t.IsCompleted, t.Priority });
        });

        modelBuilder.Entity<NodeSubPoint>(e =>
        {
            e.ToTable("node_subpoints");
            e.HasKey(s => s.Id);
            e.Property(s => s.Title).HasMaxLength(512).IsRequired();
            e.HasOne(s => s.Node).WithMany().HasForeignKey(s => s.NodeId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => new { s.NodeId, s.SortOrder });
        });

        modelBuilder.Entity<NodeSubPointCheck>(e =>
        {
            e.ToTable("node_subpoint_checks");
            e.HasKey(c => c.Id);
            e.HasOne(c => c.SubPoint).WithMany(s => s.Checks).HasForeignKey(c => c.SubPointId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(c => new { c.SubPointId, c.Date }).IsUnique();
        });

        modelBuilder.Entity<CustomLog>(e =>
        {
            e.ToTable("custom_logs");
            e.HasKey(c => c.Id);
            e.Property(c => c.Title).HasMaxLength(512).IsRequired();
            e.Property(c => c.Note).HasMaxLength(1024);
            e.HasOne(c => c.Roadmap).WithMany().HasForeignKey(c => c.RoadmapId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(c => new { c.RoadmapId, c.Date });
        });

        modelBuilder.Entity<RelaxDay>(e =>
        {
            e.ToTable("relax_days");
            e.HasKey(r => r.Id);
            e.HasOne(r => r.Roadmap).WithMany().HasForeignKey(r => r.RoadmapId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => new { r.RoadmapId, r.Date }).IsUnique();
        });

        modelBuilder.Entity<SprintGoal>(e =>
        {
            e.ToTable("sprint_goals");
            e.HasKey(g => g.Id);
            e.Property(g => g.Title).HasMaxLength(512).IsRequired();
            e.Property(g => g.Unit).HasMaxLength(64);
            e.Property(g => g.Description).HasMaxLength(1024);
            e.HasOne(g => g.Sprint).WithMany().HasForeignKey(g => g.SprintId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(g => new { g.SprintId, g.SortOrder });
        });

        modelBuilder.Entity<SprintGoalLog>(e =>
        {
            e.ToTable("sprint_goal_logs");
            e.HasKey(l => l.Id);
            e.HasOne(l => l.SprintGoal).WithMany(g => g.Logs).HasForeignKey(l => l.SprintGoalId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(l => new { l.SprintGoalId, l.Date });
        });

        modelBuilder.Entity<Note>(e =>
        {
            e.ToTable("notes");
            e.HasKey(n => n.Id);
            e.Property(n => n.Book).HasMaxLength(16).IsRequired();
            e.HasIndex(n => new { n.Book, n.EntryDate }).IsUnique();
            e.HasIndex(n => new { n.Book, n.DayNumber }).IsUnique();
        });

        modelBuilder.Entity<JobRun>(e =>
        {
            e.ToTable("job_runs");
            e.HasKey(r => r.Id);
            // One run per day: re-importing a date replaces it (see JobRun docs).
            e.HasIndex(r => r.RunDate).IsUnique();
            // Npgsql maps List<string> to text[] natively — no join table for what is
            // read-only, whole-list data. Same rationale as VocabEntry above.
        });

        modelBuilder.Entity<JobPosting>(e =>
        {
            e.ToTable("job_postings");
            e.HasKey(p => p.Id);
            e.Property(p => p.Title).HasMaxLength(512).IsRequired();
            e.Property(p => p.Company).HasMaxLength(256).IsRequired();
            e.Property(p => p.Url).HasMaxLength(1024).IsRequired();
            e.Property(p => p.Source).HasMaxLength(64).IsRequired();
            e.Property(p => p.Location).HasMaxLength(512);
            e.Property(p => p.Bucket).HasMaxLength(64).IsRequired();
            e.Property(p => p.SeniorityClass).HasMaxLength(64);
            // Tailored CV: PDF bytes as bytea, change list as unbounded text. Both nullable.
            e.Property(p => p.TailoredCvPdf).HasColumnType("bytea");
            e.Property(p => p.CvChangeList).HasColumnType("text");
            // CV-vs-JD fit: an int score and the gap breakdown as raw JSON text. Both nullable.
            e.Property(p => p.CvFitScore);
            e.Property(p => p.CvFitGaps).HasColumnType("text");
            // Application outcome. Status is bounded but free text — the UI
            // offers a fixed set and the column tolerates new values without a
            // migration. Notes are unbounded.
            e.Property(p => p.ApplicationStatus).HasMaxLength(32);
            e.Property(p => p.ApplicationNotes).HasColumnType("text");

            e.HasOne(p => p.Run)
                .WithMany(r => r.Postings)
                .HasForeignKey(p => p.JobRunId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(p => new { p.JobRunId, p.SortOrder });
        });

        modelBuilder.Entity<Article>(e =>
        {
            e.ToTable("articles");
            e.HasKey(a => a.Id);
            e.Property(a => a.Title).HasMaxLength(512).IsRequired();
            e.Property(a => a.Content).HasColumnType("text");
            e.Property(a => a.Format).HasMaxLength(16).HasDefaultValue("markdown");
            e.Property(a => a.ChatUrl).HasMaxLength(2048);
            e.Property(a => a.ReadAnchor).HasMaxLength(64);
            e.HasIndex(a => a.SortOrder);
            e.HasMany(a => a.Images)
                .WithOne(i => i.Article!)
                .HasForeignKey(i => i.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NewsletterIssue>(e =>
        {
            e.ToTable("newsletter_issues");
            e.HasKey(n => n.Id);
            e.Property(n => n.Title).HasMaxLength(256).IsRequired();
            e.Property(n => n.Html).HasColumnType("text");
            // One edition per day — the publish path upserts on this rather than inserting.
            e.HasIndex(n => n.IssueDate).IsUnique();
        });

        modelBuilder.Entity<ArticleImage>(e =>
        {
            e.ToTable("article_images");
            e.HasKey(i => i.Id);
            e.Property(i => i.Name).HasMaxLength(256).IsRequired();
            e.Property(i => i.ContentType).HasMaxLength(128).IsRequired();
            e.Property(i => i.Data).HasColumnType("bytea");
            // One image name per article — re-uploading the same name replaces it.
            e.HasIndex(i => new { i.ArticleId, i.Name }).IsUnique();
        });

        modelBuilder.Entity<VocabEntry>(e =>
        {
            e.ToTable("vocab_entries");
            e.HasKey(v => v.Id);
            e.Property(v => v.Term).HasMaxLength(256).IsRequired();
            e.Property(v => v.NormalizedTerm).HasMaxLength(256).IsRequired();
            e.Property(v => v.Definition).IsRequired();
            e.Property(v => v.Kind).HasConversion<string>().HasMaxLength(32);
            e.Property(v => v.Frequency).HasConversion<string>().HasMaxLength(32);
            e.Property(v => v.Register).HasConversion<string>().HasMaxLength(32);
            // Npgsql maps List<string> to text[] natively — no join tables for what is
            // read-only, whole-list data.
            e.Property(v => v.Examples).HasColumnType("text[]");
            e.Property(v => v.Collocations).HasColumnType("text[]");
            e.Property(v => v.Synonyms).HasColumnType("text[]");
            // One row per item: a re-save of the same term updates rather than duplicates.
            e.HasIndex(v => v.NormalizedTerm).IsUnique();
            // The review queue is "everything due on or before today", so it is the hot path.
            e.HasIndex(v => v.DueOn);
        });

        modelBuilder.Entity<Meal>(e =>
        {
            e.ToTable("meals");
            e.HasKey(m => m.Id);
            e.Property(m => m.Name).HasMaxLength(200).IsRequired();
            e.Property(m => m.Summary).HasMaxLength(512);
            e.Property(m => m.Slot).HasConversion<string>().HasMaxLength(32);
            // Npgsql maps List<string> to text[] natively — no join tables for what is
            // read-and-replace, whole-list data. Same rationale as VocabEntry above.
            e.Property(m => m.Ingredients).HasColumnType("text[]");
            e.Property(m => m.Steps).HasColumnType("text[]");
            e.Property(m => m.Tags).HasColumnType("text[]");
            // The tab always reads one slot at a time; the ordering within it (by protein density)
            // is done in memory, so the index only has to narrow to the slot.
            e.HasIndex(m => new { m.Slot, m.SortOrder });
        });

        modelBuilder.Entity<MealImage>(e =>
        {
            e.ToTable("meal_images");
            // The meal id is the key: one photo per meal, so a re-upload replaces rather than piles up.
            e.HasKey(i => i.MealId);
            e.Property(i => i.MealId).ValueGeneratedNever();
            e.Property(i => i.FileName).HasMaxLength(256);
            e.Property(i => i.ContentType).HasMaxLength(128).IsRequired();
            e.Property(i => i.Data).HasColumnType("bytea");
            e.HasOne(i => i.Meal!)
                .WithOne(m => m.Image!)
                .HasForeignKey<MealImage>(i => i.MealId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Experience>(e =>
        {
            e.ToTable("experiences");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Category).HasMaxLength(64);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Location).HasMaxLength(256);
            e.Property(x => x.DescriptionMd).HasColumnType("text");
            // text[] like the meal book's tags: read and replaced whole, never joined against.
            // GIN so "every experience tagged X" is an index lookup rather than a scan.
            // Defaulted to an empty array so the column can be added to a table that already has
            // rows — NOT NULL with no default would fail the migration on the first real one.
            e.Property(x => x.Tags).HasColumnType("text[]").HasDefaultValueSql("'{}'::text[]");
            e.HasIndex(x => x.Tags).HasMethod("gin");
            // The tab reads by status and orders by date within it.
            e.HasIndex(x => new { x.Status, x.StartDate });
            e.HasIndex(x => x.Category);
        });

        modelBuilder.Entity<ExperienceImage>(e =>
        {
            e.ToTable("experience_images");
            e.HasKey(i => i.Id);
            e.Property(i => i.ContentType).HasMaxLength(100).IsRequired();
            e.Property(i => i.FileName).HasMaxLength(255);
            e.Property(i => i.Caption).HasMaxLength(512);
            e.HasOne(i => i.Experience).WithMany(x => x.Images)
                .HasForeignKey(i => i.ExperienceId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(i => new { i.ExperienceId, i.SortOrder });
        });

        modelBuilder.Entity<VocabReview>(e =>
        {
            e.ToTable("vocab_reviews");
            e.HasKey(r => r.Id);
            e.HasOne(r => r.VocabEntry).WithMany(v => v.Reviews)
                .HasForeignKey(r => r.VocabEntryId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => new { r.VocabEntryId, r.ReviewedAt });
        });

        // ===== Notes v2: FSRS-scheduled prompts extracted from daily notes =====
        modelBuilder.Entity<NotePrompt>(e =>
        {
            e.ToTable("note_prompts");
            e.HasKey(p => p.Id);
            e.HasOne(p => p.Note).WithMany(n => n.Prompts)
                .HasForeignKey(p => p.NoteId).OnDelete(DeleteBehavior.Cascade);
            e.Property(p => p.Question).IsRequired();
            e.Property(p => p.Answer).IsRequired();
            e.Property(p => p.State).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(p => new { p.NoteId, p.SortOrder });
            // The daily session is "active and due on or before today" — the hot path.
            e.HasIndex(p => new { p.State, p.DueOn });
        });

        modelBuilder.Entity<NotePromptReview>(e =>
        {
            e.ToTable("note_prompt_reviews");
            e.HasKey(r => r.Id);
            e.HasOne(r => r.NotePrompt).WithMany(p => p.ReviewHistory)
                .HasForeignKey(r => r.NotePromptId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => new { r.NotePromptId, r.ReviewedAt });
            // The daily cap counts today's rows.
            e.HasIndex(r => r.ReviewDate);
        });

        // ===== Courses =====
        // Soft delete is a query filter on the four entities that carry history (course, stage,
        // lesson, exercise). Sections and resources are hard-deleted: they are cheap to recreate
        // and nothing points at them.

        modelBuilder.Entity<LessonTemplate>(e =>
        {
            e.ToTable("lesson_templates");
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).HasMaxLength(128).IsRequired();
            e.HasIndex(t => t.Name).IsUnique();
            e.Property(t => t.Sections).HasColumnType("jsonb").IsRequired();
            e.Property(t => t.ExerciseKinds).HasColumnType("jsonb").IsRequired();
        });

        modelBuilder.Entity<Course>(e =>
        {
            e.ToTable("courses");
            e.HasKey(c => c.Id);
            e.Property(c => c.Slug).HasMaxLength(128).IsRequired();
            e.Property(c => c.Title).HasMaxLength(256).IsRequired();
            e.Property(c => c.Subtitle).HasMaxLength(512);
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(c => c.Metadata).HasColumnType("jsonb").IsRequired();
            e.Property(c => c.InstructionsMd).HasColumnType("text");
            // One user, so the slug is unique outright rather than per owner.
            e.HasIndex(c => c.Slug).IsUnique();
            e.HasOne(c => c.Template).WithMany()
                .HasForeignKey(c => c.TemplateId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(c => c.DeletedAt == null);
        });

        modelBuilder.Entity<Stage>(e =>
        {
            e.ToTable("stages");
            e.HasKey(st => st.Id);
            e.Property(st => st.Code).HasMaxLength(32).IsRequired();
            e.Property(st => st.Title).HasMaxLength(256).IsRequired();
            e.Property(st => st.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(st => st.Metadata).HasColumnType("jsonb").IsRequired();
            e.HasOne(st => st.Course).WithMany(c => c.Stages)
                .HasForeignKey(st => st.CourseId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(st => new { st.CourseId, st.Code }).IsUnique();
            e.HasIndex(st => new { st.CourseId, st.Position });
            e.HasQueryFilter(st => st.DeletedAt == null && st.Course.DeletedAt == null);
        });

        modelBuilder.Entity<Lesson>(e =>
        {
            e.ToTable("lessons");
            e.HasKey(l => l.Id);
            e.Property(l => l.Code).HasMaxLength(32).IsRequired();
            e.Property(l => l.Title).HasMaxLength(256).IsRequired();
            e.Property(l => l.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(l => l.Metadata).HasColumnType("jsonb").IsRequired();
            e.HasOne(l => l.Stage).WithMany(st => st.Lessons)
                .HasForeignKey(l => l.StageId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(l => new { l.StageId, l.Position });
            // Codes are unique per COURSE, which needs a join — enforced in the service.
            e.HasQueryFilter(l => l.DeletedAt == null && l.Stage.DeletedAt == null
                && l.Stage.Course.DeletedAt == null);
        });

        modelBuilder.Entity<LessonSection>(e =>
        {
            e.ToTable("lesson_sections");
            e.HasKey(s => s.Id);
            e.Property(s => s.Kind).HasMaxLength(64).IsRequired();
            e.Property(s => s.Title).HasMaxLength(256);
            e.Property(s => s.ContentMd).IsRequired();
            e.HasOne(s => s.Lesson).WithMany(l => l.Sections)
                .HasForeignKey(s => s.LessonId).OnDelete(DeleteBehavior.Cascade);
            // One section per kind per lesson — what makes upsert_lesson_section idempotent.
            e.HasIndex(s => new { s.LessonId, s.Kind }).IsUnique();
            e.HasQueryFilter(s => s.Lesson.DeletedAt == null && s.Lesson.Stage.DeletedAt == null
                && s.Lesson.Stage.Course.DeletedAt == null);
        });

        modelBuilder.Entity<Exercise>(e =>
        {
            e.ToTable("exercises");
            e.HasKey(x => x.Id);
            e.Property(x => x.SectionKind).HasMaxLength(64).IsRequired();
            e.Property(x => x.Kind).HasMaxLength(64).IsRequired();
            e.Property(x => x.Title).HasMaxLength(256).IsRequired();
            e.Property(x => x.PromptMd).IsRequired();
            e.HasOne(x => x.Lesson).WithMany(l => l.Exercises)
                .HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.LessonId, x.Position });
            e.HasQueryFilter(x => x.DeletedAt == null && x.Lesson.DeletedAt == null
                && x.Lesson.Stage.DeletedAt == null && x.Lesson.Stage.Course.DeletedAt == null);
        });

        modelBuilder.Entity<Submission>(e =>
        {
            e.ToTable("submissions");
            e.HasKey(s => s.Id);
            e.Property(s => s.Links).HasColumnType("jsonb").IsRequired();
            e.Property(s => s.SubmittedBy).HasMaxLength(64).IsRequired();
            e.Property(s => s.IdempotencyKey).HasMaxLength(128);
            e.HasOne(s => s.Exercise).WithMany(x => x.Submissions)
                .HasForeignKey(s => s.ExerciseId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => new { s.ExerciseId, s.AttemptNo }).IsUnique();
            e.HasIndex(s => s.IdempotencyKey);
            e.HasQueryFilter(s => s.Exercise.DeletedAt == null && s.Exercise.Lesson.DeletedAt == null
                && s.Exercise.Lesson.Stage.DeletedAt == null
                && s.Exercise.Lesson.Stage.Course.DeletedAt == null);
        });

        modelBuilder.Entity<Grade>(e =>
        {
            e.ToTable("grades");
            e.HasKey(g => g.Id);
            e.Property(g => g.Rubric).HasColumnType("jsonb");
            e.Property(g => g.GradedBy).HasMaxLength(64).IsRequired();
            e.Property(g => g.IdempotencyKey).HasMaxLength(128);
            e.HasOne(g => g.Submission).WithMany(s => s.Grades)
                .HasForeignKey(g => g.SubmissionId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(g => new { g.SubmissionId, g.GradedAt });
            e.HasIndex(g => g.IdempotencyKey);
            e.HasQueryFilter(g => g.Submission.Exercise.DeletedAt == null
                && g.Submission.Exercise.Lesson.DeletedAt == null
                && g.Submission.Exercise.Lesson.Stage.DeletedAt == null
                && g.Submission.Exercise.Lesson.Stage.Course.DeletedAt == null);
        });

        modelBuilder.Entity<CourseResource>(e =>
        {
            e.ToTable("course_resources");
            e.HasKey(r => r.Id);
            e.Property(r => r.Title).HasMaxLength(256).IsRequired();
            e.Property(r => r.Url).HasMaxLength(1024).IsRequired();
            e.Property(r => r.Kind).HasMaxLength(32).IsRequired();
            e.HasOne(r => r.Course).WithMany(c => c.Resources)
                .HasForeignKey(r => r.CourseId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => new { r.CourseId, r.LessonId });
            e.HasQueryFilter(r => r.Course.DeletedAt == null);
        });

        modelBuilder.Entity<ProgressEvent>(e =>
        {
            e.ToTable("progress_events");
            e.HasKey(ev => ev.Id);
            e.Property(ev => ev.Type).HasConversion<string>().HasMaxLength(32);
            e.Property(ev => ev.Payload).HasColumnType("jsonb").IsRequired();
            e.Property(ev => ev.Actor).HasMaxLength(64).IsRequired();
            e.HasOne(ev => ev.Course).WithMany()
                .HasForeignKey(ev => ev.CourseId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(ev => new { ev.CourseId, ev.CreatedAt });
            e.HasQueryFilter(ev => ev.Course.DeletedAt == null);
        });
    }
}
