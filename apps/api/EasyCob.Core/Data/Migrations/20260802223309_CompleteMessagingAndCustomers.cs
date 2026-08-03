using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyCob.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompleteMessagingAndCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "whats_app_phone_number_id",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "attempts",
                table: "messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "failure_code",
                table: "messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "message_template_id",
                table: "messages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "recipient",
                table: "messages",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "scheduled_at",
                table: "messages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "sent_at",
                table: "messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "archived_at",
                table: "customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "opt_out_at",
                table: "contacts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_message_templates_tenant_id_id",
                table: "message_templates",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.CreateTable(
                name: "collection_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    days_offset = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_collection_rules_message_templates_tenant_id_message_templa~",
                        columns: x => new { x.tenant_id, x.message_template_id },
                        principalTable: "message_templates",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_messages_tenant_id_message_template_id",
                table: "messages",
                columns: new[] { "tenant_id", "message_template_id" });

            migrationBuilder.CreateIndex(
                name: "IX_messages_tenant_id_status_scheduled_at",
                table: "messages",
                columns: new[] { "tenant_id", "status", "scheduled_at" });

            migrationBuilder.CreateIndex(
                name: "IX_collection_rules_tenant_id_active_days_offset",
                table: "collection_rules",
                columns: new[] { "tenant_id", "active", "days_offset" });

            migrationBuilder.CreateIndex(
                name: "IX_collection_rules_tenant_id_message_template_id",
                table: "collection_rules",
                columns: new[] { "tenant_id", "message_template_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_messages_message_templates_tenant_id_message_template_id",
                table: "messages",
                columns: new[] { "tenant_id", "message_template_id" },
                principalTable: "message_templates",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                ALTER TABLE collection_rules ENABLE ROW LEVEL SECURITY;
                ALTER TABLE collection_rules FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON collection_rules
                    USING (tenant_id = nullif(current_setting('easycob.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = nullif(current_setting('easycob.tenant_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_messages_message_templates_tenant_id_message_template_id",
                table: "messages");

            migrationBuilder.DropTable(
                name: "collection_rules");

            migrationBuilder.DropIndex(
                name: "IX_messages_tenant_id_message_template_id",
                table: "messages");

            migrationBuilder.DropIndex(
                name: "IX_messages_tenant_id_status_scheduled_at",
                table: "messages");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_message_templates_tenant_id_id",
                table: "message_templates");

            migrationBuilder.DropColumn(
                name: "whats_app_phone_number_id",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "attempts",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "failure_code",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "message_template_id",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "recipient",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "scheduled_at",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "sent_at",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "archived_at",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "opt_out_at",
                table: "contacts");
        }
    }
}
