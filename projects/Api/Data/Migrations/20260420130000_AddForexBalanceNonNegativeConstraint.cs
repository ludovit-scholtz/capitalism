using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <summary>
    /// Adds a database-level CHECK constraint that prevents <c>PlayerCurrencyBalance.Balance</c>
    /// from ever becoming negative, providing a persistence-layer safety net in addition to the
    /// application-layer insufficient-funds check and the serializable-transaction isolation used
    /// in <c>ExecuteForexSwap</c>.
    ///
    /// SQLite does not support <c>ALTER TABLE … ADD CONSTRAINT</c> so the constraint is only
    /// applied for the PostgreSQL provider (production). The SQLite test database enforces the
    /// rule at the application layer via the insufficient-funds guard.
    /// </summary>
    public partial class AddForexBalanceNonNegativeConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only PostgreSQL supports ALTER TABLE … ADD CONSTRAINT.
            // SQLite (used in tests) enforces the invariant at the application layer.
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.AddCheckConstraint(
                    name: "CK_PlayerCurrencyBalances_Balance_NonNegative",
                    table: "PlayerCurrencyBalances",
                    sql: "\"Balance\" >= 0");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.DropCheckConstraint(
                    name: "CK_PlayerCurrencyBalances_Balance_NonNegative",
                    table: "PlayerCurrencyBalances");
            }
        }
    }
}
