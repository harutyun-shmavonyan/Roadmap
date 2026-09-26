namespace Roadmap.Api.Entities;

/// <summary>Where a course is in its life. <c>Archived</c> is terminal.</summary>
public enum CourseStatus { Draft = 0, Active = 1, Paused = 2, Completed = 3, Archived = 4 }

/// <summary>
/// A stage's state. <c>Planned</c> means it exists as a title and nothing more — the normal
/// starting point, since a course is outlined long before it is written.
/// </summary>
public enum StageStatus { Planned = 0, Defined = 1, InProgress = 2, Completed = 3, Skipped = 4 }

/// <summary>
/// A lesson's state. <c>Placeholder</c> is a lesson with no content at all, which is valid and
/// common; <c>Ready</c> asserts that every section the course template requires is present.
/// </summary>
public enum LessonStatus
{
    Placeholder = 0, Draft = 1, Ready = 2, InProgress = 3, Submitted = 4, Completed = 5, Skipped = 6
}

/// <summary>What a <see cref="ProgressEvent"/> records.</summary>
public enum ProgressEventType
{
    StatusChanged = 0, SubmissionAdded = 1, GradeAdded = 2, Note = 3,
    Handoff = 4, TimeLogged = 5, StructureChanged = 6
}
