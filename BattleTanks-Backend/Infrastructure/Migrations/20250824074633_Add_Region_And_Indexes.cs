using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Region_And_Indexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "game_sessions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "default");

            migrationBuilder.CreateIndex(
                name: "IX_users_TotalScore_GamesWon",
                table: "users",
                columns: new[] { "TotalScore", "GamesWon" });

            migrationBuilder.CreateIndex(
                name: "IX_scores_Points_AchievedAt",
                table: "scores",
                columns: new[] { "Points", "AchievedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_game_sessions_Region",
                table: "game_sessions",
                column: "Region");

            migrationBuilder.CreateIndex(
                name: "IX_game_sessions_Status_IsPublic_CreatedAt",
                table: "game_sessions",
                columns: new[] { "Status", "IsPublic", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_RoomId_SentAt",
                table: "chat_messages",
                columns: new[] { "RoomId", "SentAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_TotalScore_GamesWon",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_scores_Points_AchievedAt",
                table: "scores");

            migrationBuilder.DropIndex(
                name: "IX_game_sessions_Region",
                table: "game_sessions");

            migrationBuilder.DropIndex(
                name: "IX_game_sessions_Status_IsPublic_CreatedAt",
                table: "game_sessions");

            migrationBuilder.DropIndex(
                name: "IX_chat_messages_RoomId_SentAt",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "game_sessions");
        }
    }
}
