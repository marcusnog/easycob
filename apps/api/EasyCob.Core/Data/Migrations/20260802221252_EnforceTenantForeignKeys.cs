using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyCob.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceTenantForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_charges_customers_customer_id",
                table: "charges");

            migrationBuilder.DropForeignKey(
                name: "FK_contacts_customers_customer_id",
                table: "contacts");

            migrationBuilder.DropForeignKey(
                name: "FK_conversations_customers_customer_id",
                table: "conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_installments_charges_charge_id",
                table: "installments");

            migrationBuilder.DropForeignKey(
                name: "FK_messages_charges_charge_id",
                table: "messages");

            migrationBuilder.DropForeignKey(
                name: "FK_messages_conversations_conversation_id",
                table: "messages");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_charges_charge_id",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_payments_charge_id",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_messages_charge_id",
                table: "messages");

            migrationBuilder.DropIndex(
                name: "IX_messages_conversation_id",
                table: "messages");

            migrationBuilder.DropIndex(
                name: "IX_installments_charge_id",
                table: "installments");

            migrationBuilder.DropIndex(
                name: "IX_conversations_customer_id",
                table: "conversations");

            migrationBuilder.DropIndex(
                name: "IX_contacts_customer_id",
                table: "contacts");

            migrationBuilder.DropIndex(
                name: "IX_charges_customer_id",
                table: "charges");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_customers_tenant_id_id",
                table: "customers",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_conversations_tenant_id_id",
                table: "conversations",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_charges_tenant_id_id",
                table: "charges",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_payments_tenant_id_charge_id",
                table: "payments",
                columns: new[] { "tenant_id", "charge_id" });

            migrationBuilder.CreateIndex(
                name: "IX_messages_tenant_id_charge_id",
                table: "messages",
                columns: new[] { "tenant_id", "charge_id" });

            migrationBuilder.CreateIndex(
                name: "IX_messages_tenant_id_conversation_id",
                table: "messages",
                columns: new[] { "tenant_id", "conversation_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_charges_customers_tenant_id_customer_id",
                table: "charges",
                columns: new[] { "tenant_id", "customer_id" },
                principalTable: "customers",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_contacts_customers_tenant_id_customer_id",
                table: "contacts",
                columns: new[] { "tenant_id", "customer_id" },
                principalTable: "customers",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_conversations_customers_tenant_id_customer_id",
                table: "conversations",
                columns: new[] { "tenant_id", "customer_id" },
                principalTable: "customers",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_installments_charges_tenant_id_charge_id",
                table: "installments",
                columns: new[] { "tenant_id", "charge_id" },
                principalTable: "charges",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_messages_charges_tenant_id_charge_id",
                table: "messages",
                columns: new[] { "tenant_id", "charge_id" },
                principalTable: "charges",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_messages_conversations_tenant_id_conversation_id",
                table: "messages",
                columns: new[] { "tenant_id", "conversation_id" },
                principalTable: "conversations",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_charges_tenant_id_charge_id",
                table: "payments",
                columns: new[] { "tenant_id", "charge_id" },
                principalTable: "charges",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_charges_customers_tenant_id_customer_id",
                table: "charges");

            migrationBuilder.DropForeignKey(
                name: "FK_contacts_customers_tenant_id_customer_id",
                table: "contacts");

            migrationBuilder.DropForeignKey(
                name: "FK_conversations_customers_tenant_id_customer_id",
                table: "conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_installments_charges_tenant_id_charge_id",
                table: "installments");

            migrationBuilder.DropForeignKey(
                name: "FK_messages_charges_tenant_id_charge_id",
                table: "messages");

            migrationBuilder.DropForeignKey(
                name: "FK_messages_conversations_tenant_id_conversation_id",
                table: "messages");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_charges_tenant_id_charge_id",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_payments_tenant_id_charge_id",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_messages_tenant_id_charge_id",
                table: "messages");

            migrationBuilder.DropIndex(
                name: "IX_messages_tenant_id_conversation_id",
                table: "messages");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_customers_tenant_id_id",
                table: "customers");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_conversations_tenant_id_id",
                table: "conversations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_charges_tenant_id_id",
                table: "charges");

            migrationBuilder.CreateIndex(
                name: "IX_payments_charge_id",
                table: "payments",
                column: "charge_id");

            migrationBuilder.CreateIndex(
                name: "IX_messages_charge_id",
                table: "messages",
                column: "charge_id");

            migrationBuilder.CreateIndex(
                name: "IX_messages_conversation_id",
                table: "messages",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "IX_installments_charge_id",
                table: "installments",
                column: "charge_id");

            migrationBuilder.CreateIndex(
                name: "IX_conversations_customer_id",
                table: "conversations",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_customer_id",
                table: "contacts",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_charges_customer_id",
                table: "charges",
                column: "customer_id");

            migrationBuilder.AddForeignKey(
                name: "FK_charges_customers_customer_id",
                table: "charges",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_contacts_customers_customer_id",
                table: "contacts",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_conversations_customers_customer_id",
                table: "conversations",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_installments_charges_charge_id",
                table: "installments",
                column: "charge_id",
                principalTable: "charges",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_messages_charges_charge_id",
                table: "messages",
                column: "charge_id",
                principalTable: "charges",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_messages_conversations_conversation_id",
                table: "messages",
                column: "conversation_id",
                principalTable: "conversations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_charges_charge_id",
                table: "payments",
                column: "charge_id",
                principalTable: "charges",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
