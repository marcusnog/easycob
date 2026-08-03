using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyCob.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceTenantOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_audit_events_tenants_tenant_id",
                table: "audit_events",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_charges_tenants_tenant_id",
                table: "charges",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_collection_rules_tenants_tenant_id",
                table: "collection_rules",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_contacts_tenants_tenant_id",
                table: "contacts",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_conversations_tenants_tenant_id",
                table: "conversations",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_customers_tenants_tenant_id",
                table: "customers",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_daily_balances_tenants_tenant_id",
                table: "daily_balances",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_inbox_messages_tenants_tenant_id",
                table: "inbox_messages",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_installments_tenants_tenant_id",
                table: "installments",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_message_templates_tenants_tenant_id",
                table: "message_templates",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_messages_tenants_tenant_id",
                table: "messages",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_outbox_messages_tenants_tenant_id",
                table: "outbox_messages",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_tenants_tenant_id",
                table: "payments",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_users_tenants_tenant_id",
                table: "users",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE FUNCTION prevent_audit_mutation() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'audit_events is append-only';
                END;
                $$ LANGUAGE plpgsql;
                CREATE TRIGGER audit_events_immutable
                    BEFORE UPDATE OR DELETE ON audit_events
                    FOR EACH ROW EXECUTE FUNCTION prevent_audit_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER audit_events_immutable ON audit_events;
                DROP FUNCTION prevent_audit_mutation();
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_audit_events_tenants_tenant_id",
                table: "audit_events");

            migrationBuilder.DropForeignKey(
                name: "FK_charges_tenants_tenant_id",
                table: "charges");

            migrationBuilder.DropForeignKey(
                name: "FK_collection_rules_tenants_tenant_id",
                table: "collection_rules");

            migrationBuilder.DropForeignKey(
                name: "FK_contacts_tenants_tenant_id",
                table: "contacts");

            migrationBuilder.DropForeignKey(
                name: "FK_conversations_tenants_tenant_id",
                table: "conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_customers_tenants_tenant_id",
                table: "customers");

            migrationBuilder.DropForeignKey(
                name: "FK_daily_balances_tenants_tenant_id",
                table: "daily_balances");

            migrationBuilder.DropForeignKey(
                name: "FK_inbox_messages_tenants_tenant_id",
                table: "inbox_messages");

            migrationBuilder.DropForeignKey(
                name: "FK_installments_tenants_tenant_id",
                table: "installments");

            migrationBuilder.DropForeignKey(
                name: "FK_message_templates_tenants_tenant_id",
                table: "message_templates");

            migrationBuilder.DropForeignKey(
                name: "FK_messages_tenants_tenant_id",
                table: "messages");

            migrationBuilder.DropForeignKey(
                name: "FK_outbox_messages_tenants_tenant_id",
                table: "outbox_messages");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_tenants_tenant_id",
                table: "payments");

            migrationBuilder.DropForeignKey(
                name: "FK_users_tenants_tenant_id",
                table: "users");
        }
    }
}
