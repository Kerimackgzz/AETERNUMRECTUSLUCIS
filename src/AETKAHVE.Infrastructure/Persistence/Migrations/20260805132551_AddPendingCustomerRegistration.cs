using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AETKAHVE.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingCustomerRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingCustomerRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReservedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    VerificationTokenHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                    PrivacyAcceptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastEmailSentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TokenExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingCustomerRegistrations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingCustomerRegistrations_CreatedAtUtc",
                table: "PendingCustomerRegistrations",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PendingCustomerRegistrations_NormalizedEmail",
                table: "PendingCustomerRegistrations",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingCustomerRegistrations_TokenExpiresAtUtc",
                table: "PendingCustomerRegistrations",
                column: "TokenExpiresAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingCustomerRegistrations");
        }
    }
}
