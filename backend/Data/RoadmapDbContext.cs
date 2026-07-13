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
    }
}
