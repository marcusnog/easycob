using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyCob.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class TenantRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE table_name text;
                BEGIN
                    FOREACH table_name IN ARRAY ARRAY[
                        'users', 'customers', 'contacts', 'charges', 'installments', 'payments',
                        'message_templates', 'conversations', 'messages', 'daily_balances',
                        'audit_events', 'outbox_messages', 'inbox_messages'
                    ] LOOP
                        EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', table_name);
                        EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY', table_name);
                        EXECUTE format(
                            'CREATE POLICY tenant_isolation ON %I USING (tenant_id = nullif(current_setting(''easycob.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = nullif(current_setting(''easycob.tenant_id'', true), '''')::uuid)',
                            table_name);
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE table_name text;
                BEGIN
                    FOREACH table_name IN ARRAY ARRAY[
                        'users', 'customers', 'contacts', 'charges', 'installments', 'payments',
                        'message_templates', 'conversations', 'messages', 'daily_balances',
                        'audit_events', 'outbox_messages', 'inbox_messages'
                    ] LOOP
                        EXECUTE format('DROP POLICY tenant_isolation ON %I', table_name);
                        EXECUTE format('ALTER TABLE %I DISABLE ROW LEVEL SECURITY', table_name);
                    END LOOP;
                END $$;
                """);
        }
    }
}
