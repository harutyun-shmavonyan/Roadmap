using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Roadmap.Api.Migrations
{
    /// <inheritdoc />
    public partial class FlashcardsDueFromExposure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every never-answered prompt becomes due when it would have been: the day it was first seen
            // (its card's date, for backfilled prompts) + 4 days, in Asia/Yerevan. For the migrated
            // backlog that is in the past, so it is due now; anything parked unanswered is reactivated.
            migrationBuilder.Sql("""
                UPDATE flashcard_prompts
                SET "DueOn" = (("LastReviewedAt" AT TIME ZONE 'Asia/Yerevan')::date + 4),
                    "State" = CASE WHEN "State" = 'Parked' THEN 'Active' ELSE "State" END,
                    "UpdatedAt" = now()
                WHERE "Reviews" = 0 AND "State" <> 'Suspended';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only: the previous due dates are not recoverable and not worth restoring.

        }
    }
}
